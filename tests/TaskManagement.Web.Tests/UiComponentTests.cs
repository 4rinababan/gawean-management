using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain;
using TaskManagement.Web.Components.Ui;

namespace TaskManagement.Web.Tests;

public class UiComponentTests : BunitContext
{
    [Fact]
    public void Button_renders_label_and_raises_click()
    {
        var clicked = 0;
        var cut = Render<Button>(ps => ps
            .Add(p => p.OnClick, _ => clicked++)
            .AddChildContent("Save changes"));

        cut.Markup.Should().Contain("Save changes");
        cut.Find("button").Click();
        clicked.Should().Be(1);
    }

    [Fact]
    public void Button_is_disabled_while_loading_and_does_not_raise_click()
    {
        var clicked = 0;
        var cut = Render<Button>(ps => ps
            .Add(p => p.Loading, true)
            .Add(p => p.OnClick, _ => clicked++)
            .AddChildContent("Go"));

        cut.Find("button").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Avatar_derives_initials_from_a_name()
    {
        var cut = Render<Avatar>(ps => ps.Add(p => p.Name, "Ada Lovelace"));
        cut.Markup.Should().Contain(">AL<");
    }

    [Fact]
    public void Avatar_falls_back_to_a_question_mark_when_name_is_blank()
    {
        var cut = Render<Avatar>(ps => ps.Add(p => p.Name, "  "));
        cut.Markup.Should().Contain(">?<");
    }

    [Fact]
    public void Badge_applies_the_colour_class()
    {
        var cut = Render<Badge>(ps => ps
            .Add(p => p.Color, Badge.BadgeColor.Green)
            .AddChildContent("Done"));

        cut.Markup.Should().Contain("emerald");
        cut.Markup.Should().Contain("Done");
    }

    [Fact]
    public void Modal_renders_nothing_when_closed_and_content_when_open()
    {
        var cut = Render<Modal>(ps => ps
            .Add(p => p.Open, false)
            .Add(p => p.Title, "Hi")
            .AddChildContent("Body text"));
        cut.Markup.Trim().Should().BeEmpty();

        cut.Render(ps => ps
            .Add(p => p.Open, true)
            .Add(p => p.Title, "Hi")
            .AddChildContent("Body text"));
        cut.Markup.Should().Contain("Body text").And.Contain("Hi");
    }

    [Fact]
    public void Modal_close_button_requests_close()
    {
        var open = true;
        var cut = Render<Modal>(ps => ps
            .Add(p => p.Open, true)
            .Add(p => p.OpenChanged, v => open = v)
            .Add(p => p.Title, "T")
            .AddChildContent("x"));

        cut.Find("button[aria-label=Close]").Click();
        open.Should().BeFalse();
    }

    [Fact]
    public void ConfirmDialog_invokes_OnConfirm_then_closes()
    {
        var confirmed = false;
        var open = true;
        var cut = Render<ConfirmDialog>(ps => ps
            .Add(p => p.Open, true)
            .Add(p => p.OpenChanged, v => open = v)
            .Add(p => p.OnConfirm, () => confirmed = true)
            .Add(p => p.ConfirmText, "Delete it"));

        cut.FindAll("button").First(b => b.TextContent.Contains("Delete it")).Click();

        confirmed.Should().BeTrue();
        open.Should().BeFalse();
    }

    [Fact]
    public void EmptyState_shows_title_and_actions()
    {
        var cut = Render<EmptyState>(ps => ps
            .Add(p => p.Title, "Nothing here")
            .Add(p => p.Actions, b => b.AddMarkupContent(0, "<a>Add one</a>")));

        cut.Markup.Should().Contain("Nothing here").And.Contain("Add one");
    }

    [Fact]
    public void KanbanCard_shows_reference_points_and_assignee_avatar()
    {
        var issue = new IssueListItemDto(Guid.NewGuid(), "WEB-7", "Fix header", IssueType.Bug,
            IssueStatus.Todo, IssuePriority.High, 5, "u1", "Grace Hopper", "#111", null, "aaa", null, false);

        var cut = Render<KanbanCard>(ps => ps.Add(p => p.Issue, issue));

        cut.Markup.Should().Contain("WEB-7").And.Contain("Fix header").And.Contain(">5<").And.Contain(">GH<");
    }

    [Fact]
    public void KanbanCard_click_raises_OnOpenIssue_with_the_id()
    {
        var id = Guid.NewGuid();
        Guid? opened = null;
        var issue = new IssueListItemDto(id, "WEB-1", "T", IssueType.Task, IssueStatus.Todo,
            IssuePriority.Medium, null, null, null, null, null, "a", null, false);

        var cut = Render<KanbanCard>(ps => ps
            .Add(p => p.Issue, issue)
            .Add(p => p.OnOpenIssue, g => opened = g));

        cut.Find("[data-issue-id]").Click();
        opened.Should().Be(id);
    }

    [Fact]
    public void UserPicker_filters_members_by_the_search_term()
    {
        IReadOnlyList<OrganizationMemberDto> members =
        [
            new("u1", "Alice Smith", "alice@x.com", "#1", OrgRole.Member),
            new("u2", "Bob Jones", "bob@x.com", "#2", OrgRole.Member),
        ];

        var cut = Render<UserPicker>(ps => ps.Add(p => p.Members, members));
        cut.Find("button").Click(); // open

        cut.Find("input").Input("bob");

        cut.Markup.Should().Contain("Bob Jones");
        cut.Markup.Should().NotContain("Alice Smith");
    }
}
