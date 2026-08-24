namespace DaftechCrm.Domain.Enums;

public enum ClientAccountStatus { Pending, Approved, Rejected }

public enum EmployeeAccountStatus { Active, Disabled }

public enum AgreementStatus { Active, Expired, Pending }

public enum BillingTier { Basic, Intermediate, Advanced }

public enum TicketCategory { Frontend, Backend, Database }

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
///
/// Trainer is a dynamically assignable responsibility, not a separate
/// account type — an Employee can hold Trainer alongside
/// EmployeeTechnician (Employee.Roles is already a list), and an Admin
/// can add/remove it at any time via EmployeeService.SetResponsibilities.
/// Only employees with Trainer show up as candidates for a Training
/// agreement's auto-assigned or manually-added TrainingAssignment rows
/// (see ITrainerWorkloadService).
/// </summary>
public enum EmployeeRole { Admin, ItSupport, EmployeeTechnician, Trainer }

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
