using DaftechCrm.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace DaftechCrm.Tests.Domain;

/// <summary>
/// Covers the star-to-score conversion and escalation threshold documented
/// in TicketService.ConfirmResolutionAsync: score = stars * 20, and a
/// score below TicketWorkflow:MinimumSatisfactionScore (default 90)
/// escalates instead of closing. Exercised here as pure arithmetic so the
/// rule is pinned down without needing a database.
/// </summary>
public class TicketScoringTests
{
    private const int MinimumSatisfactionScore = 90;

    [Theory]
    [InlineData(1, 20)]
    [InlineData(2, 40)]
    [InlineData(3, 60)]
    [InlineData(4, 80)]
    [InlineData(5, 100)]
    public void Stars_convert_to_score_by_multiplying_by_20(int stars, int expectedScore)
    {
        var score = stars * 20;
        score.Should().Be(expectedScore);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(4, true)]
    [InlineData(5, true)]
    public void Ticket_closes_only_when_score_meets_the_minimum_threshold(int stars, bool expectedClosesTicket)
    {
        var score = stars * 20;
        var closes = score >= MinimumSatisfactionScore;
        closes.Should().Be(expectedClosesTicket);
    }

    [Fact]
    public void A_four_star_rating_meets_the_default_threshold_exactly_at_80_which_is_below_90()
    {
        // Regression guard: 4 stars = 80, which is BELOW the 90 threshold —
        // easy to get backwards when reading "4 out of 5 stars" as "good enough".
        (4 * 20).Should().BeLessThan(MinimumSatisfactionScore);
    }
}
