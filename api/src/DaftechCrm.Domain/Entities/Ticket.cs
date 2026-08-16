using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Domain.Entities;

public class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClientId { get; set; }
    public Client Client { get; set; } = default!;

    public Guid AgreementId { get; set; }
    public Agreement Agreement { get; set; } = default!;

    public string Description { get; set; } = default!;
    public TicketCategory Category { get; set; }

    /// <summary>
    /// Admin-defined failure type the client selected on submission (e.g.
    /// "Server Down"), separate from and additional to Category above —
    /// Category and its existing reporting are unchanged. Null for
    /// tickets submitted before this field existed, or if the client
    /// picked nothing. Drives the per-ticket on-time deadline in
    /// ReportService when set; falls back to the global
    /// OnTimeResolutionTargetDays otherwise.
    /// </summary>
    public Guid? FailureTypeId { get; set; }
    public FailureType? FailureType { get; set; }

    public DateTimeOffset DateSubmitted { get; set; } = DateTimeOffset.UtcNow;

    public Guid? ForwardedByEmployeeId { get; set; }
    public Employee? ForwardedByEmployee { get; set; }

    /// <summary>
    /// Set automatically by the assignment engine the moment the ticket is
    /// submitted — the Admin no longer chooses (ItSupport's old manual
    /// forward step is retired). See ITicketAssignmentService.
    /// </summary>
    public Guid? AssignedEmployeeId { get; set; }
    public Employee? AssignedEmployee { get; set; }

    /// <summary>Set the moment auto-assignment picks an employee (see TicketAssignmentService). Basis for the on-time/late resolution report.</summary>
    public DateTimeOffset? AssignedAt { get; set; }

    /// <summary>
    /// Snapshot of FailureType.ToTimeSpan() (in minutes) taken at the
    /// moment this ticket was assigned. Deliberately copied onto the
    /// ticket rather than read live off FailureType at report time — if an
    /// Admin later changes a FailureType's expected duration (e.g. "Network
    /// Failure" from 4 hours to 8), that must not retroactively change the
    /// SLA of tickets already assigned under the old duration. Null if the
    /// ticket has never been assigned, or was assigned before this field
    /// existed and before it has a FailureType.
    /// </summary>
    public int? ExpectedResolutionMinutes { get; set; }

    /// <summary>AssignedAt + ExpectedResolutionMinutes, computed once at assignment time from the snapshot above. Null under the same conditions as ExpectedResolutionMinutes.</summary>
    public DateTimeOffset? ExpectedResolutionBy { get; set; }

    public bool Chargeable { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Submitted;

    // --- Client confirmation / satisfaction ---

    /// <summary>Set when the employee marks the ticket Resolved; starts the client-response clock.</summary
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>Deadline after which an unanswered confirmation auto-closes the ticket (ResolvedAt + N days).</summary>
    public DateTimeOffset? ClientConfirmationDeadline { get; set; }

    /// <summary>1-5 stars, set when the client responds. Null if never rated (e.g. auto-closed).</summary>
    public int? SatisfactionStars { get; set; }

    /// <summary>Stars converted to a 0-100 score (stars * 20). Null if never rated.</summary>
    public int? SatisfactionScore { get; set; }

    public ClosureReason? ClosureReason { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>
    /// Storage key (per IFileStorageService) for one optional attachment —
    /// typically a screenshot of the error/console the client is
    /// reporting. Uploaded as a separate step after the ticket exists (see
    /// TicketsController.UploadAttachment), not at submission time. Null
    /// if nothing was ever uploaded.
    /// </summary>
    public string? AttachmentStorageKey { get; set; }

    /// <summary>Original filename of the attachment, shown in the UI instead of the opaque storage key. Null alongside AttachmentStorageKey.</summary>
    public string? AttachmentFileName { get; set; }

    /// <summary>
    /// Storage key (per IFileStorageService) for an optional voice-note
    /// recording — the client can record audio describing the issue in
    /// the browser and attach it in place of (or alongside) written
    /// description text, giving the assigned technician extra context on
    /// the error. Recorded and uploaded before/at submission time (see
    /// TicketsController.UploadVoiceNote / SubmitTicketRequest), unlike
    /// the screenshot attachment above which is added afterward. Null if
    /// no recording was made.
    /// </summary>
    public string? VoiceNoteStorageKey { get; set; }

    /// <summary>Original filename of the voice note recording (e.g. "voice-note.webm"). Null alongside VoiceNoteStorageKey.</summary>
    public string? VoiceNoteFileName { get; set; }

    public ICollection<TicketAuditEntry> AuditTrail { get; set; } = new List<TicketAuditEntry>();
}

public class TicketAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public Ticket Ticket { get; set; } = default!;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string Actor { get; set; } = default!;
    public string Action { get; set; } = default!;
}
