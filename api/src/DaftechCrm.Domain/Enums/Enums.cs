namespace DaftechCrm.Domain.Enums;

public enum ClientAccountStatus { Pending, Approved, Rejected }

public enum EmployeeAccountStatus { Active, Disabled }

public enum AgreementStatus { Active, Expired, Pending }

public enum BillingTier { Basic, Intermediate, Advanced }

public enum TicketCategory { SqlDatabaseError, Bug, Other }

public enum TicketStatus
{
    Submitted,
    Forwarded,
    Assigned,
    InProgress,
    Resolved,
    AwaitingClientConfirmation,
    Escalated,
    Closed
}

public enum MaintenanceStatus { Resolved, InProgress, Recurring }

/// <summary>
/// ItSupport is retired (Admin now absorbs that work — see
/// AuthorizationPolicies) but the value is kept rather than deleted:
/// Employee.Roles is stored as a delimited string, not a real FK/lookup
/// table, so removing this would throw Enum.Parse failures on any
/// existing employee row that still has it. New employees should never
/// be assigned ItSupport going forward.
/// </summary>
public enum EmployeeRole { Admin, ItSupport, EmployeeTechnician }

public enum DeviceType { Laptop, Pc, Tablet, Other }

public enum DeviceAccessStatus { Allowed, Revoked }

public enum NotificationRecipientType { Admin, ItSupport, Employee, Client }

public enum ClosureReason
{
    ClientConfirmedSatisfied,
    AutoClosedNoResponse
}

/// <summary>Distinguishes which table AccountId on LoginSession/session-related records points into.</summary>
public enum SessionAccountType
{
    Employee,
    Client
}

public enum PasswordResetRequestStatus
{
    Pending,
    OtpIssued,
    Dismissed
}
