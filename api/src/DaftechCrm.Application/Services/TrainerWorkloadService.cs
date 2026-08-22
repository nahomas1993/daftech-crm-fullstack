using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

/// <summary>
/// See ITrainerWorkloadService. Weighting rationale for WorkloadScore:
/// active tickets count once each, pending tickets count for half (they
/// aren't being worked yet, but will need to be soon), a high-priority
/// ticket adds an extra point on top of its base count (so a High-priority
/// active ticket is worth 2, not 1), an overdue ticket adds an extra 1.5
/// points on top of its base count (overdue work is the most urgent
/// signal), and each active training assignment counts as 1.5 — training
/// sessions are typically a bigger, less interruptible time commitment
/// than a single ticket. These weights are a deliberately simple,
/// transparent heuristic (not a tuned model) — Admin sees the underlying
/// counts too, not just the score, specifically so a human can sanity
/// check or override the ranking.
/// </summary>
public class TrainerWorkloadService : ITrainerWorkloadService
{
    private const double PendingWeight = 0.5;
    private const double HighPriorityBonus = 1.0;
    private const double OverdueBonus = 1.5;
    private const double ActiveTrainingWeight = 1.5;

    /// <summary>
    /// How far above the average of the OTHER eligible Trainers' scores
    /// counts as "excessive" — 50% above average, a deliberately simple
    /// relative threshold per the product decision that this should be
    /// relative to peers, not a fixed configurable number. Only applied
    /// when there are at least 2 eligible Trainers to compare against;
    /// with 0 or 1, "relative to peers" has no meaning, so nothing is
    /// flagged.
    /// </summary>
    private const double ExcessiveWorkloadMultiplier = 1.5;

    private static readonly TicketStatus[] ActiveStatuses = { TicketStatus.Assigned, TicketStatus.InProgress, TicketStatus.AwaitingClientConfirmation };
    private static readonly TicketStatus[] PendingStatuses = { TicketStatus.Submitted, TicketStatus.Forwarded };
    private static readonly TrainingCompletionStatus[] ActiveTrainingStatuses = { TrainingCompletionStatus.NotStarted, TrainingCompletionStatus.InProgress, TrainingCompletionStatus.FollowUpRequired };

    /// <summary>Which TrainingAssignment.Status values still represent live/outstanding work for a Trainer — everything except Approved, which is finished.</summary>
    private static readonly TrainingAssignmentStatus[] ActiveAssignmentStatuses =
        { TrainingAssignmentStatus.Assigned, TrainingAssignmentStatus.Submitted, TrainingAssignmentStatus.RejectedNeedsRework };

    private readonly IAppDbContext _db;
    public TrainerWorkloadService(IAppDbContext db) => _db = db;

    public async Task<TrainerAssignmentRecommendationDto> GetEligibleTrainersAsync(CancellationToken ct = default)
    {
        var results = await RankTrainersAsync(ct);
        if (results.Count == 0)
            return new TrainerAssignmentRecommendationDto(Array.Empty<TrainerWorkloadDto>(), null);

        var recommended = results.FirstOrDefault(r => !r.IsExcessiveWorkload) ?? results.First();
        return new TrainerAssignmentRecommendationDto(results, recommended.EmployeeId);
    }

    public async Task<IReadOnlyList<Guid>> SelectTrainersForAssignmentAsync(int count, CancellationToken ct = default)
    {
        if (count <= 0) return Array.Empty<Guid>();

        var results = await RankTrainersAsync(ct);
        // Already ordered by WorkloadScore ascending (least-loaded first) —
        // take the front of that same ranking rather than special-casing
        // IsExcessiveWorkload here, so auto-assignment and the Admin's
        // manual recommendation always agree on who's "next up".
        return results.Take(count).Select(r => r.EmployeeId).ToList();
    }

    /// <summary>Every eligible Trainer's workload snapshot, ranked by WorkloadScore ascending (least-loaded first) with IsExcessiveWorkload computed relative to the whole set — the shared computation behind both GetEligibleTrainersAsync and SelectTrainersForAssignmentAsync so the two never disagree on ranking.</summary>
    private async Task<IReadOnlyList<TrainerWorkloadDto>> RankTrainersAsync(CancellationToken ct)
    {
        var trainers = await _db.Employees
            .AsNoTracking()
            .Where(e => !e.IsDeleted && e.AccountStatus == EmployeeAccountStatus.Active)
            .ToListAsync(ct);

        trainers = trainers.Where(e => e.Roles.Contains(EmployeeRole.Trainer)).ToList();

        if (trainers.Count == 0)
            return Array.Empty<TrainerWorkloadDto>();

        var trainerIds = trainers.Select(t => t.Id).ToList();
        var now = DateTimeOffset.UtcNow;

        // One query for every relevant ticket across all candidate
        // trainers, then group in memory — cheaper than N separate
        // per-trainer queries, and keeps every trainer's snapshot
        // consistent with the exact same "now".
        var relevantTickets = await _db.Tickets
            .AsNoTracking()
            .Where(t => t.AssignedEmployeeId != null && trainerIds.Contains(t.AssignedEmployeeId.Value) &&
                        (ActiveStatuses.Contains(t.Status) || PendingStatuses.Contains(t.Status)))
            .Select(t => new { t.AssignedEmployeeId, t.Status, t.Priority, t.ExpectedResolutionBy, t.ResolvedAt })
            .ToListAsync(ct);

        var activeTrainingCounts = await _db.TrainingAssignments
            .AsNoTracking()
            .Where(ta => trainerIds.Contains(ta.TrainerEmployeeId) &&
                         ActiveAssignmentStatuses.Contains(ta.Status) &&
                         ActiveTrainingStatuses.Contains(ta.TrainingSession.CompletionStatus))
            .GroupBy(ta => ta.TrainerEmployeeId)
            .Select(g => new { TrainerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TrainerId, x => x.Count, ct);

        var provisional = trainers.Select(trainer =>
        {
            var ticketsForTrainer = relevantTickets.Where(t => t.AssignedEmployeeId == trainer.Id).ToList();
            var active = ticketsForTrainer.Count(t => ActiveStatuses.Contains(t.Status));
            var pending = ticketsForTrainer.Count(t => PendingStatuses.Contains(t.Status));
            var highPriority = ticketsForTrainer.Count(t => t.Priority == TicketPriority.High);
            var overdue = ticketsForTrainer.Count(t => t.ResolvedAt == null && t.ExpectedResolutionBy != null && t.ExpectedResolutionBy < now);
            var activeTraining = activeTrainingCounts.GetValueOrDefault(trainer.Id, 0);

            var score = active + (pending * PendingWeight) + (highPriority * HighPriorityBonus) +
                        (overdue * OverdueBonus) + (activeTraining * ActiveTrainingWeight);

            return (trainer.Id, trainer.FullName, active, pending, highPriority, overdue, activeTraining, score);
        }).ToList();

        // "Excessive" is relative to the OTHER candidates, not this one's
        // own score — so for each trainer, average over everyone else.
        return provisional.Select(p =>
        {
            var others = provisional.Where(o => o.Id != p.Id).ToList();
            var isExcessive = others.Count > 0 && p.score > others.Average(o => o.score) * ExcessiveWorkloadMultiplier;

            return new TrainerWorkloadDto(
                p.Id, p.FullName, p.active, p.pending, p.highPriority, p.overdue, p.activeTraining, p.score, isExcessive
            );
        })
        .OrderBy(r => r.WorkloadScore)
        .ToList();
    }
}
