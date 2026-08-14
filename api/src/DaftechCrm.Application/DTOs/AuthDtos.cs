using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Application.DTOs;

/// <summary>
/// Issued alongside a successful login. AccessToken is a short-lived JWT
/// sent as `Authorization: Bearer {AccessToken}` on every subsequent
/// request. RefreshToken is a long-lived opaque secret used only against
/// POST /api/auth/refresh to obtain a new pair — never sent to any other
/// endpoint. AccessTokenExpiresAt lets the frontend proactively refresh
/// before the token actually expires.
/// </summary>
public record AuthTokenResult(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt);

public record RefreshTokenRequest(string RefreshToken);

public record RevokeTokenRequest(string RefreshToken);

/// <summary>
/// Internal result from ITokenService — not returned directly over the
/// wire (AuthService folds this into AuthTokenResult).
/// </summary>
public record IssuedTokenPair(string AccessToken, string RefreshTokenPlainText, DateTimeOffset AccessTokenExpiresAt);

/// <summary>Minimal identity used to issue a token — enough to build JWT claims for either account type.</summary>
public record TokenSubject(SessionAccountType AccountType, Guid AccountId, string Username, IReadOnlyList<EmployeeRole> Roles);

/// <summary>
/// One login request shape for the unified login page — Admins, Employees,
/// and Clients all sign in with just a username and password here. Device
/// info is still collected (Employees use it for device-session tracking)
/// but defaults are supplied by the frontend automatically rather than
/// asked of the user, since this form no longer distinguishes account
/// types up front.
/// </summary>
public record UnifiedLoginRequest(string Username, string Password, DeviceType DeviceType, string DeviceIdentifier);

/// <summary>
/// AccountType tells the frontend which dashboard/guard to route to.
/// Everything else mirrors EmployeeLoginResult / ClientLoginResult — the
/// same MustChangePassword / null-Tokens rules apply regardless of which
/// account type actually matched.
/// </summary>
public record UnifiedLoginResult(
    bool Success, string? Message, SessionAccountType? AccountType,
    EmployeeDto? Employee, ClientDto? Client, bool MustChangePassword, AuthTokenResult? Tokens = null
);

/// <summary>
/// Self-service "forgot password" — submitted anonymously from either login
/// screen. There is no emailed reset link; this just queues the request for
/// an Admin to action (see PasswordResetRequest). AccountType tells the API
/// which table to resolve Username against.
/// </summary>
public record SubmitPasswordResetRequest(SessionAccountType AccountType, string Username, string? Note);

/// <summary>
/// Always returns the same generic acknowledgement regardless of whether
/// Username matched a real account — this endpoint is anonymous, so
/// confirming or denying account existence here would let it be used to
/// enumerate valid usernames.
/// </summary>
public record PasswordResetRequestSubmittedResult(string Message);

public record PasswordResetRequestDto(
    Guid Id, SessionAccountType AccountType, Guid AccountId, string Username, string? Note,
    string RequestIpAddress, PasswordResetRequestStatus Status, DateTimeOffset RequestedAt,
    DateTimeOffset? ResolvedAt, string? ResolvedByName, string? DismissReason,
    string DisplayName, string Email
);

public record DismissPasswordResetRequest(string Reason);

/// <summary>
/// Returned once, right after an Admin issues a fresh OTP for a reset
/// request. OneTimePassword is never retrievable again after this response
/// — same one-time-visibility rule as EmployeeRegisteredResult /
/// ClientRegisteredResult. Also flips MustChangePassword back on for the
/// account, exactly like ResendCredentialEmailAsync does for a fresh hire.
/// </summary>
public record PasswordResetOtpIssuedResult(string Username, string OneTimePassword, bool EmailSent, string? EmailError);
