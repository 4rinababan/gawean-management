using System.Globalization;

namespace TaskManagement.Web.Components.Ui;

/// <summary>
/// Builds SVG coordinate strings.
/// <para>
/// SVG numbers must use a dot. Under a culture whose decimal separator is a comma (id-ID, de-DE, most
/// of Europe) default formatting turns <c>12.5</c> into <c>12,5</c> — and since SVG also treats a comma
/// as a coordinate separator, every point shifts by one and the chart renders as a scribble. All
/// coordinate formatting goes through here so that cannot happen per call site.
/// </para>
/// </summary>
public static class SvgGeometry
{
    /// <summary>Formats a coordinate for an SVG attribute, always with a dot.</summary>
    public static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Maps <paramref name="values"/> to a <c>points</c> string spanning <paramref name="width"/>,
    /// with the y axis running 0..<paramref name="max"/> bottom-to-top over <paramref name="height"/>.
    /// </summary>
    public static string Polyline(IReadOnlyList<double> values, double width, double height, double max)
    {
        if (values.Count == 0)
            return "";

        if (values.Count == 1)
            return $"{Number(0)},{Number(height - values[0] / max * height)}";

        var stepX = width / (values.Count - 1);

        return string.Join(' ', values.Select((value, index) =>
            $"{Number(index * stepX)},{Number(height - value / max * height)}"));
    }
}
