using TaskManagement.Domain;
using TaskManagement.Domain.Common;
using TaskManagement.Domain.Organizations;

namespace TaskManagement.Domain.Tests;

public class OrganizationTests
{
    private static Organization NewOrg(string owner = "owner-1")
        => new("Acme Inc", "Acme Inc!", owner);

    [Fact]
    public void New_organization_slugifies_name_and_makes_owner_an_admin()
    {
        var org = NewOrg();

        org.Slug.Should().Be("acme-inc");
        org.Members.Should().ContainSingle(m => m.UserId == "owner-1" && m.Role == OrgRole.Admin);
    }

    [Theory]
    [InlineData("Hello World", "hello-world")]
    [InlineData("  Multiple   Spaces  ", "multiple-spaces")]
    [InlineData("Trailing---dashes---", "trailing-dashes")]
    [InlineData("Ünïcode 123", "n-code-123")]
    public void Slugify_normalizes_input(string input, string expected)
        => Organization.Slugify(input).Should().Be(expected);

    [Fact]
    public void Slugify_throws_when_no_alphanumerics_remain()
        => new Action(() => Organization.Slugify("---")).Should().Throw<DomainException>();

    [Fact]
    public void AddMember_rejects_duplicates()
    {
        var org = NewOrg();
        org.AddMember("user-2", OrgRole.Member);

        new Action(() => org.AddMember("user-2", OrgRole.Viewer))
            .Should().Throw<DomainException>();
    }

    [Fact]
    public void RemoveMember_cannot_remove_the_last_admin()
    {
        var org = NewOrg();
        org.AddMember("user-2", OrgRole.Member);

        new Action(() => org.RemoveMember("owner-1"))
            .Should().Throw<DomainException>().WithMessage("*last administrator*");
    }

    [Fact]
    public void ChangeMemberRole_cannot_demote_the_last_admin()
    {
        var org = NewOrg();

        new Action(() => org.ChangeMemberRole("owner-1", OrgRole.Member))
            .Should().Throw<DomainException>();
    }

    [Fact]
    public void ChangeMemberRole_allows_demotion_when_another_admin_exists()
    {
        var org = NewOrg();
        org.AddMember("user-2", OrgRole.Admin);

        org.ChangeMemberRole("owner-1", OrgRole.Member);

        org.Members.Single(m => m.UserId == "owner-1").Role.Should().Be(OrgRole.Member);
    }

    [Fact]
    public void InviteMember_creates_a_pending_redeemable_invitation()
    {
        var org = NewOrg();

        var invite = org.InviteMember("NewGuy@Example.com ", OrgRole.Member, "owner-1", TimeSpan.FromDays(7));

        invite.Email.Should().Be("newguy@example.com");
        invite.Status.Should().Be(InvitationStatus.Pending);
        invite.IsRedeemable(DateTimeOffset.UtcNow).Should().BeTrue();
        invite.Token.Should().HaveLength(64);
    }

    [Fact]
    public void InviteMember_rejects_a_second_pending_invite_for_the_same_email()
    {
        var org = NewOrg();
        org.InviteMember("dup@example.com", OrgRole.Member, "owner-1", TimeSpan.FromDays(7));

        new Action(() => org.InviteMember("dup@example.com", OrgRole.Viewer, "owner-1", TimeSpan.FromDays(7)))
            .Should().Throw<DomainException>();
    }

    [Fact]
    public void Invitation_cannot_be_accepted_after_expiry()
    {
        var org = NewOrg();
        var invite = org.InviteMember("late@example.com", OrgRole.Member, "owner-1", TimeSpan.FromDays(1));

        new Action(() => invite.Accept("user-9", DateTimeOffset.UtcNow.AddDays(2)))
            .Should().Throw<DomainException>();
    }
}
