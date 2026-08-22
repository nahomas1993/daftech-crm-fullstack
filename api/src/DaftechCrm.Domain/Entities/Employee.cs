using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Domain.Entities;

public class Employee
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Human-readable, permanent account identifier — "DAF-ADMIN-####" for an
    /// Admin, "DAF-EMP-####" for any other employee role, where #### is 4
    /// random digits. Generated once at registration (see
    /// AccountReferenceIdService) and never changes afterward, including
    /// across profile edits or role changes. Purely a display/lookup label —
    /// every [Authorize] policy checks Roles below, never this string, so it
    /// can't be used to escalate privilege even if guessed or edited in transit.
    /// </summary>
    public string AccountRefId { get; set; } = default!;

    public string FullName { get; set; } = default!;

    /// <summary>Used for login-credential delivery (SRS v2.0 §4.3.1) and notifications.</summary>
    public string Email { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;

    /// <summary>Technical specialization: Front-end, Back-end, or Database (SRS v2.0 §4.4.1) — extendable free text, not a closed enum, per "extendable list" wording.</summary>
    public string Specialization { get; set; } = default!;

    /// <summary>Stored as a comma-separated list of EmployeeRole values (see EF config).</summary>
    public List<EmployeeRole> Roles { get; set; } = new();

    /// <summary>
    /// Additional, purely-descriptive role labels an Admin has defined via
    /// the Settings → Locations tab (LocationEntry, Type=CustomRole) and
    /// assigned to this employee. These carry NO authorization meaning —
    /// every [Authorize] policy in the app checks only the hardcoded
    /// Roles/EmployeeRole above, unchanged. Purely for org-chart/labeling
    /// purposes until (if ever) a real permission system is built on top.
    /// </summary>
    public List<string> ExtraRoleLabels { get; set; } = new();

    public EmployeeAccountStatus AccountStatus { get; set; } = EmployeeAccountStatus.Active;

    /// <summary>
    /// System-generated login username (initials + random digits, e.g.
    /// "mf4821") — set once at registration by AccountCredentialService,
    /// never chosen by the employee.
    /// </summary>
    public string Username { get; set; } = default!;

    /// <summary>PBKDF2 hash of the current password — never the plaintext. See PasswordHasher.</summary>
    public string PasswordHash { get; set; } = default!;

    /// <summary>
    /// True from registration until the employee successfully changes their
    /// password. While true, every endpoint except the change-password flow
    /// is blocked for this account.
    /// </summary>
    public bool MustChangePassword { get; set; } = true;

    /// <summary>
    /// Deadline for the current one-time password, set only when an Admin
    /// issues a password-RESET OTP (PasswordResetService.IssueOtpAsync).
    /// Null for the initial signup OTP, which never expires — an employee
    /// who hasn't onboarded yet shouldn't get locked out before they've
    /// even logged in once. Checked at login; past this point the OTP is
    /// rejected with a message pointing to "Forgot password?".
    /// </summary>
    public DateTimeOffset? OtpExpiresAt { get; set; }

    /// <summary>Empty = no IP restriction, this account may log in from any IP.</summary>
    public List<string> AllowedIpAddresses { get; set; } = new();

    public DateTimeOffset? DisabledAt { get; set; }
    public string? DisabledReason { get; set; }

    /// <summary>
    /// Soft-delete flag. Tickets, time logs, maintenance records, and
    /// login/device history all reference EmployeeId, so a real DELETE
    /// would either orphan that history or be blocked by FK constraints —
    /// setting this instead removes the account from every "active" list
    /// and login path while keeping historical records intact. Distinct
    /// from AccountStatus/DisabledAt (Disable/Enable): disabling blocks
    /// login but keeps the account visible and reversible day-to-day;
    /// deleting additionally hides it from the Employees list entirely.
    /// </summary>
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<Ticket> AssignedTickets { get; set; } = new List<Ticket>();
    public ICollection<TimeLog> TimeLogs { get; set; } = new List<TimeLog>();
    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();
    public ICollection<DeviceSession> DeviceSessions { get; set; } = new List<DeviceSession>();
    public ICollection<LoginRecord> LoginRecords { get; set; } = new List<LoginRecord>();
}

public class DeviceSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = default!;

    public DeviceType DeviceType { get; set; }
    public string DeviceIdentifier { get; set; } = default!;
    public string IpAddress { get; set; } = default!;
    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.UtcNow;
    public DeviceAccessStatus AccessStatus { get; set; } = DeviceAccessStatus.Allowed;
}

/// <summary>
/// A single login attempt with the resolved IP address — captured on every
/// employee login (successful or blocked) per the access-control requirement.
/// </summary>
public class LoginRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = default!;

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string IpAddress { get; set; } = default!;
    public DeviceType DeviceType { get; set; }
    public string DeviceIdentifier { get; set; } = default!;
    public bool Allowed { get; set; }
    public string? Reason { get; set; }
}
