using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Domain.Entities;

/// <summary>
/// A coarser, client-facing-friendly lifecycle grouping of TicketStatus,
/// used purely for reporting/filtering (see Reports module) — not stored,
/// always derived live from Ticket.Status via Ticket.SupportPhase below,
/// so it can never drift out of sync with the ticket's real status.
/// </summary>
public enum SupportPhase { Intake, Diagnosis, Repair, Verification, Closed }

/// <summary>Urgency an Admin or technician can set on a ticket — used by workload-aware Trainer assignment (see TrainerWorkloadService) to weight "high-priority" tickets more heavily than routine ones. Defaults to Medium; does not affect auto-assignment order for technicians (see TicketAssignmentService, which is unaffected by this).</summary>
public enum TicketPriority { Low, Medium, High }

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
    /// Category is the required Frontend/Backend/Database classification. Null for
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
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    /// <summary>
    /// This ticket's status, grouped into a coarser support lifecycle
    /// phase for reporting/filtering. Always derived from Status — never
    /// set or stored independently, so it's impossible for the two to
    /// disagree. See SupportPhase for the mapping rationale.
    /// </summary>
    public SupportPhase SupportPhase => Status switch
    {
        TicketStatus.Submitted or TicketStatus.Forwarded => SupportPhase.Intake,
        TicketStatus.Assigned => SupportPhase.Diagnosis,
        TicketStatus.InProgress => SupportPhase.Repair,
        TicketStatus.Resolved or TicketStatus.AwaitingClientConfirmation => SupportPhase.Verification,
        TicketStatus.Escalated or TicketStatus.Closed => SupportPhase.Closed,
        _ => SupportPhase.Intake,
    };

    // --- Client confirmation / satisfaction ---

    /// <summary>Set when the employee marks the ticket Resolved; starts the client-response clock.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>Deadline after which an unanswered confirmation auto-closes the ticket (ResolvedAt + N days).</summary>
    public DateTimeOffset? ClientConfirmationDeadline { get; set; }

    /// <summary>1-5 stars, set when the client responds. Null if never rated (e.g. auto-closed).</summary>
    /// <summary>1-5 in 0.5 increments (e.g. 3.5) — half stars are allowed, whole stars are not required. SatisfactionScore below is always this value * 20, kept in sync by TicketService.ConfirmAsync.</summary>
    public decimal? SatisfactionStars { get; set; }

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
