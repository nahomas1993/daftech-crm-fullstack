using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Domain.Entities;

/// <summary>
/// A self-service "forgot password" request awaiting Admin action. There is
/// no emailed reset link in this system — every credential (initial or
/// reset) is admin-issued and hand-delivered by email, matching
/// AccountCredentialService's existing registration/resend flow. This
/// entity is just the queue an Admin works from: the requester identifies
/// themselves by username, an Admin reviews it, and either issues a fresh
/// one-time password (which also flips MustChangePassword back on) or
/// dismisses the request.
/// </summary>
public class PasswordResetRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public SessionAccountType AccountType { get; set; }

    /// <summary>FK into Employees or Clients depending on AccountType.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Username as typed by the requester — kept even though AccountId is resolved, for the Admin's audit trail.</summary>
    public string Username { get; set; } = default!;

    /// <summary>Optional free-text the requester can add (e.g. "locked out on new phone").</summary>
    public string? Note { get; set; }

    public string RequestIpAddress { get; set; } = default!;

    public PasswordResetRequestStatus Status { get; set; } = PasswordResetRequestStatus.Pending;

    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Set when an Admin issues a new OTP or dismisses the request.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>Full name of the Admin who actioned this request, captured at action time for the audit trail.</summary>
    public string? ResolvedByName { get; set; }

    public string? DismissReason { get; set; }
}
