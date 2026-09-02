using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Common;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain;
using TaskManagement.Domain.Authorization;
using TaskManagement.Domain.Issues;
using TaskManagement.Domain.Projects;

namespace TaskManagement.Application.Services;

public sealed class IssueService(
    IAppDbContext db,
    IUserDirectory users,
    PermissionGuard guard,
    IssueChangeProcessor changeProcessor)
{
    public async Task<IReadOnlyList<IssueListItemDto>> GetBacklogAsync(Guid projectId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ViewContent);
        var project = await RequireProjectAsync(projectId, ct);

        var issues = await db.Issues
            .Where(i => i.ProjectId == projectId && i.SprintId == null && i.Status != IssueStatus.Done)
            .OrderBy(i => i.BoardRank)
            .ToListAsync(ct);

        return await ToListItemsAsync(issues, project.Key, ct);
    }

    public async Task<IssueDetailDto> GetAsync(Guid issueId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ViewContent);

        var issue = await db.Issues
            .Include(i => i.Comments)
            .Include(i => i.Attachments)
            .FirstOrDefaultAsync(i => i.Id == issueId, ct)
            ?? throw NotFoundException.For<Issue>(issueId);

        var project = await RequireProjectAsync(issue.ProjectId, ct);
        var activity = await db.ActivityLogs
            .Where(a => a.IssueId == issueId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        var sprint = issue.SprintId is null ? null : await db.Sprints.FindAsync([issue.SprintId], ct);

        var userIds = issue.Comments.Select(c => c.AuthorUserId)
            .Concat(activity.Select(a => a.ActorUserId))
            .Append(issue.ReporterUserId)
            .Concat(issue.AssigneeUserId is null ? [] : [issue.AssigneeUserId])
            .Distinct();
        var directory = await users.GetManyAsync(userIds, ct);

        string Name(string id) => directory.TryGetValue(id, out var u) ? u.DisplayName : "Unknown";
        string Color(string id) => directory.TryGetValue(id, out var u) ? u.AvatarColor : "#64748b";

        return new IssueDetailDto(
            issue.Id, issue.ProjectId, project.Key, $"{project.Key}-{issue.Number}",
            issue.Title, issue.Description, issue.Type, issue.Status, issue.Priority, issue.StoryPoints,
            issue.ReporterUserId, Name(issue.ReporterUserId),
            issue.AssigneeUserId, issue.AssigneeUserId is null ? null : Name(issue.AssigneeUserId),
            issue.SprintId, sprint?.Name,
            issue.CreatedAt, issue.UpdatedAt,
            issue.Comments.OrderBy(c => c.CreatedAt)
                .Select(c => new CommentDto(c.Id, c.AuthorUserId, Name(c.AuthorUserId), Color(c.AuthorUserId), c.Body, c.CreatedAt, c.EditedAt))
                .ToList(),
            activity.Select(a => new ActivityDto(a.Id, a.ActorUserId, Name(a.ActorUserId), a.Field, a.OldValue, a.NewValue, a.CreatedAt)).ToList(),
            issue.Attachments.OrderBy(a => a.CreatedAt)
                .Select(a => new AttachmentDto(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedByUserId, a.CreatedAt))
                .ToList());
    }

    public async Task<Guid> CreateAsync(CreateIssueRequest request, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.CreateIssue);
        var actor = guard.UserId;

        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.OrganizationId == guard.OrganizationId, ct)
            ?? throw NotFoundException.For<Project>(request.ProjectId);

        var issue = project.CreateIssue(request.Title, request.Type, actor);
        issue.DequeueChanges();
        if (request.Description is not null) issue.Describe(request.Description, actor);
        if (request.Priority != IssuePriority.Medium) issue.ChangePriority(request.Priority, actor);
        if (request.StoryPoints is not null) issue.Estimate(request.StoryPoints, actor);
        if (request.SprintId is not null) issue.AssignToSprint(request.SprintId, actor);
        if (request.AssigneeUserId is not null) issue.Assign(request.AssigneeUserId, actor);

        db.Issues.Add(issue);
        await changeProcessor.ProcessAsync(issue, project.Key, actor, ct);

        return issue.Id;
    }

    public async Task UpdateAsync(Guid issueId, UpdateIssueRequest request, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.EditIssue);
        var actor = guard.UserId;

        var issue = await db.Issues.FirstOrDefaultAsync(i => i.Id == issueId, ct)
            ?? throw NotFoundException.For<Issue>(issueId);
        var project = await RequireProjectAsync(issue.ProjectId, ct);

        issue.Rename(request.Title, actor);
        issue.Describe(request.Description, actor);
        issue.ChangeType(request.Type, actor);
        issue.ChangePriority(request.Priority, actor);
        issue.Estimate(request.StoryPoints, actor);
        issue.AssignToSprint(request.SprintId, actor);
        issue.Assign(request.AssigneeUserId, actor);

        await changeProcessor.ProcessAsync(issue, project.Key, actor, ct);
    }

    public async Task ChangeStatusAsync(Guid issueId, IssueStatus status, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.EditIssue);
        var actor = guard.UserId;

        var issue = await db.Issues.FirstOrDefaultAsync(i => i.Id == issueId, ct)
            ?? throw NotFoundException.For<Issue>(issueId);
        var project = await RequireProjectAsync(issue.ProjectId, ct);

        issue.ChangeStatus(status, actor);
        await changeProcessor.ProcessAsync(issue, project.Key, actor, ct);
    }

    public async Task AssignAsync(Guid issueId, string? assigneeUserId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.EditIssue);
        var actor = guard.UserId;

        var issue = await db.Issues.FirstOrDefaultAsync(i => i.Id == issueId, ct)
            ?? throw NotFoundException.For<Issue>(issueId);
        var project = await RequireProjectAsync(issue.ProjectId, ct);

        issue.Assign(assigneeUserId, actor);
        await changeProcessor.ProcessAsync(issue, project.Key, actor, ct);
    }

    public async Task<CommentDto> AddCommentAsync(AddCommentRequest request, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.CommentOnIssue);
        var actor = guard.UserId;

        var issue = await db.Issues.FirstOrDefaultAsync(i => i.Id == request.IssueId, ct)
            ?? throw NotFoundException.For<Issue>(request.IssueId);
        var project = await RequireProjectAsync(issue.ProjectId, ct);

        var comment = issue.AddComment(actor, request.Body);
        await changeProcessor.ProcessAsync(issue, project.Key, actor, ct);

        var author = await users.GetAsync(actor, ct);
        return new CommentDto(comment.Id, actor, author?.DisplayName ?? "You", author?.AvatarColor ?? "#64748b",
            comment.Body, comment.CreatedAt, comment.EditedAt);
    }

    public async Task DeleteAsync(Guid issueId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.DeleteIssue);

        var issue = await db.Issues.FirstOrDefaultAsync(i => i.Id == issueId, ct)
            ?? throw NotFoundException.For<Issue>(issueId);

        db.Issues.Remove(issue);
        await db.SaveChangesAsync(ct);
    }

    internal async Task<Project> RequireProjectAsync(Guid projectId, CancellationToken ct)
        => await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.OrganizationId == guard.OrganizationId, ct)
           ?? throw NotFoundException.For<Project>(projectId);

    internal async Task<IReadOnlyList<IssueListItemDto>> ToListItemsAsync(IReadOnlyList<Issue> issues, string projectKey, CancellationToken ct)
    {
        var directory = await users.GetManyAsync(issues.Where(i => i.AssigneeUserId is not null).Select(i => i.AssigneeUserId!), ct);

        return issues.Select(i =>
        {
            UserSummary? assignee = null;
            if (i.AssigneeUserId is not null) directory.TryGetValue(i.AssigneeUserId, out assignee);
            return new IssueListItemDto(
                i.Id, $"{projectKey}-{i.Number}", i.Title, i.Type, i.Status, i.Priority, i.StoryPoints,
                i.AssigneeUserId, assignee?.DisplayName, assignee?.AvatarColor, i.SprintId, i.BoardRank);
        }).ToList();
    }
}
