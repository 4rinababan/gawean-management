using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common;
using TaskManagement.Application.Contracts;
using TaskManagement.Application.Services;
using TaskManagement.Domain;
using TaskManagement.Domain.Organizations;
using TaskManagement.Domain.Projects;

namespace TaskManagement.Infrastructure.Tests;

public class DashboardAndOwnershipTests : IDisposable
{
    private readonly ServiceFixture _fx = new();
    private readonly Guid _orgId;
    private readonly Guid _projectId;
    private static readonly DateOnly Today = new(2026, 1, 5); // matches FakeClock

    public DashboardAndOwnershipTests()
    {
        using var db = _fx.Db();
        var org = new Organization("Alpha", "alpha", "user-1");
        db.OrganizationMembers.Add(org.AddMember("user-2", OrgRole.Member));
        var project = new Project(org.Id, "WEB", "Web", leadUserId: "user-1");
        db.Organizations.Add(org);
        db.Projects.Add(project);
        db.SaveChanges();
        _orgId = org.Id;
        _projectId = project.Id;
        _fx.Tenant.Set(org.Id, "alpha", OrgRole.Admin);
    }

    private async Task<Guid> NewIssueAsync(string? assignee, IssueStatus status, DateOnly? due)
    {
        var issues = _fx.Build<IssueService>();
        var id = await issues.CreateAsync(new CreateIssueRequest
        {
            ProjectId = _projectId,
            Title = "T",
            Type = IssueType.Task,
            AssigneeUserId = assignee,
            DueDate = due,
        });
        if (status != IssueStatus.Backlog)
            await issues.ChangeStatusAsync(id, status);
        return id;
    }

    // ---- Dashboard -------------------------------------------------------------

    [Fact]
    public async Task Dashboard_counts_only_the_current_users_own_work()
    {
        await NewIssueAsync("user-1", IssueStatus.Done, null);
        await NewIssueAsync("user-1", IssueStatus.InProgress, null);
        await NewIssueAsync("user-2", IssueStatus.InProgress, null);   // someone else's
        await NewIssueAsync(null, IssueStatus.InProgress, null);        // unassigned

        var dash = await _fx.Build<DashboardService>().GetAsync();

        dash.Stats.Done.Should().Be(1);
        dash.Stats.InProgress.Should().Be(1);
    }

    [Fact]
    public async Task In_progress_includes_issues_in_review()
    {
        await NewIssueAsync("user-1", IssueStatus.InProgress, null);
        await NewIssueAsync("user-1", IssueStatus.InReview, null);

        (await _fx.Build<DashboardService>().GetAsync()).Stats.InProgress.Should().Be(2);
    }

    [Fact]
    public async Task Overdue_counts_past_due_open_work_but_not_finished_work()
    {
        await NewIssueAsync("user-1", IssueStatus.Todo, Today.AddDays(-2));   // overdue
        await NewIssueAsync("user-1", IssueStatus.Todo, Today.AddDays(3));    // upcoming
        await NewIssueAsync("user-1", IssueStatus.Done, Today.AddDays(-9));   // late but done

        var dash = await _fx.Build<DashboardService>().GetAsync();

        dash.Stats.Overdue.Should().Be(1);
    }

    [Fact]
    public async Task Dashboard_reports_project_and_workspace_counts()
    {
        var dash = await _fx.Build<DashboardService>().GetAsync();

        dash.Stats.Projects.Should().Be(1);
        dash.Stats.Workspaces.Should().Be(1);
        dash.Workspaces.Should().ContainSingle(w => w.Slug == "alpha");
    }

    [Fact]
    public async Task My_open_tasks_lead_with_the_overdue_ones_and_exclude_done()
    {
        await NewIssueAsync("user-1", IssueStatus.Done, null);
        await NewIssueAsync("user-1", IssueStatus.Todo, Today.AddDays(30));
        var overdueId = await NewIssueAsync("user-1", IssueStatus.Todo, Today.AddDays(-1));

        var dash = await _fx.Build<DashboardService>().GetAsync();

        dash.MyOpenTasks.Should().HaveCount(2);
        dash.MyOpenTasks[0].Id.Should().Be(overdueId);
        dash.MyOpenTasks[0].IsOverdue.Should().BeTrue();
        dash.MyOpenTasks[0].Reference.Should().StartWith("WEB-");
        dash.MyOpenTasks[0].OrganizationSlug.Should().Be("alpha");
    }

    // ---- Project ownership -----------------------------------------------------

    [Fact]
    public async Task The_project_owner_can_edit_their_project()
    {
        _fx.CurrentUser.UserId = "user-1";
        _fx.Tenant.Set(_orgId, "alpha", OrgRole.Member);

        await _fx.Build<ProjectService>()
            .UpdateAsync(_projectId, new UpdateProjectRequest { Name = "Renamed", LeadUserId = "user-1" });

        (await _fx.Build<ProjectService>().GetByKeyAsync("WEB")).Name.Should().Be("Renamed");
    }

    [Fact]
    public async Task A_non_owner_member_cannot_edit_the_project()
    {
        _fx.CurrentUser.UserId = "user-2";
        _fx.Tenant.Set(_orgId, "alpha", OrgRole.Member);

        var act = async () => await _fx.Build<ProjectService>()
            .UpdateAsync(_projectId, new UpdateProjectRequest { Name = "Hijacked" });

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task A_workspace_admin_can_still_edit_a_project_they_do_not_own()
    {
        _fx.CurrentUser.UserId = "user-2";
        _fx.Tenant.Set(_orgId, "alpha", OrgRole.Admin);

        await _fx.Build<ProjectService>()
            .UpdateAsync(_projectId, new UpdateProjectRequest { Name = "Admin fixed it", LeadUserId = "user-1" });

        (await _fx.Build<ProjectService>().GetByKeyAsync("WEB")).Name.Should().Be("Admin fixed it");
    }

    [Fact]
    public async Task A_non_owner_member_cannot_delete_the_project()
    {
        _fx.CurrentUser.UserId = "user-2";
        _fx.Tenant.Set(_orgId, "alpha", OrgRole.Member);

        var act = async () => await _fx.Build<ProjectService>().DeleteAsync(_projectId);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CanEdit_reflects_ownership_and_admin_rights()
    {
        var projects = _fx.Build<ProjectService>();

        _fx.CurrentUser.UserId = "user-1";
        _fx.Tenant.Set(_orgId, "alpha", OrgRole.Member);
        (await projects.CanEditAsync(_projectId)).Should().BeTrue("the owner may edit");

        _fx.CurrentUser.UserId = "user-2";
        (await projects.CanEditAsync(_projectId)).Should().BeFalse("a plain member may not");

        _fx.Tenant.Set(_orgId, "alpha", OrgRole.Admin);
        (await projects.CanEditAsync(_projectId)).Should().BeTrue("an admin may");
    }

    [Fact]
    public async Task A_new_project_is_owned_by_whoever_created_it()
    {
        _fx.CurrentUser.UserId = "user-1";
        _fx.Tenant.Set(_orgId, "alpha", OrgRole.Admin);

        var created = await _fx.Build<ProjectService>()
            .CreateAsync(new CreateProjectRequest { Key = "OPS", Name = "Operations" });

        created.LeadUserId.Should().Be("user-1");
    }

    // ---- Workspace settings ----------------------------------------------------

    [Fact]
    public async Task Only_an_admin_can_rename_the_workspace()
    {
        _fx.Tenant.Set(_orgId, "alpha", OrgRole.Member);
        var act = async () => await _fx.Build<OrganizationService>().RenameAsync("Nope");
        await act.Should().ThrowAsync<ForbiddenException>();

        _fx.Tenant.Set(_orgId, "alpha", OrgRole.Admin);
        await _fx.Build<OrganizationService>().RenameAsync("Alpha Team");

        await using var db = _fx.Db();
        (await db.IgnoringTenantFilter<Organization>().FirstAsync(o => o.Id == _orgId)).Name.Should().Be("Alpha Team");
    }

    public void Dispose() => _fx.Dispose();
}
