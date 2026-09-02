using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Contracts;
using TaskManagement.Application.Services;
using TaskManagement.Domain;
using TaskManagement.Domain.Organizations;
using TaskManagement.Domain.Projects;

namespace TaskManagement.Infrastructure.Tests;

public class BoardAndSprintServiceTests : IDisposable
{
    private readonly ServiceFixture _fx = new();
    private readonly Guid _projectId;

    public BoardAndSprintServiceTests()
    {
        using var db = _fx.Db();
        var org = new Organization("Alpha", "alpha", "user-1");
        var project = new Project(org.Id, "WEB", "Web");
        db.Organizations.Add(org);
        db.Projects.Add(project);
        db.SaveChanges();
        _projectId = project.Id;
        _fx.Tenant.Set(org.Id, "alpha", OrgRole.Admin);
    }

    [Fact]
    public async Task Creating_issues_assigns_sequential_references()
    {
        var issues = _fx.Build<IssueService>();

        await issues.CreateAsync(new CreateIssueRequest { ProjectId = _projectId, Title = "First", Type = IssueType.Task });
        await issues.CreateAsync(new CreateIssueRequest { ProjectId = _projectId, Title = "Second", Type = IssueType.Bug });

        await using var db = _fx.Db();
        var refs = await db.Issues.OrderBy(i => i.Number).Select(i => i.Number).ToListAsync();
        refs.Should().Equal(1, 2);
    }

    [Fact]
    public async Task Moving_a_card_changes_status_and_writes_activity()
    {
        var issueId = await _fx.Build<IssueService>()
            .CreateAsync(new CreateIssueRequest { ProjectId = _projectId, Title = "Movable", Type = IssueType.Task });

        await _fx.Build<BoardService>().MoveAsync(new MoveIssueRequest(issueId, IssueStatus.InProgress, null, null));

        await using var db = _fx.Db();
        (await db.Issues.FindAsync(issueId))!.Status.Should().Be(IssueStatus.InProgress);
        (await db.ActivityLogs.CountAsync(a => a.IssueId == issueId && a.Field == "Status")).Should().Be(1);
    }

    [Fact]
    public async Task Assigning_an_issue_notifies_the_new_assignee()
    {
        var issues = _fx.Build<IssueService>();
        var issueId = await issues.CreateAsync(new CreateIssueRequest { ProjectId = _projectId, Title = "T", Type = IssueType.Task });

        await issues.AssignAsync(issueId, "user-2");

        await using var db = _fx.Db();
        (await db.Notifications.CountAsync(n => n.RecipientUserId == "user-2"
            && n.Type == Domain.NotificationType.IssueAssigned)).Should().Be(1);
    }

    [Fact]
    public async Task Only_one_sprint_can_be_active_per_project()
    {
        var sprints = _fx.Build<SprintService>();
        var s1 = await sprints.CreateAsync(new CreateSprintRequest { ProjectId = _projectId, Name = "S1" });
        var s2 = await sprints.CreateAsync(new CreateSprintRequest { ProjectId = _projectId, Name = "S2" });

        var start = new StartSprintRequest { StartDate = new(2026, 1, 6), EndDate = new(2026, 1, 20) };
        await sprints.StartAsync(s1, start);

        var act = async () => await sprints.StartAsync(s2, start);
        await act.Should().ThrowAsync<TaskManagement.Application.Common.ConflictException>();
    }

    [Fact]
    public async Task Completing_a_sprint_moves_unfinished_issues_to_the_backlog()
    {
        var sprints = _fx.Build<SprintService>();
        var issues = _fx.Build<IssueService>();
        var sprintId = await sprints.CreateAsync(new CreateSprintRequest { ProjectId = _projectId, Name = "S" });
        var issueId = await issues.CreateAsync(new CreateIssueRequest { ProjectId = _projectId, Title = "Carryover", Type = IssueType.Task, SprintId = sprintId });
        await sprints.StartAsync(sprintId, new StartSprintRequest { StartDate = new(2026, 1, 6), EndDate = new(2026, 1, 20) });

        await sprints.CompleteAsync(sprintId, null);

        await using var db = _fx.Db();
        (await db.Issues.FindAsync(issueId))!.SprintId.Should().BeNull();
    }

    [Fact]
    public async Task Viewer_role_cannot_create_issues()
    {
        _fx.Tenant.Set(_fx.Tenant.OrganizationId, "alpha", OrgRole.Viewer);
        var issues = _fx.Build<IssueService>();

        var act = async () => await issues.CreateAsync(new CreateIssueRequest { ProjectId = _projectId, Title = "Nope", Type = IssueType.Task });

        await act.Should().ThrowAsync<TaskManagement.Application.Common.ForbiddenException>();
    }

    public void Dispose() => _fx.Dispose();
}
