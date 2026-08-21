namespace DaftechCrm.Application.Interfaces;

/// <summary>
/// Single source of truth for DAFTECH's Ethiopian office-hours schedule
/// (Africa/Addis_Ababa, UTC+3 — Ethiopia does not observe DST, so the
/// offset is fixed year-round). Every place that needs to know "are we in
/// office hours right now", "when does assignment next happen", or "how
/// much working time elapsed between these two instants" goes through
/// this service so the schedule is defined in exactly one place.
///
/// Schedule:
///   Mon-Fri: 2:30-11:30 LT, with a 6:30-8:00 LT lunch pause
///   Sat:     2:30-6:00 LT, no lunch pause (half day)
///   Sun:     closed
///
/// All methods take/return DateTimeOffset in UTC (matching how the rest of
/// the app stores timestamps — Ticket.DateSubmitted etc. are UTC) and
/// convert to/from Ethiopian local time internally.
/// </summary>
public interface IEthiopianTimeService
{
    /// <summary>True if the given UTC instant falls within office hours (working, not lunch, not closed) on its Ethiopian local calendar day.</summary>
    bool IsWorkingMoment(DateTimeOffset utcInstant);

    /// <summary>
    /// The next UTC instant at or after the given one where ticket
    /// ASSIGNMENT may happen — i.e. the next working moment. If the given
    /// instant is already a working moment, returns it unchanged. Used to
    /// decide when a queued ticket becomes assignable.
    /// </summary>
    DateTimeOffset NextAssignableMoment(DateTimeOffset utcInstant);

    /// <summary>
    /// Given a starting UTC instant and a duration of WORKING minutes to
    /// add (skipping lunch/off-hours/weekends), returns the UTC instant
    /// that many working minutes later. Used both for ExpectedResolutionBy
    /// (the SLA deadline) and to check whether a Saturday ticket's
    /// expected duration fits before Saturday close.
    /// </summary>
    DateTimeOffset AddWorkingMinutes(DateTimeOffset startUtc, int minutes);

    /// <summary>
    /// Working minutes elapsed between two UTC instants — lunch and
    /// off-hours periods in between don't count. Used to drive the
    /// resolution timer / on-time check without wall-clock time
    /// (including lunch/nights/Sundays) inflating "how long this took".
    /// If toUtc is before fromUtc, returns 0.
    /// </summary>
    int WorkingMinutesElapsed(DateTimeOffset fromUtc, DateTimeOffset toUtc);
}
