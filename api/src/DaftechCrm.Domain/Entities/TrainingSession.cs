namespace DaftechCrm.Domain.Entities;

public enum TrainingCompletionStatus { NotStarted, InProgress, Completed, FollowUpRequired }

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
/// </summary>
public class TrainingSession
{
    /// <summary>Same value as the owning Agreement's Id (one-to-one, not a separate identity).</summary>
    public Guid AgreementId { get; set; }
    public Agreement Agreement { get; set; } = default!;

    /// <summary>The Employee (with the Trainer responsibility) delivering this training. Null until an Admin assigns one.</summary>
    public Guid? TrainerEmployeeId { get; set; }
    public Employee? TrainerEmployee { get; set; }

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
