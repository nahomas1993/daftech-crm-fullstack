using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

public class EmployeeService : IEmployeeService
{
    private static readonly TicketStatus[] OpenStatuses = { TicketStatus.Assigned, TicketStatus.InProgress };

    private readonly IAppDbContext _db;
    private readonly AccountCredentialService _credentials;
    private readonly AccountReferenceIdService _accountRefIds;

    public EmployeeService(IAppDbContext db, AccountCredentialService credentials, AccountReferenceIdService accountRefIds)
    {
        _db = db;
        _credentials = credentials;
        _accountRefIds = accountRefIds;
    }

    public async Task<EmployeeRegisteredResult> RegisterAsync(CreateEmployeeRequest request, CancellationToken ct = default)
    {
        var issued = await _credentials.IssueForNameAsync(request.FullName, ct);

        var employee = new Employee
        {
            FullName = request.FullName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            Specialization = request.Specialization,
            Roles = request.Roles.ToList(),
            ExtraRoleLabels = request.ExtraRoleLabels.ToList(),
            AllowedIpAddresses = request.AllowedIpAddresses.ToList(),
            AccountRefId = await _accountRefIds.GenerateForEmployeeAsync(request.Roles, ct),
            Username = issued.Username,
            PasswordHash = PasswordHasher.Hash(issued.OneTimePassword),
            MustChangePassword = true,
        };
        _db.Add(employee);
        await _db.SaveChangesAsync(ct);

        var (sent, error) = await _credentials.SendCredentialEmailAsync(
            employee.Email, employee.FullName, issued.Username, issued.OneTimePassword, ct);

        var dto = await ToDto(employee, ct);
        return new EmployeeRegisteredResult(dto, issued.Username, issued.OneTimePassword, sent, error);
    }

    /// <summary>Admin retry — SRS v2.0 §4.3.1: if the original credential email failed, generate a fresh OTP and resend (the old OTP is invalidated, since the plaintext was never persisted).</summary>
    public async Task<ResendCredentialEmailResult> ResendCredentialEmailAsync(Guid employeeId, CancellationToken ct = default)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new InvalidOperationException("Employee not found.");

        var newOneTimePassword = await _credentials.RegenerateOneTimePasswordAsync(ct);
        employee.PasswordHash = PasswordHasher.Hash(newOneTimePassword);
        employee.MustChangePassword = true;
        _db.Update(employee);
        await _db.SaveChangesAsync(ct);

        var (sent, error) = await _credentials.SendCredentialEmailAsync(
            employee.Email, employee.FullName, employee.Username, newOneTimePassword, ct);
        return new ResendCredentialEmailResult(sent, error);
    }

    public async Task<IReadOnlyList<EmployeeDto>> GetAllAsync(CancellationToken ct = default)
    {
        var employees = await _db.Employees.AsNoTracking().Where(e => !e.IsDeleted).ToListAsync(ct);

        // Two grouped queries covering every employee, instead of the
        // previous 2-queries-per-employee loop (an N+1 that meant a
        // 20-employee list issued 40 round trips to Postgres). GroupBy here
        // translates to SQL GROUP BY, so this scales with ticket volume, not
        // with (employee count × ticket volume).
        var openCounts = await _db.Tickets
            .Where(t => t.AssignedEmployeeId != null && OpenStatuses.Contains(t.Status))
            .GroupBy(t => t.AssignedEmployeeId!.Value)
            .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.Count, ct);

        var avgScores = await _db.Tickets
            .Where(t => t.AssignedEmployeeId != null && t.SatisfactionScore != null)
            .GroupBy(t => t.AssignedEmployeeId!.Value)
            .Select(g => new { EmployeeId = g.Key, Avg = g.Average(t => t.SatisfactionScore!.Value) })
            .ToDictionaryAsync(x => x.EmployeeId, x => (double?)x.Avg, ct);

        return employees.Select(e => new EmployeeDto(
            e.Id, e.FullName, e.Email, e.PhoneNumber, e.Specialization, e.Roles, e.ExtraRoleLabels, e.AccountStatus, e.AllowedIpAddresses,
            e.DisabledAt, e.DisabledReason,
            openCounts.GetValueOrDefault(e.Id, 0), avgScores.GetValueOrDefault(e.Id),
            e.Username, e.MustChangePassword, e.AccountRefId
        )).ToList();
    }

    public async Task<PagedResult<EmployeeDto>> GetAllPagedAsync(PaginationQuery query, CancellationToken ct = default)
    {
        var totalCount = await _db.Employees.CountAsync(e => !e.IsDeleted, ct);

        var employees = await _db.Employees
            .AsNoTracking()
            .Where(e => !e.IsDeleted)
            .OrderBy(e => e.FullName)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var employeeIds = employees.Select(e => e.Id).ToList();

        // Same grouped-query approach as GetAllAsync, scoped to just this page's employees.
        var openCounts = await _db.Tickets
            .Where(t => t.AssignedEmployeeId != null && employeeIds.Contains(t.AssignedEmployeeId!.Value) && OpenStatuses.Contains(t.Status))
            .GroupBy(t => t.AssignedEmployeeId!.Value)
            .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.Count, ct);

        var avgScores = await _db.Tickets
            .Where(t => t.AssignedEmployeeId != null && employeeIds.Contains(t.AssignedEmployeeId!.Value) && t.SatisfactionScore != null)
            .GroupBy(t => t.AssignedEmployeeId!.Value)
            .Select(g => new { EmployeeId = g.Key, Avg = g.Average(t => t.SatisfactionScore!.Value) })
            .ToDictionaryAsync(x => x.EmployeeId, x => (double?)x.Avg, ct);

        var items = employees.Select(e => new EmployeeDto(
            e.Id, e.FullName, e.Email, e.PhoneNumber, e.Specialization, e.Roles, e.ExtraRoleLabels, e.AccountStatus, e.AllowedIpAddresses,
            e.DisabledAt, e.DisabledReason,
            openCounts.GetValueOrDefault(e.Id, 0), avgScores.GetValueOrDefault(e.Id),
            e.Username, e.MustChangePassword, e.AccountRefId
        )).ToList();

        return new PagedResult<EmployeeDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<EmployeeDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        return employee is null ? null : await ToDto(employee, ct);
    }

    public async Task<EmployeeDto> DisableAsync(Guid employeeId, DisableEmployeeRequest request, CancellationToken ct = default)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new InvalidOperationException("Employee not found.");

        employee.AccountStatus = EmployeeAccountStatus.Disabled;
        employee.DisabledAt = DateTimeOffset.UtcNow;
        employee.DisabledReason = request.Reason;
        _db.Update(employee);

        // Revoke every active device session immediately — offboarding cuts access now, not on next login.
        var sessions = await _db.DeviceSessions.Where(d => d.EmployeeId == employeeId && d.AccessStatus == DeviceAccessStatus.Allowed).ToListAsync(ct);
        foreach (var s in sessions)
        {
            s.AccessStatus = DeviceAccessStatus.Revoked;
            _db.Update(s);
        }

        await _db.SaveChangesAsync(ct);
        return await ToDto(employee, ct);
    }

    public async Task<EmployeeDto> EnableAsync(Guid employeeId, CancellationToken ct = default)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new InvalidOperationException("Employee not found.");

        employee.AccountStatus = EmployeeAccountStatus.Active;
        employee.DisabledAt = null;
        employee.DisabledReason = null;
        _db.Update(employee);
        await _db.SaveChangesAsync(ct);
        return await ToDto(employee, ct);
    }

    public async Task<EmployeeDto> UpdateAsync(Guid employeeId, UpdateEmployeeRequest request, CancellationToken ct = default)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId && !e.IsDeleted, ct)
            ?? throw new InvalidOperationException("Employee not found.");

        employee.FullName = request.FullName;
        employee.Email = request.Email;
        employee.PhoneNumber = request.PhoneNumber;
        employee.Specialization = request.Specialization;
        _db.Update(employee);
        await _db.SaveChangesAsync(ct);
        return await ToDto(employee, ct);
    }

    public async Task DeleteAsync(Guid employeeId, CancellationToken ct = default)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId && !e.IsDeleted, ct)
            ?? throw new InvalidOperationException("Employee not found.");

        employee.IsDeleted = true;
        employee.DeletedAt = DateTimeOffset.UtcNow;
        // A deleted account shouldn't remain a live login either — same
        // session-revocation step as Disable, so an already-issued
        // refresh token can't still be used after deletion.
        employee.AccountStatus = EmployeeAccountStatus.Disabled;
        _db.Update(employee);

        var sessions = await _db.DeviceSessions.Where(d => d.EmployeeId == employeeId && d.AccessStatus == DeviceAccessStatus.Allowed).ToListAsync(ct);
        foreach (var s in sessions)
        {
            s.AccessStatus = DeviceAccessStatus.Revoked;
            _db.Update(s);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<EmployeeDto> AddAllowedIpAsync(Guid employeeId, AddAllowedIpRequest request, CancellationToken ct = default)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new InvalidOperationException("Employee not found.");

        if (!employee.AllowedIpAddresses.Contains(request.IpAddress))
            employee.AllowedIpAddresses.Add(request.IpAddress);

        _db.Update(employee);
        await _db.SaveChangesAsync(ct);
        return await ToDto(employee, ct);
    }

    public async Task<EmployeeDto> RemoveAllowedIpAsync(Guid employeeId, string ip, CancellationToken ct = default)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new InvalidOperationException("Employee not found.");

        employee.AllowedIpAddresses.Remove(ip);
        _db.Update(employee);
        await _db.SaveChangesAsync(ct);
        return await ToDto(employee, ct);
    }

    public async Task<IReadOnlyList<DeviceSessionDto>> GetDevicesAsync(Guid employeeId, CancellationToken ct = default) =>
        await _db.DeviceSessions.Where(d => d.EmployeeId == employeeId)
            .OrderByDescending(d => d.LastSeen)
            .Select(d => new DeviceSessionDto(d.Id, d.DeviceType, d.DeviceIdentifier, d.IpAddress, d.LastSeen, d.AccessStatus))
            .ToListAsync(ct);

    public async Task RevokeDeviceAsync(Guid deviceSessionId, CancellationToken ct = default)
    {
        var session = await _db.DeviceSessions.FirstOrDefaultAsync(d => d.Id == deviceSessionId, ct)
            ?? throw new InvalidOperationException("Device session not found.");
        session.AccessStatus = DeviceAccessStatus.Revoked;
        _db.Update(session);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LoginRecordDto>> GetLoginHistoryAsync(Guid employeeId, CancellationToken ct = default) =>
        await _db.LoginRecords.Where(l => l.EmployeeId == employeeId)
            .OrderByDescending(l => l.Timestamp)
            .Select(l => new LoginRecordDto(l.Id, l.Timestamp, l.IpAddress, l.DeviceType, l.DeviceIdentifier, l.Allowed, l.Reason))
            .ToListAsync(ct);

    private async Task<EmployeeDto> ToDto(Employee e, CancellationToken ct)
    {
        var openCount = await _db.Tickets.CountAsync(t => t.AssignedEmployeeId == e.Id && OpenStatuses.Contains(t.Status), ct);

        // Average satisfaction score across the employee's tickets that were
        // actually rated (auto-closes with no rating are excluded).
        var scores = await _db.Tickets
            .AsNoTracking()
            .Where(t => t.AssignedEmployeeId == e.Id && t.SatisfactionScore != null)
            .Select(t => t.SatisfactionScore!.Value)
            .ToListAsync(ct);
        double? avgScore = scores.Count > 0 ? scores.Average() : null;

        return new EmployeeDto(
            e.Id, e.FullName, e.Email, e.PhoneNumber, e.Specialization, e.Roles, e.ExtraRoleLabels, e.AccountStatus, e.AllowedIpAddresses,
            e.DisabledAt, e.DisabledReason, openCount, avgScore, e.Username, e.MustChangePassword, e.AccountRefId
        );
    }
}
