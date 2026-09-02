using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain.Issues;

namespace TaskManagement.Application.Services;

/// <summary>Projects loaded <see cref="Issue"/> aggregates to list DTOs, resolving assignee display data in one directory lookup.</summary>
internal static class IssueMapper
{
    public static async Task<IReadOnlyList<IssueListItemDto>> ToListItemsAsync(
        IReadOnlyList<Issue> issues, string projectKey, IUserDirectory users, CancellationToken ct)
    {
        var directory = await users.GetManyAsync(
            issues.Where(i => i.AssigneeUserId is not null).Select(i => i.AssigneeUserId!), ct);

        return issues.Select(i =>
        {
            UserSummary? assignee = null;
            if (i.AssigneeUserId is not null)
                directory.TryGetValue(i.AssigneeUserId, out assignee);

            return new IssueListItemDto(
                i.Id, $"{projectKey}-{i.Number}", i.Title, i.Type, i.Status, i.Priority, i.StoryPoints,
                i.AssigneeUserId, assignee?.DisplayName, assignee?.AvatarColor, i.SprintId, i.BoardRank);
        }).ToList();
    }
}
