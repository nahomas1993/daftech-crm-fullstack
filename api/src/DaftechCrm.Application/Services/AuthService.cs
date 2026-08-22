using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

public class AuthService : IAuthService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentRequestContext _requestContext;
    private readonly ISessionService _sessions;
    private readonly ITokenService _tokens;

    public AuthService(IAppDbContext db, ICurrentRequestContext requestContext, ISessionService sessions, ITokenService tokens)
    {
        _db = db;
        _requestContext = requestContext;
        _sessions = sessions;
        _tokens = tokens;
    }

    /// <summary>
    /// Looks up the username against Employees first, then Clients, and
    /// hands off to the matching login path unchanged — this method itself
    /// makes no password/status/role decisions, it only decides which
    /// table the username belongs to. The account's real Role(s) are only
    /// ever read from that row and encoded into the JWT by IssueTokenPairAsync;
    /// nothing here or downstream infers a role from the username's shape.
    /// </summary>
    public async Task<UnifiedLoginResult> LoginAsync(UnifiedLoginRequest request, CancellationToken ct = default)
    {
        var isEmployee = await _db.Employees.AnyAsync(e => e.Username == request.Username, ct);
        if (isEmployee)
        {
            var result = await LoginEmployeeAsync(
                new EmployeeLoginRequest(request.Username, request.Password, request.DeviceType, request.DeviceIdentifier), ct);
            return new UnifiedLoginResult(
                result.Success, result.Message, result.Success ? SessionAccountType.Employee : null,
                result.Employee, null, result.MustChangePassword, result.Tokens);
        }

        var clientResult = await LoginClientAsync(new ClientLoginRequest(request.Username, request.Password), ct);
        return new UnifiedLoginResult(
            clientResult.Success, clientResult.Message, clientResult.Success ? SessionAccountType.Client : null,
            null, clientResult.Client, clientResult.MustChangePassword, clientResult.Tokens);
    }

    public async Task<EmployeeLoginResult> LoginEmployeeAsync(EmployeeLoginRequest request, CancellationToken ct = default)
    {
        var ip = _requestContext.ResolveClientIpAddress();
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Username == request.Username, ct);

        if (employee is null || !PasswordHasher.Verify(request.Password, employee.PasswordHash))
        {
            // Don't record a LoginRecord against an employee we couldn't identify —
            // but if the username matched and only the password was wrong, still log it.
            if (employee is not null)
                await RecordLoginAsync(employee.Id, ip, request.DeviceType, request.DeviceIdentifier, allowed: false, reason: "Incorrect password", ct);
            return new EmployeeLoginResult(false, "Incorrect username or password.", ip, null, false);
        }

        if (employee.AccountStatus == EmployeeAccountStatus.Disabled)
        {
            await RecordLoginAsync(employee.Id, ip, request.DeviceType, request.DeviceIdentifier, allowed: false, reason: "Account disabled", ct);
            return new EmployeeLoginResult(false, "This account has been disabled. Contact your Admin.", ip, null, false);
        }

        // A reset-issued OTP carries an expiry (initial signup OTPs don't —
        // OtpExpiresAt stays null until a reset is issued). Past it, the
        // temp password no longer works; the message points at the
        // existing self-service "Forgot password?" flow rather than
        // dead-ending the user.
        if (employee.MustChangePassword && employee.OtpExpiresAt is { } expiresAt && expiresAt < DateTimeOffset.UtcNow)
        {
            await RecordLoginAsync(employee.Id, ip, request.DeviceType, request.DeviceIdentifier, allowed: false, reason: "OTP expired", ct);
            return new EmployeeLoginResult(false, "This temporary password has expired. Click \"Forgot password?\" to request a new one.", ip, null, false);
        }

        // TEMPORARILY DISABLED — the deployed host's outbound IP isn't fixed/known
        // yet, so every login was being blocked. Uncomment to re-enable per-employee
        // IP allow-listing once the real deployment IP(s) are known.
        // if (employee.AllowedIpAddresses.Count > 0 && !employee.AllowedIpAddresses.Contains(ip))
        // {
        //     await RecordLoginAsync(employee.Id, ip, request.DeviceType, request.DeviceIdentifier, allowed: false, reason: "IP not on allow-list", ct);
        //     return new EmployeeLoginResult(false, $"Login blocked: {ip} is not an approved IP address for this account.", ip, null, false);
        // }

        await RecordLoginAsync(employee.Id, ip, request.DeviceType, request.DeviceIdentifier, allowed: true, reason: null, ct);
        await _sessions.OpenSessionAsync(SessionAccountType.Employee, employee.Id, ip, ct);

        // A forced password change must happen before any access token is
        // issued — otherwise a leaked one-time password could be used to
        // call every other endpoint, not just change-password.
        AuthTokenResult? tokens = null;
        if (!employee.MustChangePassword)
        {
            var subject = new TokenSubject(SessionAccountType.Employee, employee.Id, employee.Username, employee.Roles);
            var pair = await _tokens.IssueTokenPairAsync(subject, ip, ct);
            tokens = new AuthTokenResult(pair.AccessToken, pair.RefreshTokenPlainText, pair.AccessTokenExpiresAt);
        }

        var dto = await ToEmployeeDtoAsync(employee, ct);
        return new EmployeeLoginResult(true, null, ip, dto, employee.MustChangePassword, tokens);
    }

    public async Task ChangeEmployeePasswordAsync(Guid employeeId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new InvalidOperationException("Employee not found.");

        if (!PasswordHasher.Verify(request.CurrentPassword, employee.PasswordHash))
        {
            // A stale one-time password commonly means a PRIOR change-password
            // call already succeeded (e.g. the page was interrupted right
            // after, before the frontend could move on) — MustChangePassword
            // is already false in that case, so "current password is wrong"
            // is technically true but misleading; point the person at
            // signing in with whatever they set last time instead of
            // implying they mistyped the one-time password they were given.
            if (!employee.MustChangePassword)
                throw new InvalidOperationException("Your password has already been changed. Please sign in with your new password instead of the one-time password.");

            throw new InvalidOperationException("Current password is incorrect.");
        }

        if (request.NewPassword != request.ConfirmNewPassword)
            throw new InvalidOperationException("New password and confirmation do not match.");

        ValidatePasswordStrength(request.NewPassword);

        employee.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        employee.MustChangePassword = false;
        employee.OtpExpiresAt = null;
        _db.Update(employee);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ClientLoginResult> LoginClientAsync(ClientLoginRequest request, CancellationToken ct = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Username == request.Username, ct);

        if (client is null || client.PasswordHash is null || !PasswordHasher.Verify(request.Password, client.PasswordHash))
            return new ClientLoginResult(false, "Incorrect username or password.", null, false);

        if (client.AccountStatus != ClientAccountStatus.Approved)
            return new ClientLoginResult(false, "Your account is not yet approved.", null, false);

        if (client.MustChangePassword && client.OtpExpiresAt is { } clientExpiresAt && clientExpiresAt < DateTimeOffset.UtcNow)
            return new ClientLoginResult(false, "This temporary password has expired. Click \"Forgot password?\" to request a new one.", null, false);

        var ip = _requestContext.ResolveClientIpAddress();
        await _sessions.OpenSessionAsync(SessionAccountType.Client, client.Id, ip, ct);

        AuthTokenResult? tokens = null;
        if (!client.MustChangePassword)
        {
            var subject = new TokenSubject(SessionAccountType.Client, client.Id, client.Username ?? string.Empty, new List<EmployeeRole>());
            var pair = await _tokens.IssueTokenPairAsync(subject, ip, ct);
            tokens = new AuthTokenResult(pair.AccessToken, pair.RefreshTokenPlainText, pair.AccessTokenExpiresAt);
        }

        var dto = ToClientDto(client);
        return new ClientLoginResult(true, null, dto, client.MustChangePassword, tokens);
    }

    public async Task ChangeClientPasswordAsync(Guid clientId, ClientChangePasswordRequest request, CancellationToken ct = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, ct)
            ?? throw new InvalidOperationException("Client not found.");

        if (client.PasswordHash is null || !PasswordHasher.Verify(request.CurrentPassword, client.PasswordHash))
        {
            // Same reasoning as ChangeEmployeePasswordAsync above — a stale
            // one-time password after MustChangePassword is already false
            // usually means a previous attempt succeeded and something
            // interrupted the frontend before it could move the person
            // past this screen, not that they mistyped the OTP just now.
            if (!client.MustChangePassword)
                throw new InvalidOperationException("Your password has already been changed. Please sign in with your new password instead of the one-time password.");

            throw new InvalidOperationException("Current password is incorrect.");
        }

        if (request.NewPassword != request.ConfirmNewPassword)
            throw new InvalidOperationException("New password and confirmation do not match.");

        ValidatePasswordStrength(request.NewPassword);

        client.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        client.MustChangePassword = false;
        client.OtpExpiresAt = null;
        _db.Update(client);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<AuthTokenResult> RefreshAsync(RefreshTokenRequest request, string ipAddress, CancellationToken ct = default)
    {
        var pair = await _tokens.RefreshAsync(request.RefreshToken, ipAddress, ct);
        return new AuthTokenResult(pair.AccessToken, pair.RefreshTokenPlainText, pair.AccessTokenExpiresAt);
    }

    public Task RevokeRefreshTokenAsync(RevokeTokenRequest request, string ipAddress, CancellationToken ct = default) =>
        _tokens.RevokeAsync(request.RefreshToken, ipAddress, ct);

    /// <summary>
    /// Real enforcement point — the Angular forms validate the same rule
    /// client-side for immediate feedback (see frontend core/password-strength.ts),
    /// but that's UX only; this is what actually stops a weak password
    /// from being set, including via direct API calls that bypass the UI.
    /// </summary>
    private static void ValidatePasswordStrength(string password)
    {
        if (password.Length < 8)
            throw new InvalidOperationException("New password must be at least 8 characters.");
        if (!password.Any(char.IsLower))
            throw new InvalidOperationException("New password must include at least one lowercase letter.");
        if (!password.Any(char.IsUpper))
            throw new InvalidOperationException("New password must include at least one uppercase letter.");
        if (!password.Any(char.IsDigit))
            throw new InvalidOperationException("New password must include at least one number.");
    }

    private async Task RecordLoginAsync(Guid employeeId, string ip, DeviceType deviceType, string deviceIdentifier, bool allowed, string? reason, CancellationToken ct)
    {
        var record = new LoginRecord
        {
            EmployeeId = employeeId,
            IpAddress = ip,
            DeviceType = deviceType,
            DeviceIdentifier = deviceIdentifier,
            Allowed = allowed,
            Reason = reason,
        };
        _db.Add(record);

        if (allowed)
        {
            var existing = await _db.DeviceSessions.FirstOrDefaultAsync(
                d => d.EmployeeId == employeeId && d.DeviceIdentifier == deviceIdentifier, ct);

            if (existing is not null)
            {
                existing.IpAddress = ip;
                existing.LastSeen = DateTimeOffset.UtcNow;
                existing.AccessStatus = DeviceAccessStatus.Allowed;
                _db.Update(existing);
            }
            else
            {
                _db.Add(new DeviceSession
                {
                    EmployeeId = employeeId,
                    DeviceType = deviceType,
                    DeviceIdentifier = deviceIdentifier,
                    IpAddress = ip,
                    LastSeen = DateTimeOffset.UtcNow,
                    AccessStatus = DeviceAccessStatus.Allowed,
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<EmployeeDto> ToEmployeeDtoAsync(Employee e, CancellationToken ct)
    {
        var openCount = await _db.Tickets.CountAsync(t => t.AssignedEmployeeId == e.Id && (t.Status == TicketStatus.Assigned || t.Status == TicketStatus.InProgress), ct);
        var scores = await _db.Tickets.Where(t => t.AssignedEmployeeId == e.Id && t.SatisfactionScore != null).Select(t => t.SatisfactionScore!.Value).ToListAsync(ct);
        double? avgScore = scores.Count > 0 ? scores.Average() : null;

        return new EmployeeDto(
            e.Id, e.FullName, e.Email, e.PhoneNumber, e.Specialization, e.Roles, e.ExtraRoleLabels, e.AccountStatus, e.AllowedIpAddresses,
            e.DisabledAt, e.DisabledReason, openCount, avgScore, e.Username, e.MustChangePassword, e.AccountRefId
        );
    }

    private static ClientDto ToClientDto(Client c) => new(
        c.Id, c.Name, c.IdNumber, c.PhoneNumber, c.Email, c.Office, c.Location,
        c.Region, c.Zone, c.City, c.Woreda,
        c.KycType, c.KycContact, c.ItSupportContact, c.AccountStatus, c.OnboardingDate, c.RejectionReason,
        c.Username, c.MustChangePassword, c.AccountRefId
    );
}