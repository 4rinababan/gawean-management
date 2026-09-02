using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Common;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain;
using TaskManagement.Domain.Authorization;
using TaskManagement.Domain.Issues;
using TaskManagement.Domain.Notifications;
using TaskManagement.Domain.Sprints;

namespace TaskManagement.Application.Services;

public sealed class SprintService(
    IAppDbContextFactory dbf,
    PermissionGuard guard,
    INotificationRealtime realtime)
{
    public async Task<IReadOnlyList<SprintDto>> GetForProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ViewContent);
        await using var db = dbf.CreateDbContext();
        await IssueService.RequireProjectAsync(db, projectId, guard.OrganizationId, ct);

        var sprints = await db.Sprints
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.State == SprintState.Active)
            .ThenByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        var stats = await db.Issues
            .Where(i => i.ProjectId == projectId && i.SprintId != null)
            .GroupBy(i => i.SprintId!.Value)
            .Select(g => new
            {
                SprintId = g.Key,
                Count = g.Count(),
                Total = g.Sum(i => i.StoryPoints ?? 0),
                Completed = g.Where(i => i.Status == IssueStatus.Done).Sum(i => i.StoryPoints ?? 0),
            })
            .ToDictionaryAsync(x => x.SprintId, ct);

        return sprints.Select(s =>
        {
            stats.TryGetValue(s.Id, out var st);
            return new SprintDto(s.Id, s.ProjectId, s.Name, s.Goal, s.State, s.StartDate, s.EndDate,
                st?.Count ?? 0, st?.Completed ?? 0, st?.Total ?? 0);
        }).ToList();
    }

    public async Task<Guid> CreateAsync(CreateSprintRequest request, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageSprints);
        await using var db = dbf.CreateDbContext();
        await IssueService.RequireProjectAsync(db, request.ProjectId, guard.OrganizationId, ct);

        var sprint = new Sprint(guard.OrganizationId, request.ProjectId, request.Name, request.Goal);
        db.Sprints.Add(sprint);
        await db.SaveChangesAsync(ct);
        return sprint.Id;
    }

    public async Task UpdateAsync(Guid sprintId, UpdateSprintRequest request, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageSprints);
        await using var db = dbf.CreateDbContext();
        var sprint = await RequireSprintAsync(db, sprintId, ct);
        sprint.Update(request.Name, request.Goal);
        await db.SaveChangesAsync(ct);
    }

    public async Task StartAsync(Guid sprintId, StartSprintRequest request, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageSprints);
        await using var db = dbf.CreateDbContext();
        var sprint = await RequireSprintAsync(db, sprintId, ct);

        if (await db.Sprints.AnyAsync(s => s.ProjectId == sprint.ProjectId && s.State == SprintState.Active, ct))
            throw new ConflictException("This project already has an active sprint. Complete it before starting another.");

        sprint.Start(request.StartDate, request.EndDate);
        await db.SaveChangesAsync(ct);

        var assignees = await db.Issues
            .Where(i => i.SprintId == sprintId && i.AssigneeUserId != null)
            .Select(i => i.AssigneeUserId!)
            .Distinct()
            .ToListAsync(ct);

        foreach (var userId in assignees)
        {
            db.Notifications.Add(new Notification(guard.OrganizationId, userId, NotificationType.SprintStarted,
                $"Sprint \"{sprint.Name}\" has started."));
        }

        await db.SaveChangesAsync(ct);
        foreach (var userId in assignees)
            await realtime.NotifyAsync(userId, ct);
    }

    public async Task CompleteAsync(Guid sprintId, Guid? moveUnfinishedToSprintId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageSprints);
        await using var db = dbf.CreateDbContext();
        var sprint = await RequireSprintAsync(db, sprintId, ct);
        sprint.Complete();

        var unfinished = await db.Issues
            .Where(i => i.SprintId == sprintId && i.Status != IssueStatus.Done)
            .ToListAsync(ct);

        foreach (var issue in unfinished)
            issue.AssignToSprint(moveUnfinishedToSprintId, guard.UserId);

        await db.SaveChangesAsync(ct);
    }

    public async Task<BurndownDto> GetBurndownAsync(Guid sprintId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ViewContent);
        await using var db = dbf.CreateDbContext();
        var sprint = await RequireSprintAsync(db, sprintId, ct);

        if (sprint.StartDate is not { } start || sprint.EndDate is not { } end)
            return new BurndownDto(sprint.Id, sprint.Name, []);

        var sprintIssues = await db.Issues
            .Where(i => i.SprintId == sprintId)
            .Select(i => new { i.Id, i.StoryPoints, i.Status })
            .ToListAsync(ct);

        var total = sprintIssues.Sum(i => i.StoryPoints ?? 0);

        var issueIds = sprintIssues.Select(i => i.Id).ToList();
        var doneEvents = await db.ActivityLogs
            .Where(a => issueIds.Contains(a.IssueId) && a.Field == nameof(Issue.Status) && a.NewValue == nameof(IssueStatus.Done))
            .Select(a => new { a.IssueId, a.CreatedAt })
            .ToListAsync(ct);

        var pointsByIssue = sprintIssues.ToDictionary(i => i.Id, i => (double)(i.StoryPoints ?? 0));
        var events = doneEvents
            .GroupBy(e => e.IssueId)
            .Select(g => new BurndownEvent(
                DateOnly.FromDateTime(g.Max(x => x.CreatedAt).UtcDateTime),
                pointsByIssue.GetValueOrDefault(g.Key)))
            .ToList();

        var points = Burndown.Build(start, end, total, events);
        return new BurndownDto(sprint.Id, sprint.Name, points);
    }

    private async Task<Sprint> RequireSprintAsync(IAppDbContext db, Guid sprintId, CancellationToken ct)
        => await db.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId && s.OrganizationId == guard.OrganizationId, ct)
           ?? throw NotFoundException.For<Sprint>(sprintId);
}
