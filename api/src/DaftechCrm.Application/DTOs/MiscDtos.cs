using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Application.DTOs;

public record AgreementTypeDto(Guid Id, string Name, string? Description, bool IsSystemDefined);
public record CreateAgreementTypeRequest(string Name, string? Description);
public record UpdateAgreementTypeRequest(string? Description);

public record SystemProductDto(
    Guid Id, Guid ClientId, string ReferenceNumber, string Name, string? Description, DateOnly? DeploymentDate
);
public record CreateSystemProductRequest(Guid ClientId, string Name, string? Description, DateOnly? DeploymentDate);
public record UpdateSystemProductRequest(string Name, string? Description, DateOnly? DeploymentDate);

public record TrainingSessionDto(
    Guid AgreementId, Guid? TrainerEmployeeId, string? TrainerEmployeeName,
    DateOnly? StartDate, DateOnly? EndDate, string? Location, string? Participants, string? Attendance,
    string? TopicsCovered, string? IssuesOrQuestions, string? TrainerComments,
    string? ClientRepresentativeConfirmation, string? ClientRepresentativeComments,
    TrainingCompletionStatus CompletionStatus, bool FollowUpRequired, string? FollowUpNotes, string? ScanFileName
);

/// <summary>
/// All fields optional/settable independently — the Admin/Trainer fills
/// this in over time as the training session progresses (schedule it
/// first, add attendance/topics/comments afterward, mark complete last).
/// TrainerEmployeeId must reference an Employee with the Trainer
/// responsibility (see EmployeeRole.Trainer) — enforced server-side.
/// </summary>
public record SaveTrainingSessionRequest(
    Guid? TrainerEmployeeId, DateOnly? StartDate, DateOnly? EndDate, string? Location,
    string? Participants, string? Attendance, string? TopicsCovered, string? IssuesOrQuestions,
    string? TrainerComments, string? ClientRepresentativeConfirmation, string? ClientRepresentativeComments,
    TrainingCompletionStatus CompletionStatus, bool FollowUpRequired, string? FollowUpNotes
);

public record AgreementDto(
    Guid Id, Guid SystemProductId, Guid ClientId, string ClientName, string SystemProductName,
    Guid AgreementTypeId, string AgreementTypeName,
    string DocumentNumber, string? ScannedFileUrl, string AgreementPlace,
    DateOnly SignDate, DateOnly ExpiryDate, int SupportWindowMonths, AgreementStatus Status, BillingTier BillingTier,
    string? Details, TrainingSessionDto? TrainingSession
);

/// <summary>
/// DocumentNumber is system-generated (see ReferenceNumberService), not
/// supplied by the caller. SignDate IS admin-entered here (defaults to
/// today in the UI, but the Admin can set/backdate it) — creating an
/// Agreement no longer forces "today" the way the old Client-level model
/// did. If AgreementTypeId resolves to the Support type, creation is
/// rejected unless the same SystemProduct already has a completed
/// Training agreement (TrainingSession.EndDate set) — training must
/// finish first, per-SystemProduct (see AgreementService.CreateAsync).
/// Never overwrites an existing agreement — always inserts a new row,
/// even for the same SystemProduct/AgreementType pair (e.g. a renewal).
/// </summary>
public record CreateAgreementRequest(
    Guid SystemProductId, Guid AgreementTypeId, string? ScannedFileUrl, string AgreementPlace,
    DateOnly SignDate, DateOnly? ExpiryDate, int SupportWindowMonths, BillingTier BillingTier, string? Details
);

public record TimeLogDto(Guid Id, Guid EmployeeId, DateOnly Date, DateTimeOffset? StartTime, DateTimeOffset? FinishTime, double? TotalHours);

public record MaintenanceRecordDto(
    Guid Id, DateOnly Date, string Category, string Description,
    Guid PerformedByEmployeeId, MaintenanceStatus Status, string? Remarks
);

public record CreateMaintenanceRecordRequest(string Category, string Description, Guid PerformedByEmployeeId, MaintenanceStatus Status, string? Remarks);

public record NotificationDto(Guid Id, NotificationRecipientType RecipientType, string RecipientId, string EventType, string Message, DateTimeOffset DateSent, bool ReadStatus);
