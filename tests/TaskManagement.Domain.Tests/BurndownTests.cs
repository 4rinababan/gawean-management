using TaskManagement.Domain.Sprints;

namespace TaskManagement.Domain.Tests;

public class BurndownTests
{
    private static readonly DateOnly Start = new(2026, 1, 5);

    [Fact]
    public void Build_returns_one_point_per_day_inclusive()
    {
        var series = Burndown.Build(Start, Start.AddDays(9), 20, []);

        series.Should().HaveCount(10);
        series[0].Date.Should().Be(Start);
        series[^1].Date.Should().Be(Start.AddDays(9));
    }

    [Fact]
    public void Ideal_line_falls_linearly_from_total_to_zero()
    {
        var series = Burndown.Build(Start, Start.AddDays(10), 30, []);

        series[0].Ideal.Should().Be(30);
        series[^1].Ideal.Should().Be(0);
        series[5].Ideal.Should().BeApproximately(15, 0.001);
    }

    [Fact]
    public void Remaining_drops_as_points_are_burned_and_never_goes_negative()
    {
        var events = new[]
        {
            new BurndownEvent(Start.AddDays(2), 8),
            new BurndownEvent(Start.AddDays(2), 2),
            new BurndownEvent(Start.AddDays(4), 100),
        };

        var series = Burndown.Build(Start, Start.AddDays(5), 20, events);

        series[1].Remaining.Should().Be(20);
        series[2].Remaining.Should().Be(10);
        series[3].Remaining.Should().Be(10);
        series[4].Remaining.Should().Be(0);
    }

    [Fact]
    public void Build_rejects_an_inverted_date_range()
        => new Action(() => Burndown.Build(Start, Start.AddDays(-1), 5, []))
            .Should().Throw<ArgumentException>();
}
