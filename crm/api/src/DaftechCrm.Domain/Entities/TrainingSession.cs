namespace DaftechCrm.Domain.Entities;

public enum TrainingCompletionStatus { NotStarted, InProgress, Completed, FollowUpRequired }

/// <summary>
/// Lifecycle of one Trainer's individual assignment within a
/// TrainingSession — separate from TrainingSession.CompletionStatus,
/// which reflects the session as a whole. Assigned is the state from the
/// moment auto-assignment (or an Admin) attaches the trainer; Submitted
/// once the trainer uploads their work and description and sends it for
/// review; Approved/RejectedNeedsRework are the Admin's review decision.
/// A rejected assignment goes back to Assigned so the trainer can revise
/// and resubmit — it is not a dead end.
/// </summary>
public enum TrainingAssignmentStatus { Assigned, Submitted, Approved, RejectedNeedsRework }

/// <summary>
/// One Trainer's participation in a TrainingSession. A session can have
/// several of these at once — see Training.TrainersPerSession in
/// SystemConfigurationService, which controls how many are auto-assigned
/// (by TrainerWorkloadService) the moment a Training agreement is created.
/// An Admin can still add/remove individual assignments by hand
/// afterward; auto-assignment only decides the initial set.
///
/// The session's own EndDate/CompletionStatus (still the gate for signing
/// a Support agreement — see AgreementService.CreateAsync) is only set to
/// Completed once every TrainingAssignment on the session is Approved;
/// see AgreementService.ApproveTrainingAssignmentAsync.
/// </summary>
public class TrainingAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TrainingSessionId { get; set; }
    public TrainingSession TrainingSession { get; set; } = default!;

    public Guid TrainerEmployeeId { get; set; }
    public Employee TrainerEmployee { get; set; } = default!;

    /// <summary>Set the moment this assignment is created (auto-assignment or a manual Admin add).</summary>
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>The trainer's own free-text account of the work they completed. Filled in before submitting for review.</summary>
    public string? WorkDescription { get; set; }

    /// <summary>Storage key (per IFileStorageService) for a file the trainer uploaded as evidence of the completed work (materials, notes, photos, etc). Null until uploaded. Distinct from TrainingSession.ScanStorageKey, which is the client sign-in sheet.</summary>
    public string? FileStorageKey { get; set; }
    public string? FileName { get; set; }

    public TrainingAssignmentStatus Status { get; set; } = TrainingAssignmentStatus.Assigned;

    /// <summary>Set when the trainer submits WorkDescription/file for Admin review.</summary>
    public DateTimeOffset? SubmittedAt { get; set; }

    /// <summary>Full name of the Admin who last reviewed (approved or rejected) this assignment, for the audit trail.</summary>
    public string? ReviewedByName { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>Admin's notes on the review decision — required context when rejecting, optional when approving.</summary>
    public string? ReviewNotes { get; set; }
}

/// <summary>
/// The full training workflow record for one Training-type Agreement.
/// One-to-one with its Agreement (AgreementId is the primary/foreign key)
/// — a training session only exists because a Training agreement exists
/// for some Client → SystemProduct, and is reachable from all three (see
/// AgreementService.GetTrainingSessionAsync and the Client/SystemProduct/
/// Agreement detail pages, which all surface it).
///
/// Renamed/restructured from the earlier AgreementTraining (which
/// attached to Client directly, before an agreement could exist, as a
/// free-text pre-agreement gate) now that Training is itself a first-class
/// Agreement type under SystemProduct — see migration
/// 20260819000000_AddSystemProductAndAgreementType for how old
/// AgreementTraining rows become TrainingSession rows on new Training
/// agreements.
///
/// Holds a collection of TrainingAssignment rather than a single trainer
/// field — see TrainerAssignments and Training.TrainersPerSession.
/// </summary>
public class TrainingSession
{
    /// <summary>Same value as the owning Agreement's Id (one-to-one, not a separate identity).</summary>
    public Guid AgreementId { get; set; }
    public Agreement Agreement { get; set; } = default!;

    /// <summary>Every Trainer assigned to this session, each tracked through their own submit/review lifecycle. See TrainingAssignment.</summary>
    public ICollection<TrainingAssignment> TrainerAssignments { get; set; } = new List<TrainingAssignment>();

    public DateOnly? StartDate { get; set; }

    /// <summary>Set once training finishes — this is what "training complete" means for the training-before-support gate (see AgreementService.CreateAsync). Stays editable afterward (e.g. to push it out if training runs long).</summary>
    public DateOnly? EndDate { get; set; }

    public string? Location { get; set; }

    /// <summary>Free-text list of expected/invited participants (names/roles), entered by the Admin when scheduling.</summary>
    public string? Participants { get; set; }

    /// <summary>Free-text record of who actually attended, filled in by the trainer after the session.</summary>
    public string? Attendance { get; set; }

    /// <summary>What was covered in the session.</summary>
    public string? TopicsCovered { get; set; }

    /// <summary>Questions or issues raised by the client's staff during training.</summary>
    public string? IssuesOrQuestions { get; set; }

    /// <summary>The trainer's own notes/comments on how the session went.</summary>
    public string? TrainerComments { get; set; }

    /// <summary>The client-side representative's confirmation that training happened as described, plus any comments they add.</summary>
    public string? ClientRepresentativeConfirmation { get; set; }
    public string? ClientRepresentativeComments { get; set; }

    public TrainingCompletionStatus CompletionStatus { get; set; } = TrainingCompletionStatus.NotStarted;

    /// <summary>True if this training needs a follow-up session (e.g. a topic needs to be revisited). FollowUpNotes explains why/what.</summary>
    public bool FollowUpRequired { get; set; }
    public string? FollowUpNotes { get; set; }

    /// <summary>Storage key (per IFileStorageService) for the scanned training document/sign-in sheet. Null until uploaded.</summary>
    public string? ScanStorageKey { get; set; }
    public string? ScanFileName { get; set; }
}
