using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Application.DTOs;

public record AgreementTrainingDto(
    Guid Id, Guid ClientId, Guid? AgreementId, string? Description, DateOnly? StartDate, DateOnly? EndDate, string? ScanFileName
);

public record AgreementDto(
    Guid Id, Guid ClientId, string DocumentNumber, string? ScannedFileUrl, string AgreementPlace,
    DateOnly SignDate, DateOnly ExpiryDate, int SupportWindowMonths, AgreementStatus Status, BillingTier BillingTier,
    IReadOnlyList<AgreementTrainingDto> Trainings
);

/// <summary>
/// DocumentNumber is system-generated (see ReferenceNumberService), not
/// supplied by the caller. SignDate is likewise not accepted here — the
/// server always sets it to today, since creating an Agreement IS the
/// admin's act of signing it (see AgreementService.CreateAsync). Creation
/// is rejected unless the client already has at least one training with
/// EndDate set — training is mandatory and must finish first.
/// </summary>
public record CreateAgreementRequest(
    Guid ClientId, string? ScannedFileUrl, string AgreementPlace,
    DateOnly? ExpiryDate, int SupportWindowMonths, BillingTier BillingTier
);

/// <summary>
/// Creates or updates one training row for a client. All fields
/// optional — the Admin can fill this in over time as training details
/// firm up. Each training is saved independently of the others (its own
/// Save button in the UI). EndDate stays editable even after being set,
/// so the admin can push it out if training runs long.
/// </summary>
public record SaveAgreementTrainingRequest(
    string? Description, DateOnly? StartDate, DateOnly? EndDate
);

public record TimeLogDto(Guid Id, Guid EmployeeId, DateOnly Date, DateTimeOffset? StartTime, DateTimeOffset? FinishTime, double? TotalHours);

public record MaintenanceRecordDto(
    Guid Id, DateOnly Date, string Category, string Description,
    Guid PerformedByEmployeeId, MaintenanceStatus Status, string? Remarks
);

public record CreateMaintenanceRecordRequest(string Category, string Description, Guid PerformedByEmployeeId, MaintenanceStatus Status, string? Remarks);

public record NotificationDto(Guid Id, NotificationRecipientType RecipientType, string RecipientId, string EventType, string Message, DateTimeOffset DateSent, bool ReadStatus);
