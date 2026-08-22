using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

/// <summary>
/// See IPasswordResetService. Reuses AccountCredentialService for OTP
/// generation and email delivery so the credential itself is generated the
/// same way (readable alphabet, single-use, never persisted in plaintext)
/// regardless of whether it's a first issuance or a reset.
/// </summary>
public class PasswordResetService : IPasswordResetService
{
    private readonly IAppDbContext _db;
    private readonly AccountCredentialService _credentials;
    private readonly INotificationService _notifications;
    private readonly ISystemConfigurationService _config;

    public PasswordResetService(IAppDbContext db, AccountCredentialService credentials, INotificationService notifications, ISystemConfigurationService config)
    {
        _db = db;
        _credentials = credentials;
        _notifications = notifications;
        _config = config;
    }

    public async Task<PasswordResetRequestSubmittedResult> SubmitAsync(SubmitPasswordResetRequest request, string ipAddress, CancellationToken ct = default)
    {
        const string genericMessage = "If that username exists, your request has been sent to an Admin. You'll be contacted with a new temporary password.";

        var username = request.Username.Trim();
        if (username.Length == 0)
            return new PasswordResetRequestSubmittedResult(genericMessage);

        // Resolved by lookup rather than trusting request.AccountType alone —
        // the unified login page's forgot-password form no longer asks the
        // user which account type they are, so this tries Employees first,
        // then Clients, the same way AuthService.LoginAsync resolves a
        // unified login. request.AccountType is kept as a hint/fallback for
        // any older caller that still supplies it accurately.
        var employeeMatch = await _db.Employees.FirstOrDefaultAsync(e => e.Username == username, ct);
        var resolvedType = employeeMatch is not null ? SessionAccountType.Employee : SessionAccountType.Client;
        Guid? accountId = employeeMatch?.Id
            ?? (await _db.Clients.FirstOrDefaultAsync(c => c.Username == username, ct))?.Id;

        // Deliberately still returns success-shaped output for an unknown
        // username — the caller isn't authenticated, so distinguishing
        // "no such account" here would let this endpoint be used to
        // enumerate valid usernames.
        if (accountId is null)
            return new PasswordResetRequestSubmittedResult(genericMessage);

        // Avoid piling up duplicate pending requests if someone clicks submit twice.
        var alreadyPending = await _db.PasswordResetRequests.AnyAsync(
            r => r.AccountType == resolvedType && r.AccountId == accountId && r.Status == PasswordResetRequestStatus.Pending, ct);

        if (!alreadyPending)
        {
            var entity = new PasswordResetRequest
            {
                AccountType = resolvedType,
                AccountId = accountId.Value,
                Username = username,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                RequestIpAddress = ipAddress,
            };
            _db.Add(entity);
            await _db.SaveChangesAsync(ct);

            await _notifications.NotifyAsync(NotificationRecipientType.Admin, "ALL_ADMIN", "password_reset_requested",
                $"{username} requested a password reset.", ct);
        }

        return new PasswordResetRequestSubmittedResult(genericMessage);
    }

    public async Task<IReadOnlyList<PasswordResetRequestDto>> GetPendingAsync(CancellationToken ct = default) =>
        await ToDtosAsync(_db.PasswordResetRequests.Where(r => r.Status == PasswordResetRequestStatus.Pending).OrderBy(r => r.RequestedAt), ct);

    public async Task<IReadOnlyList<PasswordResetRequestDto>> GetAllAsync(CancellationToken ct = default) =>
        await ToDtosAsync(_db.PasswordResetRequests.OrderByDescending(r => r.RequestedAt), ct);

    public async Task<PasswordResetOtpIssuedResult> IssueOtpAsync(Guid requestId, string resolvedByName, CancellationToken ct = default)
    {
        var reset = await _db.PasswordResetRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new InvalidOperationException("Password reset request not found.");

        if (reset.Status != PasswordResetRequestStatus.Pending)
            throw new InvalidOperationException("This request has already been actioned.");

        var newOneTimePassword = await _credentials.RegenerateOneTimePasswordAsync(ct);
        var otpExpiryMinutes = await _config.GetIntAsync("Auth.OtpExpiryMinutes", ct);
        var otpExpiresAt = DateTimeOffset.UtcNow.AddMinutes(otpExpiryMinutes > 0 ? otpExpiryMinutes : 15);
        bool sent;
        string? error;

        if (reset.AccountType == SessionAccountType.Employee)
        {
            var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == reset.AccountId, ct)
                ?? throw new InvalidOperationException("Employee account no longer exists.");

            employee.PasswordHash = PasswordHasher.Hash(newOneTimePassword);
            employee.MustChangePassword = true;
            employee.OtpExpiresAt = otpExpiresAt;
            _db.Update(employee);
            await _db.SaveChangesAsync(ct);

            (sent, error) = await _credentials.SendCredentialEmailAsync(employee.Email, employee.FullName, employee.Username, newOneTimePassword, ct, otpExpiryMinutes);
            await _notifications.NotifyAsync(NotificationRecipientType.Employee, employee.Id.ToString(), "password_reset_issued",
                "An Admin issued you a new temporary password. Check your email.", ct);
        }
        else
        {
            var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == reset.AccountId, ct)
                ?? throw new InvalidOperationException("Client account no longer exists.");

            client.PasswordHash = PasswordHasher.Hash(newOneTimePassword);
            client.MustChangePassword = true;
            client.OtpExpiresAt = otpExpiresAt;
            _db.Update(client);
            await _db.SaveChangesAsync(ct);

            (sent, error) = await _credentials.SendCredentialEmailAsync(client.Email, client.Name, client.Username ?? reset.Username, newOneTimePassword, ct, otpExpiryMinutes);
            await _notifications.NotifyAsync(NotificationRecipientType.Client, client.Id.ToString(), "password_reset_issued",
                "An Admin issued you a new temporary password. Check your email.", ct);
        }

        reset.Status = PasswordResetRequestStatus.OtpIssued;
        reset.ResolvedAt = DateTimeOffset.UtcNow;
        reset.ResolvedByName = resolvedByName;
        _db.Update(reset);
        await _db.SaveChangesAsync(ct);

        return new PasswordResetOtpIssuedResult(reset.Username, newOneTimePassword, sent, error);
    }

    public async Task<PasswordResetRequestDto> DismissAsync(Guid requestId, string resolvedByName, DismissPasswordResetRequest request, CancellationToken ct = default)
    {
        var reset = await _db.PasswordResetRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new InvalidOperationException("Password reset request not found.");

        if (reset.Status != PasswordResetRequestStatus.Pending)
            throw new InvalidOperationException("This request has already been actioned.");

        reset.Status = PasswordResetRequestStatus.Dismissed;
        reset.ResolvedAt = DateTimeOffset.UtcNow;
        reset.ResolvedByName = resolvedByName;
        reset.DismissReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        _db.Update(reset);
        await _db.SaveChangesAsync(ct);

        return await ToDtoAsync(reset, ct);
    }

    private async Task<IReadOnlyList<PasswordResetRequestDto>> ToDtosAsync(IQueryable<PasswordResetRequest> query, CancellationToken ct)
    {
        var requests = await query.ToListAsync(ct);
        var result = new List<PasswordResetRequestDto>(requests.Count);
        foreach (var r in requests)
            result.Add(await ToDtoAsync(r, ct));
        return result;
    }

    private async Task<PasswordResetRequestDto> ToDtoAsync(PasswordResetRequest r, CancellationToken ct)
    {
        string displayName = r.Username;
        string email = string.Empty;

        if (r.AccountType == SessionAccountType.Employee)
        {
            var e = await _db.Employees.FirstOrDefaultAsync(x => x.Id == r.AccountId, ct);
            if (e is not null) { displayName = e.FullName; email = e.Email; }
        }
        else
        {
            var c = await _db.Clients.FirstOrDefaultAsync(x => x.Id == r.AccountId, ct);
            if (c is not null) { displayName = c.Name; email = c.Email; }
        }

        return new PasswordResetRequestDto(
            r.Id, r.AccountType, r.AccountId, r.Username, r.Note, r.RequestIpAddress, r.Status,
            r.RequestedAt, r.ResolvedAt, r.ResolvedByName, r.DismissReason, displayName, email
        );
    }
}
