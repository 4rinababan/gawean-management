using Bunit;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain;
using TaskManagement.Web.Components.Ui;

namespace TaskManagement.Web.Tests;

public class MentionTextAreaTests : BunitContext
{
    private static IReadOnlyList<OrganizationMemberDto> Members =>
    [
        new("u1", "Ada Lovelace", "ada@x.com", "#111", OrgRole.Member, "adalovelace"),
        new("u2", "Bang Boy", "bang.boy@x.com", "#222", OrgRole.Member, "bangboy"),
    ];

    private IRenderedComponent<MentionTextArea> RenderBox(Action<string?> onChange, string? value = "")
        => Render<MentionTextArea>(ps => ps
            .Add(p => p.Members, Members)
            .Add(p => p.Value, value)
            .Add(p => p.ValueChanged, onChange));

    [Fact]
    public void No_suggestions_until_an_at_sign_is_typed()
    {
        var cut = RenderBox(_ => { });

        cut.Find("textarea").Input("looks good to me");

        cut.Markup.Should().NotContain("Ada Lovelace");
    }

    [Fact]
    public void Typing_an_at_sign_offers_every_member()
    {
        var cut = RenderBox(_ => { });

        cut.Find("textarea").Input("cc @");

        cut.Markup.Should().Contain("Ada Lovelace").And.Contain("Bang Boy");
    }

    [Fact]
    public void The_list_narrows_as_the_handle_is_typed()
    {
        var cut = RenderBox(_ => { });

        cut.Find("textarea").Input("cc @bang");

        cut.Markup.Should().Contain("Bang Boy");
        cut.Markup.Should().NotContain("Ada Lovelace");
    }

    [Fact]
    public void Picking_someone_inserts_their_canonical_handle()
    {
        string? value = null;
        var cut = RenderBox(v => value = v);

        cut.Find("textarea").Input("cc @bang");
        cut.FindAll("button").First(b => b.TextContent.Contains("Bang Boy")).Click();

        value.Should().Be("cc @bangboy ", "the handle must be the one the notifier resolves");
    }

    [Fact]
    public void Picking_replaces_only_the_partial_token()
    {
        string? value = null;
        var cut = RenderBox(v => value = v);

        cut.Find("textarea").Input("please review this @ada");
        cut.FindAll("button").First(b => b.TextContent.Contains("Ada Lovelace")).Click();

        value.Should().Be("please review this @adalovelace ");
    }

    [Fact]
    public void An_email_address_does_not_open_the_picker()
    {
        var cut = RenderBox(_ => { });

        cut.Find("textarea").Input("mail me at bang.boy@x.com");

        cut.Markup.Should().NotContain("Bang Boy");
    }

    [Fact]
    public void The_picker_closes_after_a_selection()
    {
        var cut = RenderBox(_ => { });

        cut.Find("textarea").Input("cc @bang");
        cut.FindAll("button").First(b => b.TextContent.Contains("Bang Boy")).Click();

        cut.FindAll("button").Should().BeEmpty();
    }
}
