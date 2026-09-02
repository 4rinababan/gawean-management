namespace TaskManagement.Domain.Sprints;

/// <summary>One day on the burndown chart: story points still open at the end of that day vs. the ideal trend line.</summary>
public sealed record BurndownPoint(DateOnly Date, double Remaining, double Ideal);

/// <summary>A story-point completion event within a sprint: points that became Done (or were removed) on a given date.</summary>
public sealed record BurndownEvent(DateOnly Date, double PointsBurned);

public static class Burndown
{
    /// <summary>
    /// Builds the burndown series for a sprint running <paramref name="start"/>..<paramref name="end"/> inclusive,
    /// starting from <paramref name="totalPoints"/> committed points and applying <paramref name="events"/> chronologically.
    /// </summary>
    public static IReadOnlyList<BurndownPoint> Build(
        DateOnly start,
        DateOnly end,
        double totalPoints,
        IEnumerable<BurndownEvent> events)
    {
        if (end < start)
            throw new ArgumentException("Sprint end must not precede its start.", nameof(end));

        var burnedByDate = events
            .GroupBy(e => e.Date)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.PointsBurned));

        var totalDays = end.DayNumber - start.DayNumber;
        var points = new List<BurndownPoint>(totalDays + 1);
        var remaining = totalPoints;

        for (var day = 0; day <= totalDays; day++)
        {
            var date = start.AddDays(day);
            if (burnedByDate.TryGetValue(date, out var burned))
                remaining = Math.Max(0, remaining - burned);

            var ideal = totalDays == 0 ? 0 : totalPoints * (1 - (double)day / totalDays);
            points.Add(new BurndownPoint(date, remaining, Math.Round(ideal, 2)));
        }

        return points;
    }
}
