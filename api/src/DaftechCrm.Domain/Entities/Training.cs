namespace DaftechCrm.Domain.Entities;

/// <summary>
/// Training status for a SystemProduct as a whole — NOT per training
/// record. There is no per-record review/approval step (see
/// TrainingRecord); Admin looks at however many records have accumulated
/// and, when satisfied, marks the whole thing Completed in one action
/// (see SystemProductService.MarkTrainingCompletedAsync). Completed is
/// the gate for signing a Support agreement on this SAME SystemProduct
/// (see AgreementService.CreateAsync) — but is not a lock: more
/// TrainingRecord rows (e.g. a refresher) can still be added afterward,
/// same as before, without reverting this status.
/// </summary>
public enum TrainingCompletionStatus { NotStarted, InProgress, Completed }

/// <summary>
/// One Trainer/Technician assigned to train a client on a SystemProduct —
/// a roster entry, not a task with its own lifecycle. Created either by
/// AutoAssignTrainersAsync (workload-based) or AddTrainingAssignmentAsync
/// (Admin's manual pick from a dropdown), both capped at
/// Training.MaxTrainersPerSystemProduct (see SystemConfigurationService).
/// An employee on this roster is who's allowed to log a TrainingRecord
/// against this SystemProduct — see TrainingRecordService.
/// </summary>
public class TrainingAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SystemProductId { get; set; }
    public SystemProduct SystemProduct { get; set; } = default!;

    public Guid TrainerEmployeeId { get; set; }
    public Employee TrainerEmployee { get; set; } = default!;

    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// One training session actually conducted, logged by the Trainer who
/// conducted it. Open-ended by design — a SystemProduct can accumulate
/// any number of these over time (see TrainingRecordService.CreateAsync,
/// which only ever inserts, same as Agreement) — there is no single
/// "the" training session to submit/approve/reject; each visit gets its
/// own row with its own agreement item, dates, description, and optional
/// file. Admin reviews the accumulated set of these, informally, then
/// marks the SystemProduct's training Completed as a separate one-click
/// action — no record here is itself marked approved/rejected.
///
/// AgreementTypeId is which named training item this record is for
/// (e.g. "Attendance") — Admin defines the set of names via the same
/// admin-managed AgreementType lookup table used for Support/Training
/// agreements (see AgreementType), so the checklist of item names a
/// Trainer works through is fully configurable without a code change.
/// A SystemProduct's training roster works through one row per
/// AgreementType they need to cover; a Trainer can still log more than
/// one row against the same AgreementType over time (e.g. a refresher).
/// </summary>
public class TrainingRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SystemProductId { get; set; }
    public SystemProduct SystemProduct { get; set; } = default!;

    /// <summary>
    /// Which admin-configured training item this record is for (e.g.
    /// "Attendance") — see AgreementType. Required: every logged session
    /// belongs to exactly one named item.
    /// </summary>
    public Guid AgreementTypeId { get; set; }
    public AgreementType AgreementType { get; set; } = default!;

    /// <summary>Who conducted this specific session — must have been on the SystemProduct's TrainingAssignment roster at the time it was logged (see TrainingRecordService.CreateAsync).</summary>
    public Guid TrainerEmployeeId { get; set; }
    public Employee TrainerEmployee { get; set; } = default!;

    public DateOnly TrainingDate { get; set; }

    /// <summary>
    /// When this session started. Optional — some agreement items (e.g.
    /// a document review) have no real start/end time at all, only the
    /// TrainingDate and a description; the Trainer can leave both this
    /// and EndDateTime blank for those.
    /// </summary>
    public DateTimeOffset? StartDateTime { get; set; }

    /// <summary>
    /// When this session ended. For a training that runs only a couple
    /// of hours and finishes the same day, StartDateTime and
    /// EndDateTime fall on the same calendar date and the time-of-day
    /// portion is what actually distinguishes them — trainings are not
    /// assumed to all span the same length or even the same number of
    /// days. Multi-day trainings simply carry different dates here.
    /// Must be on/after StartDateTime when both are set (see
    /// TrainingRecordService.CreateAsync).
    /// </summary>
    public DateTimeOffset? EndDateTime { get; set; }

    /// <summary>What was taught/conducted in this session — the trainer's own account, required.</summary>
    public string Description { get; set; } = default!;

    /// <summary>Storage key (per IFileStorageService) for an optional supporting file/document. Null if none was attached.</summary>
    public string? FileStorageKey { get; set; }
    public string? FileName { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
