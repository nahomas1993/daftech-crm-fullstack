using DaftechCrm.Domain.Entities;

namespace DaftechCrm.Application.DTOs;

/// <summary>
/// One eligible Trainer's current workload, shown to Admin when assigning
/// a Training agreement's TrainingSession.TrainerEmployeeId — see
/// ITrainerWorkloadService.GetEligibleTrainersAsync. Every count is a
/// snapshot as of the call (not cached), since Admin needs the up-to-date
/// picture at the moment of assignment.
/// </summary>
public record TrainerWorkloadDto(
    Guid EmployeeId,
    string EmployeeName,
    /// <summary>Tickets currently assigned and not yet resolved/closed (Assigned, InProgress, AwaitingClientConfirmation).</summary>
    int ActiveTicketCount,
    /// <summary>Tickets received but not yet assigned/started (Submitted, Forwarded) — see the product decision that "pending" means queued, not "assigned but not started".</summary>
    int PendingTicketCount,
    /// <summary>Of ActiveTicketCount + PendingTicketCount, how many are TicketPriority.High.</summary>
    int HighPriorityTicketCount,
    /// <summary>Tickets not yet resolved whose ExpectedResolutionBy has already passed.</summary>
    int OverdueTicketCount,
    /// <summary>Training agreements currently assigned to this Trainer whose TrainingSession hasn't reached Completed yet (NotStarted/InProgress/FollowUpRequired).</summary>
    int ActiveTrainingAssignmentCount,
    /// <summary>
    /// A single weighted score combining every dimension above, used only
    /// to rank/recommend and to compute the relative-to-peers "excessive
    /// workload" warning (see ITrainerWorkloadService for the weighting
    /// and warning rationale) — not shown as a standalone number to Admin,
    /// since the individual counts above are what's actually meaningful to
    /// a person deciding who to assign.
    /// </summary>
    double WorkloadScore,
    /// <summary>True if this Trainer's WorkloadScore is significantly above the average of the other eligible Trainers being compared in the same call — see ITrainerWorkloadService for the threshold.</summary>
    bool IsExcessiveWorkload
);

/// <summary>
/// The full picture Admin sees when assigning a Trainer to a Training
/// agreement: every eligible Trainer's workload, plus which one the
/// system recommends. Admin can still pick any of them (including one
/// flagged IsExcessiveWorkload) — this is a recommendation, never an
/// enforced restriction.
/// </summary>
public record TrainerAssignmentRecommendationDto(
    IReadOnlyList<TrainerWorkloadDto> EligibleTrainers,
    Guid? RecommendedTrainerEmployeeId
);
