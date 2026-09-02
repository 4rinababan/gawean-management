using System.Globalization;
using TaskManagement.Web.Components.Ui;

namespace TaskManagement.Web.Tests;

/// <summary>
/// The burndown chart rendered as a scribble for an Indonesian user: a comma decimal separator turned
/// "12.5" into "12,5", and SVG reads a comma as a coordinate separator, so every point shifted.
/// These run under comma-decimal cultures on purpose.
/// </summary>
public class SvgGeometryTests
{
    private static readonly string[] CommaDecimalCultures = ["id-ID", "de-DE", "fr-FR", "pt-BR"];

    private static void InCulture(string name, Action assert)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(name);
            assert();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("id-ID")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    public void A_coordinate_always_uses_a_dot(string culture)
        => InCulture(culture, () => SvgGeometry.Number(12.5).Should().Be("12.5"));

    [Fact]
    public void A_polyline_has_exactly_one_coordinate_pair_per_value()
    {
        foreach (var culture in CommaDecimalCultures)
        {
            InCulture(culture, () =>
            {
                var values = new double[] { 14, 11, 9, 4, 0 };

                var points = SvgGeometry.Polyline(values, width: 300, height: 80, max: 14);

                var pairs = points.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                pairs.Should().HaveCount(values.Length, $"under {culture}");
                pairs.Should().OnlyContain(p => p.Split(',').Length == 2,
                    $"under {culture} a comma decimal separator would split a coordinate in two");
            });
        }
    }

    [Fact]
    public void The_line_spans_the_full_width_and_inverts_the_y_axis()
    {
        var points = SvgGeometry.Polyline([10, 5, 0], width: 300, height: 80, max: 10)
            .Split(' ')
            .Select(p => p.Split(','))
            .Select(p => (X: double.Parse(p[0], CultureInfo.InvariantCulture),
                          Y: double.Parse(p[1], CultureInfo.InvariantCulture)))
            .ToList();

        points[0].X.Should().Be(0);
        points[^1].X.Should().Be(300);

        points[0].Y.Should().Be(0, "the maximum value sits at the top");
        points[1].Y.Should().Be(40);
        points[^1].Y.Should().Be(80, "zero sits on the baseline");
    }

    [Fact]
    public void A_single_point_still_produces_a_valid_coordinate()
        => SvgGeometry.Polyline([5], width: 300, height: 80, max: 10).Should().Be("0,40");

    [Fact]
    public void No_values_produces_an_empty_string_rather_than_broken_markup()
        => SvgGeometry.Polyline([], width: 300, height: 80, max: 10).Should().BeEmpty();
}
