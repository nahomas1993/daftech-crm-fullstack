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
/// own row with its own date, description, and optional file. Admin
/// reviews the accumulated set of these, informally, then marks the
/// SystemProduct's training Completed as a separate one-click action —
/// no record here is itself marked approved/rejected.
/// </summary>
public class TrainingRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SystemProductId { get; set; }
    public SystemProduct SystemProduct { get; set; } = default!;

    /// <summary>Who conducted this specific session — must have been on the SystemProduct's TrainingAssignment roster at the time it was logged (see TrainingRecordService.CreateAsync).</summary>
    public Guid TrainerEmployeeId { get; set; }
    public Employee TrainerEmployee { get; set; } = default!;

    public DateOnly TrainingDate { get; set; }

    /// <summary>What was taught/conducted in this session — the trainer's own account, required.</summary>
    public string Description { get; set; } = default!;

    /// <summary>Storage key (per IFileStorageService) for an optional supporting file/document. Null if none was attached.</summary>
    public string? FileStorageKey { get; set; }
    public string? FileName { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
