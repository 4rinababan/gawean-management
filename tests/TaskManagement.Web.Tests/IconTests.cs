using Bunit;
using TaskManagement.Web.Components.Ui;

namespace TaskManagement.Web.Tests;

/// <summary>
/// Components take icon *names*, not glyphs. When one of them forgets to render an
/// <see cref="Icon"/> the name leaks onto the page as literal text ("check-circle"), which is what
/// these guard against — every wrapper must emit an svg and never the raw name.
/// </summary>
public class IconTests : BunitContext
{
    [Theory]
    [InlineData("home")]
    [InlineData("chart-bar")]
    [InlineData("users")]
    [InlineData("cog")]
    [InlineData("plus")]
    [InlineData("envelope")]
    [InlineData("bell")]
    [InlineData("trash")]
    [InlineData("check-circle")]
    [InlineData("clock")]
    [InlineData("exclamation-triangle")]
    [InlineData("squares")]
    [InlineData("folder")]
    [InlineData("sun")]
    [InlineData("moon")]
    [InlineData("bars")]
    [InlineData("x-mark")]
    [InlineData("paper-clip")]
    [InlineData("lock-closed")]
    [InlineData("arrow-left")]
    [InlineData("sign-out")]
    [InlineData("rocket")]
    [InlineData("clipboard")]
    [InlineData("magnifying-glass")]
    [InlineData("flag")]
    [InlineData("check")]
    public void Every_named_icon_renders_a_path(string name)
    {
        var cut = Render<Icon>(ps => ps.Add(p => p.Name, name));

        cut.Find("svg").Should().NotBeNull();
        cut.Find("path").GetAttribute("d").Should().NotBeNullOrWhiteSpace();
        cut.Markup.Should().NotContain(name, "the name is a lookup key, never rendered text");
    }

    [Fact]
    public void An_unknown_name_falls_back_to_a_placeholder_rather_than_rendering_nothing()
    {
        var cut = Render<Icon>(ps => ps.Add(p => p.Name, "definitely-not-an-icon"));

        cut.Find("path").GetAttribute("d").Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void StatTile_renders_its_icon_as_svg_not_as_the_name()
    {
        var cut = Render<StatTile>(ps => ps
            .Add(p => p.Label, "Overdue")
            .Add(p => p.Value, 3)
            .Add(p => p.Icon, "exclamation-triangle"));

        cut.FindAll("svg").Should().NotBeEmpty();
        cut.Markup.Should().NotContain("exclamation-triangle");
        cut.Markup.Should().Contain("Overdue").And.Contain("3");
    }

    [Fact]
    public void Button_renders_its_icon_as_svg_not_as_the_name()
    {
        var cut = Render<Button>(ps => ps
            .Add(p => p.Icon, "plus")
            .AddChildContent("New workspace"));

        cut.FindAll("svg").Should().NotBeEmpty();
        cut.Markup.Should().NotContain(">plus<");
    }

    [Fact]
    public void EmptyState_renders_its_icon_as_svg_not_as_the_name()
    {
        var cut = Render<EmptyState>(ps => ps
            .Add(p => p.Icon, "rocket")
            .Add(p => p.Title, "Nothing here"));

        cut.FindAll("svg").Should().NotBeEmpty();
        cut.Markup.Should().NotContain("rocket");
    }

    [Fact]
    public void MenuItem_renders_its_icon_as_svg_not_as_the_name()
    {
        var cut = Render<MenuItem>(ps => ps
            .Add(p => p.Icon, "cog")
            .AddChildContent("Settings"));

        cut.FindAll("svg").Should().NotBeEmpty();
        cut.Markup.Should().NotContain(">cog<");
    }
}
