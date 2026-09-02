using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain;
using TaskManagement.Domain.Organizations;
using TaskManagement.Domain.Projects;

namespace TaskManagement.Infrastructure.Tests;

/// <summary>
/// The attachment endpoint authorises by hand (it runs outside a Blazor circuit, so it can't use
/// PermissionGuard). These cover the query it relies on: an attachment is reachable only by a member
/// of the owning organisation, and only under that organisation's own slug.
/// </summary>
public class AttachmentAccessTests : IDisposable
{
    private readonly SqliteHarness _harness = new();
    private Guid _attachmentId;

    public AttachmentAccessTests()
    {
        using var db = _harness.CreateContext();

        var owner = new Organization("Alpha", "alpha", "member-1");
        var other = new Organization("Beta", "beta", "outsider-1");
        var project = new Project(owner.Id, "WEB", "Web");
        var issue = project.CreateIssue("T", IssueType.Task, "member-1");
        var attachment = issue.AddAttachment("member-1", "diagram.png", "image/png", 1024, "2026/09/abc.png");

        db.Organizations.AddRange(owner, other);
        db.Projects.Add(project);
        db.Issues.Add(issue);
        db.Attachments.Add(attachment);
        db.SaveChanges();

        _attachmentId = attachment.Id;
    }

    /// <summary>Mirrors the endpoint's authorisation query.</summary>
    private async Task<bool> CanReachAsync(string userId, string slug)
    {
        await using var db = _harness.CreateContext();

        var attachment = await db.Attachments.IgnoreQueryFilters()
            .Where(a => a.Id == _attachmentId)
            .Select(a => new { a.OrganizationId })
            .FirstOrDefaultAsync();

        if (attachment is null) return false;

        return await db.Organizations.IgnoreQueryFilters()
            .AnyAsync(o => o.Id == attachment.OrganizationId
                           && o.Slug == slug
                           && o.Members.Any(m => m.UserId == userId));
    }

    [Fact]
    public async Task A_member_of_the_owning_workspace_can_read_it()
        => (await CanReachAsync("member-1", "alpha")).Should().BeTrue();

    [Fact]
    public async Task Someone_outside_the_workspace_cannot()
        => (await CanReachAsync("outsider-1", "alpha")).Should().BeFalse();

    [Fact]
    public async Task A_member_of_another_workspace_cannot_reach_it_through_their_own_slug()
        => (await CanReachAsync("outsider-1", "beta")).Should().BeFalse();

    [Fact]
    public async Task A_member_cannot_reach_it_through_the_wrong_slug()
        => (await CanReachAsync("member-1", "beta")).Should().BeFalse();

    public void Dispose() => _harness.Dispose();
}
