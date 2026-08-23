using DaftechCrm.Application.Services;
using FluentAssertions;
using Xunit;

namespace DaftechCrm.Tests.Domain;

/// <summary>
/// Pins down the DAFTECH office schedule (Africa/Addis_Ababa, UTC+3, no DST):
///   Mon-Fri 8:30 AM - 5:30 PM, lunch 12:30 PM - 2:00 PM
///   Sat     8:30 AM - 12:30 PM (half day, no lunch)
///   Sun     closed
/// These previously regressed because the schedule constants were written in
/// Ethiopian clock hours (2:30/11:30/6:30) while the service compares them
/// against Gregorian UTC+3 wall-clock time, which put the office at
/// 2:30 AM - 11:30 AM.
/// </summary>
public class EthiopianTimeServiceTests
{
    private readonly EthiopianTimeService _svc = new();

    /// <summary>Builds a UTC instant from an Ethiopian local (UTC+3) wall-clock date/time.</summary>
    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute) =>
        new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.FromHours(3)).ToUniversalTime();

    // 2026-08-24 is a Monday; 2026-08-29 a Saturday; 2026-08-30 a Sunday.

    [Theory]
    [InlineData(24, 8, 29, false)] // Monday 08:29 - one minute before opening
    [InlineData(24, 8, 30, true)]  // Monday 08:30 - opening
    [InlineData(24, 12, 29, true)] // just before lunch
    [InlineData(24, 12, 30, false)] // lunch starts
    [InlineData(24, 13, 59, false)] // still lunch
    [InlineData(24, 14, 0, true)]   // back from lunch
    [InlineData(24, 17, 29, true)]  // last working minute
    [InlineData(24, 17, 30, false)] // closed
    [InlineData(29, 8, 30, true)]   // Saturday opening
    [InlineData(29, 12, 29, true)]  // Saturday last working minute
    [InlineData(29, 12, 30, false)] // Saturday closed (half day)
    [InlineData(29, 13, 0, false)]  // Saturday afternoon - closed, no lunch concept
    [InlineData(30, 10, 0, false)]  // Sunday - always closed
    public void IsWorkingMoment_matches_the_published_schedule(int day, int hour, int minute, bool expected) =>
        _svc.IsWorkingMoment(Local(2026, 8, day, hour, minute)).Should().Be(expected);

    [Fact]
    public void Before_opening_waits_for_0830()
    {
        _svc.NextAssignableMoment(Local(2026, 8, 24, 6, 0))
            .Should().Be(Local(2026, 8, 24, 8, 30));
    }

    [Fact]
    public void During_lunch_waits_for_1400()
    {
        _svc.NextAssignableMoment(Local(2026, 8, 24, 13, 0))
            .Should().Be(Local(2026, 8, 24, 14, 0));
    }

    [Fact]
    public void After_close_rolls_to_next_morning()
    {
        _svc.NextAssignableMoment(Local(2026, 8, 24, 18, 0))
            .Should().Be(Local(2026, 8, 25, 8, 30));
    }

    [Fact]
    public void Saturday_afternoon_and_sunday_roll_to_monday_0830()
    {
        _svc.NextAssignableMoment(Local(2026, 8, 29, 13, 0))
            .Should().Be(Local(2026, 8, 31, 8, 30));
        _svc.NextAssignableMoment(Local(2026, 8, 30, 9, 0))
            .Should().Be(Local(2026, 8, 31, 8, 30));
    }

    [Fact]
    public void AddWorkingMinutes_skips_lunch()
    {
        // 12:00 + 60 working minutes -> 30 min to lunch, remaining 30 after 14:00.
        _svc.AddWorkingMinutes(Local(2026, 8, 24, 12, 0), 60)
            .Should().Be(Local(2026, 8, 24, 14, 30));
    }

    [Fact]
    public void AddWorkingMinutes_rolls_over_the_close_of_business()
    {
        // 17:00 Monday + 60 -> 30 min today, remaining 30 from Tuesday 08:30.
        _svc.AddWorkingMinutes(Local(2026, 8, 24, 17, 0), 60)
            .Should().Be(Local(2026, 8, 25, 9, 0));
    }

    [Fact]
    public void Saturday_work_that_does_not_fit_lands_on_monday()
    {
        // Saturday 12:00 + 60 working minutes: only 30 min remain before the
        // 12:30 half-day close, so the rest is served Monday morning. This is
        // exactly the check TicketService.FitsBeforeCloseIfSaturday relies on
        // to queue non-fitting Saturday tickets for Monday.
        _svc.AddWorkingMinutes(Local(2026, 8, 29, 12, 0), 60)
            .Should().Be(Local(2026, 8, 31, 9, 0));
    }

    [Fact]
    public void Saturday_work_that_fits_stays_on_saturday()
    {
        _svc.AddWorkingMinutes(Local(2026, 8, 29, 11, 0), 60)
            .Should().Be(Local(2026, 8, 29, 12, 0));
    }

    [Fact]
    public void A_full_weekday_is_450_working_minutes()
    {
        // 8:30-12:30 (240) + 14:00-17:30 (210) = 450.
        _svc.WorkingMinutesElapsed(Local(2026, 8, 24, 0, 0), Local(2026, 8, 25, 0, 0))
            .Should().Be(450);
    }

    [Fact]
    public void A_saturday_is_240_working_minutes_and_sunday_is_zero()
    {
        _svc.WorkingMinutesElapsed(Local(2026, 8, 29, 0, 0), Local(2026, 8, 30, 0, 0)).Should().Be(240);
        _svc.WorkingMinutesElapsed(Local(2026, 8, 30, 0, 0), Local(2026, 8, 31, 0, 0)).Should().Be(0);
    }

    [Fact]
    public void Overnight_and_lunch_gaps_do_not_count_as_elapsed_work()
    {
        // Monday 17:00 -> Tuesday 09:00 is 16 wall-clock hours but 60 working minutes.
        _svc.WorkingMinutesElapsed(Local(2026, 8, 24, 17, 0), Local(2026, 8, 25, 9, 0))
            .Should().Be(60);
    }
}
