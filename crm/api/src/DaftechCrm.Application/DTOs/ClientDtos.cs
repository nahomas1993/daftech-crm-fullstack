using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Application.DTOs;

public record ClientDto(
    Guid Id, string Name, string IdNumber, string PhoneNumber, string Email, string Office, string Location,
    string? Region, string? Zone, string? City, string? Woreda,
    string KycType, string KycContact, string? ItSupportContact,
    ClientAccountStatus AccountStatus, DateOnly OnboardingDate, string? RejectionReason,
    string? Username, bool MustChangePassword, string AccountRefId
);

/// <summary>Self-service signup — still available, still lands in Pending for Admin approval, still has no credentials until approved. IdNumber is system-generated, not supplied by the applicant.</summary>
public record CreateClientSignupRequest(
    string Name, string PhoneNumber, string Email, string Office, string Location,
    string? Region, string? Zone, string? City, string? Woreda
);

/// <summary>Admin registers a client directly — Approved immediately, with credentials issued and emailed in the same request. IdNumber is system-generated, not entered by the Admin.</summary>
public record RegisterClientRequest(
    string Name, string PhoneNumber, string Email, string Office, string Location,
    string? Region, string? Zone, string? City, string? Woreda,
    string KycType, string KycContact, string? ItSupportContact
);

public record ClientRegisteredResult(ClientDto Client, string Username, string OneTimePassword, bool EmailSent, string? EmailError);

public record RejectClientRequest(string Reason);

/// <summary>Admin edits an existing client's profile fields. Excludes AccountStatus/Username/credentials — those go through the dedicated approve/reject/credential-resend endpoints.</summary>
public record UpdateClientRequest(
    string Name, string PhoneNumber, string Email, string Office, string Location,
    string? Region, string? Zone, string? City, string? Woreda,
    string KycType, string KycContact, string? ItSupportContact
);

/// <summary>Client logs in with the system-issued username and their current password.</summary>
public record ClientLoginRequest(string Username, string Password);

/// <summary>Tokens is null when Success is false, or when MustChangePassword is true (see EmployeeLoginResult for why).</summary>
public record ClientLoginResult(bool Success, string? Message, ClientDto? Client, bool MustChangePassword, AuthTokenResult? Tokens = null);

public record ClientChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmNewPassword);

public record ResendClientCredentialEmailResult(bool EmailSent, string? EmailError);
