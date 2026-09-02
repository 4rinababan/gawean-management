using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Services;

namespace TaskManagement.Application.Tests;

/// <summary>
/// The comment box inserts <see cref="Mentions.HandleFor"/> and the notifier resolves with
/// <see cref="Mentions.Matches"/>. The property that matters is that those two agree — otherwise a
/// mention picked from the list silently notifies nobody, which is exactly what used to happen.
/// </summary>
public class MentionsTests
{
    private static UserSummary User(string display, string email, string? userName = null)
        => new("u1", display, email, userName ?? email, "#111");

    [Theory]
    [InlineData("Bang Boy", "bang.boy@example.com")]
    [InlineData("ARI NABABAN", "ari.decentindonesia@gmail.com")]
    [InlineData("Ada", "ada@example.com")]
    [InlineData("Jean-Luc Picard", "jlp@example.com")]
    public void The_handle_offered_to_the_author_is_one_the_notifier_resolves(string display, string email)
    {
        var user = User(display, email);

        var handle = Mentions.HandleFor(display, email);

        Mentions.Matches(user, handle).Should().BeTrue($"'@{handle}' is what the picker inserts");
    }

    [Fact]
    public void A_handle_is_produced_even_when_the_display_name_has_no_letters()
    {
        Mentions.HandleFor("", "someone@example.com").Should().Be("someone");
        Mentions.HandleFor("   ", "someone@example.com").Should().Be("someone");
    }

    [Theory]
    [InlineData("bangboy")]
    [InlineData("BangBoy")]
    [InlineData("bang.boy")]
    [InlineData("BANG-BOY")]
    public void Matching_ignores_case_and_separators(string token)
        => Mentions.Matches(User("Bang Boy", "bb@example.com"), token).Should().BeTrue();

    [Fact]
    public void The_email_local_part_also_matches()
        => Mentions.Matches(User("Ari Nababan", "ari.decentindonesia@gmail.com"), "aridecentindonesia")
            .Should().BeTrue();

    [Theory]
    [InlineData("someoneelse")]
    [InlineData("")]
    [InlineData("   ")]
    public void Unrelated_tokens_do_not_match(string token)
        => Mentions.Matches(User("Bang Boy", "bb@example.com"), token).Should().BeFalse();

    [Fact]
    public void Tokens_are_extracted_from_a_comment_body()
    {
        var tokens = Mentions.Extract("cc @alice and @bob — also @alice again");

        tokens.Should().BeEquivalentTo(["alice", "bob"]);
    }

    [Fact]
    public void An_email_address_in_the_body_is_not_a_mention()
        => Mentions.Extract("reach me at bang.boy@example.com").Should().BeEmpty();

    [Fact]
    public void A_bare_at_sign_is_not_a_mention()
        => Mentions.Extract("cost is 5 @ each").Should().BeEmpty();
}
