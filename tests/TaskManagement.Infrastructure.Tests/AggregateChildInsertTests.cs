using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Contracts;
using TaskManagement.Application.Services;
using TaskManagement.Domain;
using TaskManagement.Domain.Organizations;
using TaskManagement.Domain.Projects;

namespace TaskManagement.Infrastructure.Tests;

/// <summary>
/// Entity ids are generated client-side (Guid.CreateVersion7 in the Entity base). When a child is created
/// through an aggregate method and only ends up in a navigation collection, EF's "the key is already set"
/// heuristic can mark it Modified instead of Added — producing an UPDATE of a row that doesn't exist yet
/// ("expected to affect 1 row(s), but actually affected 0"). These cover the services that do that.
/// </summary>
public class AggregateChildInsertTests : IDisposable
{
    private readonly ServiceFixture _fx = new();
    private readonly Guid _orgId;
    private readonly Guid _projectId;

    public AggregateChildInsertTests()
    {
        using var db = _fx.Db();
        var org = new Organization("Alpha", "alpha", "user-1");
        var project = new Project(org.Id, "WEB", "Web");
        db.Organizations.Add(org);
        db.Projects.Add(project);
        db.SaveChanges();
        _orgId = org.Id;
        _projectId = project.Id;
        _fx.Tenant.Set(org.Id, "alpha", OrgRole.Admin);
    }

    [Fact]
    public async Task Inviting_a_member_persists_the_invitation()
    {
        await _fx.Build<InvitationService>()
            .InviteAsync(new InviteMemberRequest { Email = "new.person@example.com", Role = OrgRole.Member });

        await using var db = _fx.Db();
        var invite = await db.Invitations.SingleAsync();
        invite.Email.Should().Be("new.person@example.com");
        invite.Status.Should().Be(InvitationStatus.Pending);
        invite.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Pending_invitations_are_listed_with_an_accept_url()
    {
        var invitations = _fx.Build<InvitationService>();
        await invitations.InviteAsync(new InviteMemberRequest { Email = "a@example.com", Role = OrgRole.Member });

        var pending = await invitations.GetPendingAsync();

        pending.Should().ContainSingle();
        pending[0].Email.Should().Be("a@example.com");
        pending[0].AcceptUrl.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Accepting_an_invitation_adds_the_member_and_is_idempotent()
    {
        var invitations = _fx.Build<InvitationService>();
        await invitations.InviteAsync(new InviteMemberRequest { Email = "joiner@example.com", Role = OrgRole.Member });

        string token;
        await using (var db = _fx.Db())
            token = (await db.Invitations.SingleAsync()).Token;

        _fx.CurrentUser.UserId = "user-2";
        var first = await invitations.AcceptAsync(token);
        var second = await invitations.AcceptAsync(token); // re-render / double click must not blow up

        first.OrganizationSlug.Should().Be("alpha");
        second.OrganizationSlug.Should().Be("alpha");

        await using var check = _fx.Db();
        (await check.OrganizationMembers.CountAsync(m => m.OrganizationId == _orgId && m.UserId == "user-2"))
            .Should().Be(1);
    }

    [Fact]
    public async Task Adding_a_comment_persists_it()
    {
        var issues = _fx.Build<IssueService>();
        var issueId = await issues.CreateAsync(new CreateIssueRequest { ProjectId = _projectId, Title = "T", Type = IssueType.Task });

        await issues.AddCommentAsync(new AddCommentRequest(issueId, "Looks good to me"));

        await using var db = _fx.Db();
        var comment = await db.Comments.SingleAsync();
        comment.Body.Should().Be("Looks good to me");
        comment.IssueId.Should().Be(issueId);
    }

    public void Dispose() => _fx.Dispose();
}
