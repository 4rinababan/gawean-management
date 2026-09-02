using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Contracts;
using TaskManagement.Application.Services;
using TaskManagement.Domain;
using TaskManagement.Domain.Organizations;
using TaskManagement.Domain.Projects;

namespace TaskManagement.Infrastructure.Tests;

/// <summary>Covers the edits made from the issue detail sidebar: assignee, status, estimate and sprint.</summary>
public class IssueDetailFlowTests : IDisposable
{
    private readonly ServiceFixture _fx = new();
    private readonly Guid _projectId;

    public IssueDetailFlowTests()
    {
        using var db = _fx.Db();
        var org = new Organization("Alpha", "alpha", "user-1");
        db.OrganizationMembers.Add(org.AddMember("user-2", OrgRole.Member));
        var project = new Project(org.Id, "WEB", "Web");
        db.Organizations.Add(org);
        db.Projects.Add(project);
        db.SaveChanges();
        _projectId = project.Id;
        _fx.Tenant.Set(org.Id, "alpha", OrgRole.Admin);
    }

    private async Task<Guid> NewIssueAsync() => await _fx.Build<IssueService>()
        .CreateAsync(new CreateIssueRequest { ProjectId = _projectId, Title = "T", Type = IssueType.Task });

    [Fact]
    public async Task Assigning_persists_and_is_visible_on_the_next_read()
    {
        var issues = _fx.Build<IssueService>();
        var issueId = await NewIssueAsync();

        await issues.AssignAsync(issueId, "user-2");

        var detail = await issues.GetAsync(issueId);
        detail.AssigneeUserId.Should().Be("user-2");
    }

    [Fact]
    public async Task Unassigning_clears_the_assignee()
    {
        var issues = _fx.Build<IssueService>();
        var issueId = await NewIssueAsync();
        await issues.AssignAsync(issueId, "user-2");

        await issues.AssignAsync(issueId, null);

        (await issues.GetAsync(issueId)).AssigneeUserId.Should().BeNull();
    }

    [Fact]
    public async Task Changing_status_persists()
    {
        var issues = _fx.Build<IssueService>();
        var issueId = await NewIssueAsync();

        await issues.ChangeStatusAsync(issueId, IssueStatus.InReview);

        (await issues.GetAsync(issueId)).Status.Should().Be(IssueStatus.InReview);
    }

    [Fact]
    public async Task Updating_the_issue_keeps_every_edited_field()
    {
        var issues = _fx.Build<IssueService>();
        var issueId = await NewIssueAsync();

        await issues.UpdateAsync(issueId, new UpdateIssueRequest
        {
            Title = "Renamed",
            Description = "Now with detail",
            Type = IssueType.Bug,
            Priority = IssuePriority.Highest,
            AssigneeUserId = "user-3",
            StoryPoints = 8,
        });

        var detail = await issues.GetAsync(issueId);
        detail.Title.Should().Be("Renamed");
        detail.Description.Should().Be("Now with detail");
        detail.Type.Should().Be(IssueType.Bug);
        detail.Priority.Should().Be(IssuePriority.Highest);
        detail.AssigneeUserId.Should().Be("user-3");
        detail.StoryPoints.Should().Be(8);
    }

    [Theory]
    [InlineData("please look at this @user-2")]      // username handle
    [InlineData("cc @User-2 thanks")]                 // case-insensitive
    [InlineData("ping @Useruser2")]                   // display name "User user-2", separators ignored
    public async Task Commenting_with_a_mention_notifies_the_mentioned_member(string body)
    {
        var issues = _fx.Build<IssueService>();
        var issueId = await NewIssueAsync();

        await issues.AddCommentAsync(new AddCommentRequest(issueId, body));

        await using var db = _fx.Db();
        (await db.Notifications.CountAsync(n => n.RecipientUserId == "user-2"
            && n.Type == Domain.NotificationType.IssueMentioned)).Should().Be(1);
    }

    [Fact]
    public async Task A_description_is_sanitised_before_it_is_stored()
    {
        var issues = _fx.Build<IssueService>();

        var issueId = await issues.CreateAsync(new CreateIssueRequest
        {
            ProjectId = _projectId,
            Title = "T",
            Type = IssueType.Task,
            Description = "<p><strong>keep</strong></p><script>alert('xss')</script><img src=x onerror=alert(1)>",
        });

        var stored = (await issues.GetAsync(issueId)).Description!;
        stored.Should().Contain("keep").And.Contain("strong");
        stored.Should().NotContain("<script").And.NotContain("onerror");
    }

    [Fact]
    public async Task Updating_a_description_sanitises_it_too()
    {
        var issues = _fx.Build<IssueService>();
        var issueId = await NewIssueAsync();

        await issues.UpdateAsync(issueId, new UpdateIssueRequest
        {
            Title = "T",
            Description = "<p onclick=\"steal()\">text</p>",
            Type = IssueType.Task,
            Priority = IssuePriority.Medium,
        });

        (await issues.GetAsync(issueId)).Description.Should().NotContain("onclick");
    }

    [Fact]
    public async Task An_email_address_in_a_comment_is_not_treated_as_a_mention()
    {
        var issues = _fx.Build<IssueService>();
        var issueId = await NewIssueAsync();

        await issues.AddCommentAsync(new AddCommentRequest(issueId, "reach me at user-2@example.com"));

        await using var db = _fx.Db();
        (await db.Notifications.CountAsync(n => n.Type == Domain.NotificationType.IssueMentioned))
            .Should().Be(0);
    }

    [Fact]
    public async Task Mentioning_someone_outside_the_workspace_notifies_nobody()
    {
        var issues = _fx.Build<IssueService>();
        var issueId = await NewIssueAsync();

        await issues.AddCommentAsync(new AddCommentRequest(issueId, "hey @stranger"));

        await using var db = _fx.Db();
        (await db.Notifications.CountAsync(n => n.Type == Domain.NotificationType.IssueMentioned))
            .Should().Be(0);
    }

    public void Dispose() => _fx.Dispose();
}
