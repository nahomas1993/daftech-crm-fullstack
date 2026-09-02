using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Application.DTOs;

public record AgreementTypeDto(Guid Id, string Name, string? Description, bool IsSystemDefined, bool IsTrainingItem, bool IsRequiredForCompletion);
public record CreateAgreementTypeRequest(string Name, string? Description, bool IsTrainingItem, bool IsRequiredForCompletion);
public record UpdateAgreementTypeRequest(string? Description, bool IsTrainingItem, bool IsRequiredForCompletion);

/// <summary>One Trainer/Technician's assignment to train on this system/product — roster entry, no lifecycle of its own. See TrainingAssignment.</summary>
public record TrainingAssignmentDto(Guid Id, Guid TrainerEmployeeId, string TrainerEmployeeName, DateTimeOffset AssignedAt);

/// <summary>Manual Assignment request body — Admin's pick from the dropdown (itself capped client-side, re-checked server-side against Training.MaxTrainersPerSystemProduct). See SystemProductsController.AddTrainingAssignment.</summary>
public record AddTrainingAssignmentRequest(Guid TrainerEmployeeId);

/// <summary>One system/product the Admin has put this Trainer on the roster for — the only things a Trainer may log training against. See TrainingController.GetMyAssignments.</summary>
public record MyTrainingAssignmentDto(Guid SystemProductId, string SystemProductName, Guid ClientId, string ClientName);

/// <summary>One training session actually conducted and logged — see TrainingRecord.</summary>
public record TrainingRecordDto(
    Guid Id, Guid SystemProductId, string SystemProductName, Guid ClientId, string ClientName,
    Guid TrainerEmployeeId, string TrainerEmployeeName,
    Guid AgreementTypeId, string AgreementTypeName,
    DateOnly TrainingDate, DateTimeOffset? StartDateTime, DateTimeOffset? EndDateTime,
    string Description, string? FileName, DateTimeOffset CreatedAt
);

/// <summary>
/// "Add Training": the Trainer picks the Client's SystemProduct, picks
/// which admin-configured item (e.g. "Attendance" — see AgreementType)
/// this session is for, enters the date and what was taught, and
/// optionally attaches a file (uploaded separately, multipart — see
/// TrainingController.UploadFile). callerEmployeeId (who conducted it) is
/// resolved server-side from the JWT, never taken from this request.
///
/// StartDateTime/EndDateTime are both optional — some items have no real
/// duration worth recording. When a training starts and finishes the
/// same day, give both the same date and let the time-of-day carry the
/// distinction; a training that runs across days simply carries
/// different dates. When EndDateTime is given it must be on/after
/// StartDateTime (see TrainingRecordService.CreateAsync).
/// </summary>
public record CreateTrainingRecordRequest(
    Guid SystemProductId, Guid AgreementTypeId, DateOnly TrainingDate,
    DateTimeOffset? StartDateTime, DateTimeOffset? EndDateTime, string Description
);

/// <summary>Admin backfilling a historical training session on behalf of a chosen Trainer — see ITrainingRecordService.AdminCreateAsync.</summary>
public record AdminCreateTrainingRecordRequest(
    Guid TrainerEmployeeId, Guid SystemProductId, Guid AgreementTypeId, DateOnly TrainingDate,
    DateTimeOffset? StartDateTime, DateTimeOffset? EndDateTime, string Description
);

public record SystemProductDto(
    Guid Id, Guid ClientId, string ReferenceNumber, string Name, string? Description, DateOnly? DeploymentDate,
    TrainingCompletionStatus TrainingCompletionStatus, IReadOnlyList<TrainingAssignmentDto> TrainingAssignments,
    /// <summary>Optional catalog entry this product was created from — see ProductCatalogItem.</summary>
    Guid? CatalogItemId,
    /// <summary>When this client's product is due to expire, shown on the client dashboard. Optional.</summary>
    DateOnly? ExpiryDate,
    /// <summary>Set once a Trainer has submitted — see ISystemProductService.SubmitTrainingAsync. Null until then.</summary>
    DateTimeOffset? TrainingSubmittedAt
);
public record CreateSystemProductRequest(Guid ClientId, string Name, string? Description, DateOnly? DeploymentDate, Guid? CatalogItemId = null, DateOnly? ExpiryDate = null);
public record UpdateSystemProductRequest(string Name, string? Description, DateOnly? DeploymentDate, Guid? CatalogItemId = null, DateOnly? ExpiryDate = null);

/// <summary>Admin-managed catalog entry describing a kind of system/product DAFTECH deploys — see ProductCatalogItem.</summary>
public record ProductCatalogItemDto(Guid Id, string Name, string? Description, bool IsActive);
public record CreateProductCatalogItemRequest(string Name, string? Description);
public record UpdateProductCatalogItemRequest(string Name, string? Description, bool IsActive);

public record AgreementDto(
    Guid Id, Guid SystemProductId, Guid ClientId, string ClientName, string SystemProductName,
    Guid AgreementTypeId, string AgreementTypeName,
    string DocumentNumber, string? ScannedFileUrl, string AgreementPlace,
    DateOnly SignDate, DateOnly ExpiryDate, int SupportWindowMonths, AgreementStatus Status, BillingTier BillingTier,
    string? Details
);

/// <summary>
/// DocumentNumber is system-generated (see ReferenceNumberService), not
/// supplied by the caller. SignDate IS admin-entered here (defaults to
/// today in the UI, but the Admin can set/backdate it) — creating an
/// Agreement no longer forces "today" the way the old Client-level model
/// did. If AgreementTypeId resolves to the Support type, creation is
/// rejected unless the same SystemProduct's TrainingCompletionStatus is
/// already Completed — training must finish first, per-SystemProduct
/// (see AgreementService.CreateAsync). Never overwrites an existing
/// agreement — always inserts a new row, even for the same
/// SystemProduct/AgreementType pair (e.g. a renewal).
/// </summary>
public record CreateAgreementRequest(
    Guid SystemProductId, Guid AgreementTypeId, string? ScannedFileUrl, string AgreementPlace,
    DateOnly SignDate, DateOnly? ExpiryDate, int SupportWindowMonths, BillingTier BillingTier, string? Details
);

public record TimeLogDto(Guid Id, Guid EmployeeId, DateOnly Date, DateTimeOffset? StartTime, DateTimeOffset? FinishTime, double? TotalHours);

public record MaintenanceRecordDto(
    Guid Id, DateOnly Date, string Category, string Description,
    Guid PerformedByEmployeeId, string PerformedByEmployeeName, MaintenanceStatus Status, string? Remarks,
    Guid? ClientId, string? ClientName, Guid? SystemProductId, string? SystemProductName, Guid? TicketId
);

public record CreateMaintenanceRecordRequest(
    string Category, string Description, Guid PerformedByEmployeeId, MaintenanceStatus Status, string? Remarks,
    Guid? ClientId, Guid? SystemProductId, Guid? TicketId
);

public record NotificationDto(Guid Id, NotificationRecipientType RecipientType, string RecipientId, string EventType, string Message, DateTimeOffset DateSent, bool ReadStatus);
