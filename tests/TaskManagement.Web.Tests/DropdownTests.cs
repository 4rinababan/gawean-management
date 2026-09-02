using Bunit;
using Microsoft.AspNetCore.Components;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain;
using TaskManagement.Web.Components.Ui;

namespace TaskManagement.Web.Tests;

/// <summary>
/// Guards the click path through the overlay components. These previously closed on <c>@onfocusout</c>,
/// which fires on mousedown and tore the panel down before the item's click event could land.
/// </summary>
public class DropdownTests : BunitContext
{
    [Fact]
    public void DropdownMenu_opens_on_trigger_and_a_menu_item_click_reaches_its_handler()
    {
        var clicked = 0;

        var cut = Render<DropdownMenu>(ps => ps
            .Add<RenderFragment>(p => p.Trigger, b => b.AddMarkupContent(0, "<span id=\"trigger\">open</span>"))
            .Add<RenderFragment>(p => p.ChildContent, b =>
            {
                b.OpenComponent<MenuItem>(0);
                b.AddComponentParameter(1, nameof(MenuItem.OnClick), EventCallback.Factory.Create(this, () => clicked++));
                b.AddComponentParameter(2, nameof(MenuItem.ChildContent), (RenderFragment)(cb => cb.AddContent(0, "Do it")));
                b.CloseComponent();
            }));

        cut.Markup.Should().NotContain("Do it", "the menu starts closed");

        cut.Find("#trigger").Click();
        cut.Markup.Should().Contain("Do it");

        cut.FindAll("button").First(b => b.TextContent.Contains("Do it")).Click();

        clicked.Should().Be(1);
    }

    [Fact]
    public void UserPicker_option_click_raises_ValueChanged()
    {
        IReadOnlyList<OrganizationMemberDto> members =
        [
            new("u1", "Ada Lovelace", "ada@x.com", "#111", OrgRole.Member),
            new("u2", "Bob Jones", "bob@x.com", "#222", OrgRole.Member),
        ];
        string? picked = "unset";

        var cut = Render<UserPicker>(ps => ps
            .Add(p => p.Members, members)
            .Add(p => p.ValueChanged, v => picked = v));

        cut.Find("button").Click(); // open
        cut.FindAll("button").First(b => b.TextContent.Contains("Bob Jones")).Click();

        picked.Should().Be("u2");
    }

    [Fact]
    public void UserPicker_can_clear_the_selection()
    {
        IReadOnlyList<OrganizationMemberDto> members = [new("u1", "Ada", "ada@x.com", "#111", OrgRole.Member)];
        string? picked = "u1";

        var cut = Render<UserPicker>(ps => ps
            .Add(p => p.Members, members)
            .Add(p => p.Value, "u1")
            .Add(p => p.ValueChanged, v => picked = v));

        cut.Find("button").Click();
        cut.FindAll("button").First(b => b.TextContent.Contains("Unassigned")).Click();

        picked.Should().BeNull();
    }
}
