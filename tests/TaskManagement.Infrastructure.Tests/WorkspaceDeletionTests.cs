using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TaskManagement.Application.Common;
using TaskManagement.Application.Contracts;
using TaskManagement.Application.Services;
using TaskManagement.Domain;
using TaskManagement.Domain.Organizations;
using TaskManagement.Domain.Projects;

namespace TaskManagement.Infrastructure.Tests;

/// <summary>
/// Deleting a workspace has to take everything with it. Projects and notifications had no foreign key
/// to Organization, so they would have been orphaned; these tests pin the cascade down to issues and
/// their comments, and check the guard rails around such a destructive action.
/// </summary>
public class WorkspaceDeletionTests : IDisposable
{
    private readonly ServiceFixture _fx = new();
    private readonly Guid _orgId;
    private readonly Guid _survivorOrgId;

    public WorkspaceDeletionTests()
    {
        using var db = _fx.Db();

        var doomed = new Organization("Doomed Co", "doomed", "user-1");
        var survivor = new Organization("Survivor Co", "survivor", "user-1");
        var project = new Project(doomed.Id, "WEB", "Web");
        var issue = project.CreateIssue("T", IssueType.Task, "user-1");
        var comment = issue.AddComment("user-1", "hello");
        var attachment = issue.AddAttachment("user-1", "f.png", "image/png", 10, "2026/09/f.png");
        var sprint = new Domain.Sprints.Sprint(doomed.Id, project.Id, "S1");
        var notification = new Domain.Notifications.Notification(
            doomed.Id, "user-1", NotificationType.IssueAssigned, "hi");

        db.Organizations.AddRange(doomed, survivor);
        db.Projects.Add(project);
        db.Issues.Add(issue);
        db.Comments.Add(comment);
        db.Attachments.Add(attachment);
        db.Sprints.Add(sprint);
        db.Notifications.Add(notification);
        db.SaveChanges();

        _orgId = doomed.Id;
        _survivorOrgId = survivor.Id;
        _fx.Tenant.Set(doomed.Id, "doomed", OrgRole.Admin);
    }

    [Fact]
    public async Task Deleting_a_workspace_removes_everything_inside_it()
    {
        await _fx.Build<OrganizationService>().DeleteAsync("Doomed Co");

        await using var db = _fx.Db();
        (await db.IgnoringTenantFilter<Organization>().CountAsync(o => o.Id == _orgId)).Should().Be(0);
        (await db.IgnoringTenantFilter<Project>().CountAsync()).Should().Be(0);
        (await db.IgnoringTenantFilter<Domain.Issues.Issue>().CountAsync()).Should().Be(0);
        (await db.IgnoringTenantFilter<Domain.Issues.Comment>().CountAsync()).Should().Be(0);
        (await db.IgnoringTenantFilter<Domain.Issues.Attachment>().CountAsync()).Should().Be(0);
        (await db.IgnoringTenantFilter<Domain.Sprints.Sprint>().CountAsync()).Should().Be(0);
        (await db.IgnoringTenantFilter<Domain.Notifications.Notification>().CountAsync()).Should().Be(0);
        (await db.IgnoringTenantFilter<OrganizationMember>().CountAsync(m => m.OrganizationId == _orgId)).Should().Be(0);
    }

    [Fact]
    public async Task Other_workspaces_are_untouched()
    {
        await _fx.Build<OrganizationService>().DeleteAsync("Doomed Co");

        await using var db = _fx.Db();
        (await db.IgnoringTenantFilter<Organization>().CountAsync(o => o.Id == _survivorOrgId)).Should().Be(1);
    }

    [Fact]
    public async Task The_stored_files_are_removed_too()
    {
        await _fx.Build<OrganizationService>().DeleteAsync("Doomed Co");

        await _fx.Storage.Received(1).DeleteAsync("2026/09/f.png", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_mistyped_name_does_not_delete_anything()
    {
        var act = async () => await _fx.Build<OrganizationService>().DeleteAsync("doomed co");

        await act.Should().ThrowAsync<ConflictException>();

        await using var db = _fx.Db();
        (await db.IgnoringTenantFilter<Organization>().CountAsync(o => o.Id == _orgId)).Should().Be(1);
    }

    [Fact]
    public async Task A_non_admin_cannot_delete_the_workspace()
    {
        _fx.Tenant.Set(_orgId, "doomed", OrgRole.Member);

        var act = async () => await _fx.Build<OrganizationService>().DeleteAsync("Doomed Co");

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    public void Dispose() => _fx.Dispose();
}
