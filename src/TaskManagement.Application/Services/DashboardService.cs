using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain;
using TaskManagement.Domain.Issues;
using TaskManagement.Domain.Organizations;

namespace TaskManagement.Application.Services;

/// <summary>
/// Builds the signed-in user's home dashboard. Everything here is scoped to that user across *all* the
/// workspaces they belong to, so it deliberately runs outside the per-tenant query filter.
/// </summary>
public sealed class DashboardService(IAppDbContextFactory dbf, ICurrentUser currentUser, IClock clock)
{
    private const int MyTasksLimit = 8;

    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        await using var db = dbf.CreateDbContext();

        var workspaces = await db.IgnoringTenantFilter<Organization>()
            .Where(o => o.Members.Any(m => m.UserId == userId))
            .OrderBy(o => o.Name)
            .Select(o => new
            {
                o.Id,
                o.Name,
                o.Slug,
                Role = o.Members.Where(m => m.UserId == userId).Select(m => (OrgRole?)m.Role).FirstOrDefault(),
                MemberCount = o.Members.Count,
            })
            .ToListAsync(ct);

        var orgIds = workspaces.Select(w => w.Id).ToList();

        var projectCount = await db.IgnoringTenantFilter<Domain.Projects.Project>()
            .CountAsync(p => orgIds.Contains(p.OrganizationId), ct);

        // Everything assigned to me across those workspaces, projected once and aggregated in memory —
        // the row count per user is small and it keeps the overdue rule in one place.
        var mine = await db.IgnoringTenantFilter<Issue>()
            .Where(i => i.AssigneeUserId == userId && orgIds.Contains(i.OrganizationId))
            .Select(i => new
            {
                i.Id,
                i.Number,
                i.Title,
                i.Status,
                i.Priority,
                i.DueDate,
                i.ProjectId,
                i.OrganizationId,
            })
            .ToListAsync(ct);

        bool IsOverdue(IssueStatus status, DateOnly? due)
            => status != IssueStatus.Done && due is { } d && d < today;

        var stats = new DashboardStatsDto(
            Done: mine.Count(i => i.Status == IssueStatus.Done),
            InProgress: mine.Count(i => i.Status is IssueStatus.InProgress or IssueStatus.InReview),
            Overdue: mine.Count(i => IsOverdue(i.Status, i.DueDate)),
            Projects: projectCount,
            Workspaces: workspaces.Count,
            OpenTotal: mine.Count(i => i.Status != IssueStatus.Done));

        // Overdue first, then soonest due, then highest priority.
        var shortlist = mine
            .Where(i => i.Status != IssueStatus.Done)
            .OrderByDescending(i => IsOverdue(i.Status, i.DueDate))
            .ThenBy(i => i.DueDate ?? DateOnly.MaxValue)
            .ThenByDescending(i => i.Priority)
            .Take(MyTasksLimit)
            .ToList();

        var projectIds = shortlist.Select(i => i.ProjectId).Distinct().ToList();
        var projects = await db.IgnoringTenantFilter<Domain.Projects.Project>()
            .Where(p => projectIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Key, p.Name })
            .ToDictionaryAsync(p => p.Id, ct);
        var slugByOrg = workspaces.ToDictionary(w => w.Id, w => w.Slug);

        var myTasks = shortlist.Select(i =>
        {
            projects.TryGetValue(i.ProjectId, out var project);
            return new MyTaskDto(
                i.Id,
                $"{project?.Key ?? "?"}-{i.Number}",
                i.Title,
                i.Status,
                i.Priority,
                i.DueDate,
                IsOverdue(i.Status, i.DueDate),
                project?.Name ?? "Unknown project",
                slugByOrg.GetValueOrDefault(i.OrganizationId, ""));
        }).ToList();

        var workspaceDtos = workspaces
            .Select(w => new OrganizationDto(w.Id, w.Name, w.Slug, w.Role ?? OrgRole.Viewer, w.MemberCount))
            .ToList();

        return new DashboardDto(stats, myTasks, workspaceDtos);
    }
}
