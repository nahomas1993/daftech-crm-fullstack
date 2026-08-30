using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using DaftechCrm.Domain.Entities;
using Microsoft.Extensions.Options;

namespace DaftechCrm.Application.Services;

/// <summary>
/// Single source of truth for "was this ticket resolved on time" and "what
/// was its SLA target" — before this existed, the same
/// WorkingMinutesElapsed(AssignedAt, ResolvedAt) &lt;= target comparison was
/// written out independently in five places across ReportService and
/// TicketReportService, each one free to drift from the others if the rule
/// ever changed (e.g. "on-time means within 48 working hours instead of
/// 24") without every call site being updated in lockstep. Everything here
/// is pure/stateless given a Ticket, so it's injectable as a singleton and
/// trivially unit-testable without a database.
///
/// The rule itself (see TargetFor and IsOnTime):
///   "On time" = working hours (IEthiopianTimeService) from AssignedAt to
///   ResolvedAt are within the ticket's own FROZEN SLA target
///   (Ticket.ExpectedResolutionMinutes, snapshotted at assignment time —
///   see TicketService.SubmitFromClientAsync) if one was recorded,
///   otherwise the global TicketWorkflowOptions.OnTimeResolutionTargetDays
///   fallback. Working hours, not wall-clock, so a ticket assigned Friday
///   and resolved Monday isn't penalized for the weekend/lunch time in
///   between — matches how the resolution timer itself pauses (see
///   TicketService). A ticket missing either AssignedAt or ResolvedAt has
///   no on-time verdict yet (IsOnTime returns false, ResolutionHours
///   returns null) — callers that only want to count tickets that have
///   actually reached Resolved should filter on those two fields first,
///   same as before this was extracted.
///
///   Deliberately reads Ticket.ExpectedResolutionMinutes, NOT
///   t.FailureType.ToTimeSpan() — the latter is the FailureType's CURRENT
///   configured duration, which an Admin can edit at any time. Reading it
///   live here would silently reclassify already-resolved tickets as
///   on-time/late every time a report runs, based on today's settings
///   rather than what applied when the ticket was actually worked. Tickets
///   resolved before this snapshot field existed have
///   ExpectedResolutionMinutes = null and fall back to the global target,
///   same as a ticket with no FailureType at all.
/// </summary>
public class TicketOnTimeCalculator
{
    private readonly IEthiopianTimeService _officeTime;
    private readonly TicketWorkflowOptions _options;

    public TicketOnTimeCalculator(IEthiopianTimeService officeTime, IOptions<TicketWorkflowOptions> options)
    {
        _officeTime = officeTime;
        _options = options.Value;
    }

    /// <summary>The ticket's own SLA target, or the global fallback (OnTimeResolutionTargetDays) if none was snapshotted on it.</summary>
    public TimeSpan TargetFor(Ticket t) =>
        t.ExpectedResolutionMinutes is int mins ? TimeSpan.FromMinutes(mins) : TimeSpan.FromDays(_options.OnTimeResolutionTargetDays);

    /// <summary>
    /// True if the ticket was resolved within its SLA target. False (not
    /// null) if AssignedAt or ResolvedAt is unset — a ticket with no
    /// on-time verdict yet isn't "late", it just isn't decided; callers
    /// that need to distinguish "not yet resolved" from "resolved late"
    /// should check ResolvedAt themselves, same as every call site did
    /// before this was extracted.
    /// </summary>
    public bool IsOnTime(Ticket t) =>
        t.AssignedAt is DateTimeOffset assigned && t.ResolvedAt is DateTimeOffset resolved &&
        _officeTime.WorkingMinutesElapsed(assigned, resolved) <= TargetFor(t).TotalMinutes;

    /// <summary>Working hours (not wall-clock) between AssignedAt and ResolvedAt, in hours — null if either is unset.</summary>
    public double? ResolutionHours(Ticket t) =>
        t.AssignedAt is DateTimeOffset assigned && t.ResolvedAt is DateTimeOffset resolved
            ? _officeTime.WorkingMinutesElapsed(assigned, resolved) / 60.0
            : null;

    /// <summary>
    /// Counts on-time/late/total/rate over a set of tickets in one pass —
    /// the shape every report that breaks this down (overall, per
    /// employee, per month, per failure type) needs. Tickets without both
    /// timestamps are the caller's concern to filter out first (via
    /// ResolvableTickets) since "total" here means "the denominator this
    /// particular breakdown wants", which differs slightly between call
    /// sites (e.g. per-month trend vs. all-time employee stats).
    /// </summary>
    public OnTimeTally Tally(IEnumerable<Ticket> tickets)
    {
        var list = tickets as IReadOnlyCollection<Ticket> ?? tickets.ToList();
        var onTime = list.Count(IsOnTime);
        var total = list.Count;
        var rate = total > 0 ? Math.Round(onTime * 100.0 / total, 1) : (double?)null;
        return new OnTimeTally(onTime, total - onTime, total, rate);
    }

    /// <summary>Tickets that have reached Resolved and can therefore be given an on-time verdict — the standard "denominator" filter used across the Reports/Dashboard on-time breakdowns.</summary>
    public static IEnumerable<Ticket> ResolvableTickets(IEnumerable<Ticket> tickets) =>
        tickets.Where(t => t.AssignedAt != null && t.ResolvedAt != null);
}

/// <summary>On-time/late counts and rate for a set of tickets — see TicketOnTimeCalculator.Tally.</summary>
public record OnTimeTally(int OnTimeCount, int LateCount, int Total, double? OnTimeRatePercent);
