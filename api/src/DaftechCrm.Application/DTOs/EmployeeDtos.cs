using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Application.DTOs;

public record EmployeeDto(
    Guid Id, string FullName, string Email, string PhoneNumber, string Specialization,
    IReadOnlyList<EmployeeRole> Roles, IReadOnlyList<string> ExtraRoleLabels, EmployeeAccountStatus AccountStatus, IReadOnlyList<string> AllowedIpAddresses,
    DateTimeOffset? DisabledAt, string? DisabledReason, int OpenTicketCount,
    double? AverageSatisfactionScore, string Username, bool MustChangePassword, string AccountRefId
);

public record CreateEmployeeRequest(
    string FullName, string Email, string PhoneNumber, string Specialization,
    IReadOnlyList<EmployeeRole> Roles, IReadOnlyList<string> ExtraRoleLabels, IReadOnlyList<string> AllowedIpAddresses
);

/// <summary>
/// Returned once, immediately after registration. OneTimePassword is never
/// retrievable again after this response. EmailDelivery reports whether the
/// credential email actually sent — per SRS v2.0 §4.3.1, if it failed the
/// Admin still has the plaintext here to relay manually or retry sending.
/// </summary>
public record EmployeeRegisteredResult(EmployeeDto Employee, string Username, string OneTimePassword, bool EmailSent, string? EmailError);

public record DisableEmployeeRequest(string Reason);

/// <summary>
/// Admin edits an existing employee's profile fields. Deliberately
/// excludes Roles/AllowedIpAddresses/AccountStatus — those already have
/// dedicated endpoints (role changes aren't exposed via this edit form;
/// IP allow-list has its own add/remove; status has enable/disable) so
/// this one endpoint only ever touches the plain profile fields shown on
/// the edit form.
/// </summary>
public record UpdateEmployeeRequest(string FullName, string Email, string PhoneNumber, string Specialization);

public record AddAllowedIpRequest(string IpAddress);

public record DeviceSessionDto(Guid Id, DeviceType DeviceType, string DeviceIdentifier, string IpAddress, DateTimeOffset LastSeen, DeviceAccessStatus AccessStatus);

public record LoginRecordDto(Guid Id, DateTimeOffset Timestamp, string IpAddress, DeviceType DeviceType, string DeviceIdentifier, bool Allowed, string? Reason);

/// <summary>Employee logs in with the system-issued username and their current password (one-time or self-chosen).</summary>
public record EmployeeLoginRequest(string Username, string Password, DeviceType DeviceType, string DeviceIdentifier);

/// <summary>
/// Tokens is null when Success is false, or when Success is true but
/// MustChangePassword is also true — a forced-change account gets no
/// access token until the password is actually changed, so a leaked
/// one-time password can't be used to call anything except the
/// change-password endpoint.
/// </summary>
public record EmployeeLoginResult(bool Success, string? Message, string IpAddress, EmployeeDto? Employee, bool MustChangePassword, AuthTokenResult? Tokens = null);

/// <summary>Both fields are sent so the server can enforce match — never trust confirmation logic to the client alone.</summary>
public record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmNewPassword);

/// <summary>Admin can retry sending the credential email if the original attempt failed (SRS v2.0 §4.3.1).</summary>
public record ResendCredentialEmailResult(bool EmailSent, string? EmailError);
