using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Domain.Entities;

public class Client
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Human-readable, permanent account identifier — "DAF-CLI-####" where
    /// #### is 4 random digits. Generated once at registration/signup
    /// approval (see AccountReferenceIdService) and never changes afterward,
    /// including across profile edits. Distinct from IdNumber below (a
    /// separate KYC/business reference the client already has) — this is
    /// specifically the CRM login-account identifier. Purely a display/
    /// lookup label; role/permission checks never depend on it.
    /// </summary>
    public string AccountRefId { get; set; } = default!;

    public string Name { get; set; } = default!;
    public string IdNumber { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Office { get; set; } = default!;
    public string Location { get; set; } = default!;
    public string? Region { get; set; }
    public string? Zone { get; set; }
    public string? City { get; set; }
    public string? Woreda { get; set; }
    public string KycType { get; set; } = default!;
    public string KycContact { get; set; } = default!;
    public string? ItSupportContact { get; set; }
    public ClientAccountStatus AccountStatus { get; set; } = ClientAccountStatus.Pending;
    public DateOnly OnboardingDate { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>
    /// System-generated login username (initials + random digits) — null
    /// until credentials are issued (at registration for Admin-created
    /// clients, or at approval time for self-signup clients).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>PBKDF2 hash of the current password — null until credentials are issued. Never the plaintext.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>True until the client changes their password on first login.</summary>
    public bool MustChangePassword { get; set; } = true;

    /// <summary>
    /// Deadline for the current one-time password, set only when an Admin
    /// issues a password-RESET OTP (PasswordResetService.IssueOtpAsync).
    /// Null for the initial signup OTP, which never expires. Checked at
    /// login; past this point the OTP is rejected with a message pointing
    /// to "Forgot password?".
    /// </summary>
    public DateTimeOffset? OtpExpiresAt { get; set; }

    /// <summary>
    /// Soft-delete flag. Agreements, tickets, and trainings all reference
    /// ClientId, so a real DELETE would either orphan that history or be
    /// blocked by FK constraints — setting this instead removes the
    /// account from the Clients list and login path while keeping
    /// historical records intact.
    /// </summary>
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// This client's systems/products — each may carry its own set of
    /// agreements (Support, Training, etc.). Replaces the earlier direct
    /// Agreements collection now that Agreement hangs off SystemProduct
    /// instead of Client directly (see SystemProduct, Agreement).
    /// </summary>
    public ICollection<SystemProduct> SystemProducts { get; set; } = new List<SystemProduct>();

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
