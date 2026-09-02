using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Common;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain;
using TaskManagement.Domain.Authorization;
using TaskManagement.Domain.Common;
using TaskManagement.Domain.Issues;
using TaskManagement.Domain.Sprints;

namespace TaskManagement.Application.Services;

/// <summary>Reads the Kanban board for a project's active sprint and applies drag-and-drop reordering via lexicographic ranks.</summary>
public sealed class BoardService(
    IAppDbContextFactory dbf,
    IUserDirectory users,
    PermissionGuard guard,
    IssueChangeProcessor changeProcessor)
{
    private static readonly IssueStatus[] ColumnOrder =
        [IssueStatus.Todo, IssueStatus.InProgress, IssueStatus.InReview, IssueStatus.Done];

    private static string ColumnTitle(IssueStatus status) => status switch
    {
        IssueStatus.Todo => "To Do",
        IssueStatus.InProgress => "In Progress",
        IssueStatus.InReview => "In Review",
        IssueStatus.Done => "Done",
        _ => status.ToString(),
    };

    public async Task<BoardDto> GetAsync(Guid projectId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ViewContent);
        await using var db = dbf.CreateDbContext();
        var project = await IssueService.RequireProjectAsync(db, projectId, guard.OrganizationId, ct);

        var activeSprint = await db.Sprints
            .Where(s => s.ProjectId == projectId && s.State == SprintState.Active)
            .FirstOrDefaultAsync(ct);

        var query = db.Issues.Where(i => i.ProjectId == projectId);
        query = activeSprint is not null
            ? query.Where(i => i.SprintId == activeSprint.Id)
            : query.Where(i => i.Status != IssueStatus.Backlog);

        var boardIssues = await query.OrderBy(i => i.BoardRank).ToListAsync(ct);
        var items = await IssueMapper.ToListItemsAsync(boardIssues, project.Key, users, ct);

        var columns = ColumnOrder
            .Select(status => new BoardColumnDto(
                status, ColumnTitle(status),
                items.Where(i => i.Status == status).ToList()))
            .ToList();

        return new BoardDto(projectId, project.Key, activeSprint?.Id, activeSprint?.Name, columns);
    }

    /// <summary>Moves an issue to <paramref name="request"/>.TargetStatus, ranked between the two neighbour issues it was dropped between.</summary>
    public async Task MoveAsync(MoveIssueRequest request, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.EditIssue);
        var actor = guard.UserId;
        await using var db = dbf.CreateDbContext();

        var issue = await db.Issues.FirstOrDefaultAsync(i => i.Id == request.IssueId, ct)
            ?? throw NotFoundException.For<Issue>(request.IssueId);
        var project = await IssueService.RequireProjectAsync(db, issue.ProjectId, guard.OrganizationId, ct);

        var before = await RankOf(db, request.BeforeIssueId, ct);
        var after = await RankOf(db, request.AfterIssueId, ct);
        var newRank = LexoRank.Between(before, after);

        issue.MoveOnBoard(request.TargetStatus, newRank, actor);
        await changeProcessor.ProcessAsync(db, issue, project.Key, actor, ct);
    }

    private static async Task<string?> RankOf(IAppDbContext db, Guid? issueId, CancellationToken ct)
        => issueId is null ? null : (await db.Issues.Where(i => i.Id == issueId).Select(i => i.BoardRank).FirstOrDefaultAsync(ct));
}
