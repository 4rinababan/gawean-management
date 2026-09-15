using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain.Issues;

namespace TaskManagement.Application.Services;

/// <summary>Projects loaded <see cref="Issue"/> aggregates to list DTOs, resolving assignee/reporter/viewer
/// display data and any linked wiki page in a handful of batched lookups rather than per-issue queries.</summary>
internal static class IssueMapper
{
    public static async Task<IReadOnlyList<IssueListItemDto>> ToListItemsAsync(
        IAppDbContext db, IReadOnlyList<Issue> issues, string projectKey, IUserDirectory users, CancellationToken ct, DateOnly? today = null)
    {
        var asOf = today ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var peopleIds = issues
            .SelectMany(i => (i.AssigneeUserId is null ? [] : new[] { i.AssigneeUserId })
                .Concat([i.ReporterUserId])
                .Concat(i.Viewers.Select(v => v.UserId)))
            .Distinct();
        var directory = await users.GetManyAsync(peopleIds!, ct);

        var issueIds = issues.Select(i => i.Id).ToList();
        var linkedPages = await db.WikiPages
            .Where(w => w.IssueId != null && issueIds.Contains(w.IssueId!.Value))
            .Select(w => new { w.Id, Title = w.Title, IssueId = w.IssueId!.Value })
            .ToDictionaryAsync(w => w.IssueId, ct);

        return issues.Select(i =>
        {
            UserSummary? assignee = null;
            if (i.AssigneeUserId is not null)
                directory.TryGetValue(i.AssigneeUserId, out assignee);

            directory.TryGetValue(i.ReporterUserId, out var reporter);

            var viewers = i.Viewers
                .Select(v => directory.TryGetValue(v.UserId, out var u)
                    ? new IssueMemberDto(v.UserId, u.DisplayName, u.AvatarColor)
                    : new IssueMemberDto(v.UserId, "Unknown", "#64748b"))
                .ToList();

            linkedPages.TryGetValue(i.Id, out var linkedPage);

            return new IssueListItemDto(
                i.Id, $"{projectKey}-{i.Number}", i.Title, i.Type, i.Status, i.Priority, i.StoryPoints,
                i.AssigneeUserId, assignee?.DisplayName, assignee?.AvatarColor, i.SprintId, i.BoardRank,
                i.DueDate, i.IsOverdue(asOf), i.Attachments.Count,
                i.ReporterUserId, reporter?.DisplayName ?? "Unknown", reporter?.AvatarColor ?? "#64748b",
                viewers, linkedPage?.Id, linkedPage?.Title);
        }).ToList();
    }
}
