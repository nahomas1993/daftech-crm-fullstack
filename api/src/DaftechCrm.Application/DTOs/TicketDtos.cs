using DaftechCrm.Domain.Enums;
using DaftechCrm.Domain.Entities;

namespace DaftechCrm.Application.DTOs;

public record TicketDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    Guid AgreementId,
    string Description,
    TicketCategory Category,
    Guid? FailureTypeId,
    string? FailureTypeName,
    DateTimeOffset DateSubmitted,
    Guid? ForwardedByEmployeeId,
    Guid? AssignedEmployeeId,
    string? AssignedEmployeeName,
    DateTimeOffset? AssignedAt,
    /// <summary>AssignedAt + the ticket's FailureType duration, or null if no FailureType was chosen (falls back to the global on-time target in reporting — see ReportService).</summary>
    DateTimeOffset? ExpectedResolutionBy,
    bool Chargeable,
    TicketStatus Status,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClientConfirmationDeadline,
    int? SatisfactionStars,
    int? SatisfactionScore,
    ClosureReason? ClosureReason,
    /// <summary>Original filename of the optional attachment (screenshot/document), or null if none was uploaded. Fetch/upload via TicketsController's /attachment endpoints — this DTO only carries the display name, not the file itself.</summary>
    string? AttachmentFileName,
    /// <summary>Original filename of the optional voice-note recording, or null if none was recorded. Fetch/upload via TicketsController's /voice-note endpoints — this DTO only carries the display name, not the audio itself.</summary>
    string? VoiceNoteFileName,
    IReadOnlyList<TicketAuditEntryDto> AuditTrail
);

public record TicketAuditEntryDto(DateTimeOffset Timestamp, string Actor, string Action);

/// <summary>Returned by POST /api/tickets/voice-note — the client passes both values back in SubmitTicketRequest to attach the recording.</summary>
public record VoiceNoteUploadResult(string StorageKey, string FileName);

/// <summary>
/// VoiceNoteStorageKey is optional and, unlike the screenshot attachment,
/// is expected to already exist by submission time — the client records
/// in the browser and uploads it via POST /api/tickets/voice-note (no
/// ticket exists yet at that point), then passes the returned storage key
/// here so it's attached to the ticket atomically on creation.
/// </summary>
public record SubmitTicketRequest(Guid ClientId, Guid AgreementId, string Description, TicketCategory Category, Guid? FailureTypeId, string? VoiceNoteStorageKey = null, string? VoiceNoteFileName = null);

public record UpdateTicketStatusRequest(TicketStatus Status, string ActorName);

/// <summary>
/// Client's response to the "did this actually get fixed?" confirmation step.
/// Stars are 1-5; the service converts to a 0-100 score (stars * 20) and
/// applies the 90/100 (4.5-star) escalation threshold.
/// </summary>
/// <summary>
/// Client's response to the "did this actually get fixed?" confirmation step.
/// SRS v2.0 §4.5.1: IsFixed is answered first — if false, the ticket
/// reopens to the assigned employee and SatisfactionStars is ignored (not
/// required). If true, SatisfactionStars (1-5) is required and the service
/// converts it to a 0-100 score, applying the 90/100 escalation threshold.
/// </summary>
public record ClientConfirmationRequest(bool IsFixed, int? SatisfactionStars);
