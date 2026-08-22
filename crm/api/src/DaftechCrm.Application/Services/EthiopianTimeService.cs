using DaftechCrm.Application.Interfaces;

namespace DaftechCrm.Application.Services;

/// <summary>
/// See IEthiopianTimeService for the schedule and the rationale for
/// centralizing it here. Ethiopia does not observe daylight saving time,
/// so Africa/Addis_Ababa is a fixed UTC+3 offset year-round — this uses a
/// plain fixed TimeSpan rather than TimeZoneInfo/ICU tzdata lookups, which
/// keeps this correct even in a minimal container image that might not
/// ship a full IANA tzdata set.
/// </summary>
public class EthiopianTimeService : IEthiopianTimeService
{
    private static readonly TimeSpan AddisOffset = TimeSpan.FromHours(3);

    private static readonly TimeSpan OfficeStart = new(2, 30, 0);
    private static readonly TimeSpan OfficeEnd = new(11, 30, 0);
    private static readonly TimeSpan LunchStart = new(6, 30, 0);
    private static readonly TimeSpan LunchEnd = new(8, 0, 0);
    private static readonly TimeSpan SaturdayEnd = new(6, 0, 0);

    private DateTime ToLocal(DateTimeOffset utc) => utc.ToOffset(AddisOffset).DateTime;
    private DateTimeOffset ToUtc(DateTime local) => new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), AddisOffset).ToUniversalTime();

    /// <summary>The office end-of-day time-of-day for a given local calendar day — Saturday closes at 6:00, Mon-Fri at 11:30, Sunday has none (see IsWorkingDay).</summary>
    private static TimeSpan? DayEnd(DayOfWeek day) => day switch
    {
        DayOfWeek.Sunday => null,
        DayOfWeek.Saturday => SaturdayEnd,
        _ => OfficeEnd,
    };

    private static bool IsWorkingDay(DayOfWeek day) => day != DayOfWeek.Sunday;

    private static bool HasLunch(DayOfWeek day) => day != DayOfWeek.Saturday && day != DayOfWeek.Sunday;

    public bool IsWorkingMoment(DateTimeOffset utcInstant)
    {
        var local = ToLocal(utcInstant);
        var dayEnd = DayEnd(local.DayOfWeek);
        if (dayEnd is null) return false;

        var timeOfDay = local.TimeOfDay;
        if (timeOfDay < OfficeStart || timeOfDay >= dayEnd.Value) return false;

        if (HasLunch(local.DayOfWeek) && timeOfDay >= LunchStart && timeOfDay < LunchEnd)
            return false;

        return true;
    }

    public DateTimeOffset NextAssignableMoment(DateTimeOffset utcInstant)
    {
        var local = ToLocal(utcInstant);

        // Walk forward day by day (bounded — a week covers every case,
        // since Sunday is the only fully-closed day and Mon always opens)
        // until we land on a working day, then clamp the time-of-day into
        // that day's working window.
        for (var dayOffset = 0; dayOffset < 8; dayOffset++)
        {
            var candidateDate = local.Date.AddDays(dayOffset);
            var dayEnd = DayEnd(candidateDate.DayOfWeek);
            if (dayEnd is null) continue; // Sunday — skip entirely.

            var dayStart = candidateDate + OfficeStart;
            var dayClose = candidateDate + dayEnd.Value;

            // The moment we're testing "today" (dayOffset == 0) may already
            // be mid-way through the day — for later days we always start
            // from that day's opening time.
            var earliestOnThisDay = dayOffset == 0 ? local : dayStart;

            if (earliestOnThisDay < dayStart)
                return ToUtc(dayStart);

            if (HasLunch(candidateDate.DayOfWeek))
            {
                var lunchStart = candidateDate + LunchStart;
                var lunchEnd = candidateDate + LunchEnd;
                if (earliestOnThisDay >= lunchStart && earliestOnThisDay < lunchEnd)
                    return ToUtc(lunchEnd);
            }

            if (earliestOnThisDay < dayClose)
                return ToUtc(earliestOnThisDay);

            // On or after this day's close — fall through to the next day.
        }

        // Unreachable in practice (8 days always contains a working day),
        // but keeps the method total rather than throwing.
        return utcInstant;
    }

    public DateTimeOffset AddWorkingMinutes(DateTimeOffset startUtc, int minutes)
    {
        if (minutes <= 0) return startUtc;

        // Start from the next assignable moment — working time can only be
        // consumed starting from a working moment.
        var cursor = ToLocal(NextAssignableMoment(startUtc));
        var remaining = minutes;

        for (var guard = 0; guard < 10_000; guard++) // guard: ~a year of Sundays-only would never realistically be hit
        {
            var dayEnd = DayEnd(cursor.DayOfWeek);
            if (dayEnd is null)
            {
                cursor = cursor.Date.AddDays(1) + OfficeStart;
                continue;
            }

            var dayClose = cursor.Date + dayEnd.Value;

            // The next "pause point" today — start of lunch, if lunch is
            // ahead of the cursor and this day has one — otherwise the
            // day's close.
            DateTime segmentEnd = dayClose;
            if (HasLunch(cursor.DayOfWeek))
            {
                var lunchStart = cursor.Date + LunchStart;
                if (cursor < lunchStart)
                    segmentEnd = lunchStart;
            }

            var availableMinutesThisSegment = (int)(segmentEnd - cursor).TotalMinutes;

            if (remaining <= availableMinutesThisSegment)
                return ToUtc(cursor.AddMinutes(remaining));

            remaining -= availableMinutesThisSegment;

            // Jump to the next working moment after this segment ends —
            // NextAssignableMoment handles "segmentEnd is lunch start" (→
            // resumes at lunch end) and "segmentEnd is day close" (→
            // resumes next working day) identically.
            cursor = ToLocal(NextAssignableMoment(ToUtc(segmentEnd)));
        }

        // Guard tripped — should be unreachable; fail safe by returning
        // the wall-clock addition rather than looping forever.
        return startUtc.AddMinutes(minutes);
    }

    public int WorkingMinutesElapsed(DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        if (toUtc <= fromUtc) return 0;

        var cursor = ToLocal(NextAssignableMoment(fromUtc));
        var endLocal = ToLocal(toUtc);
        var totalMinutes = 0;

        for (var guard = 0; guard < 10_000; guard++)
        {
            if (cursor >= endLocal) break;

            var dayEnd = DayEnd(cursor.DayOfWeek);
            if (dayEnd is null)
            {
                cursor = cursor.Date.AddDays(1) + OfficeStart;
                continue;
            }

            var dayClose = cursor.Date + dayEnd.Value;
            DateTime segmentEnd = dayClose;
            if (HasLunch(cursor.DayOfWeek))
            {
                var lunchStart = cursor.Date + LunchStart;
                if (cursor < lunchStart)
                    segmentEnd = lunchStart;
            }

            var effectiveEnd = segmentEnd < endLocal ? segmentEnd : endLocal;
            if (effectiveEnd > cursor)
                totalMinutes += (int)(effectiveEnd - cursor).TotalMinutes;

            if (segmentEnd >= endLocal) break;

            cursor = ToLocal(NextAssignableMoment(ToUtc(segmentEnd)));
        }

        return totalMinutes;
    }
}
