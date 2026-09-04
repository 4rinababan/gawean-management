using System.Text.RegularExpressions;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain.Issues;

namespace TaskManagement.Application.Services;

/// <summary>
/// Renders raw <see cref="ActivityLog"/> rows for humans. The log stores whatever the aggregate changed —
/// property names and raw ids — so this is where "AssigneeUserId: da29604f-… → 10a8438b-…" becomes
/// "assignee: Ari Nababan → Bang Boy".
/// </summary>
internal static partial class ActivityFormatter
{
    private static readonly Dictionary<string, string> FieldLabels = new(StringComparer.Ordinal)
    {
        [nameof(Issue.Title)] = "summary",
        [nameof(Issue.Description)] = "description",
        [nameof(Issue.Status)] = "status",
        [nameof(Issue.Priority)] = "priority",
        [nameof(Issue.Type)] = "type",
        [nameof(Issue.AssigneeUserId)] = "assignee",
        [nameof(Issue.StoryPoints)] = "story points",
        [nameof(Issue.DueDate)] = "due date",
        [nameof(Issue.SprintId)] = "sprint",
        [nameof(Comment)] = "comment",
        ["Viewer"] = "viewer",
    };

    public static ActivityDto ToDto(
        ActivityLog log,
        Func<string, string> displayName,
        IReadOnlyDictionary<Guid, string> sprintNames)
    {
        var isComment = log.Field == nameof(Comment);

        return new ActivityDto(
            log.Id,
            log.ActorUserId,
            displayName(log.ActorUserId),
            FieldLabels.GetValueOrDefault(log.Field, Humanize(log.Field)),
            isComment ? null : RenderValue(log.Field, log.OldValue, displayName, sprintNames),
            isComment ? null : RenderValue(log.Field, log.NewValue, displayName, sprintNames),
            isComment,
            log.CreatedAt);
    }

    private static string? RenderValue(
        string field,
        string? value,
        Func<string, string> displayName,
        IReadOnlyDictionary<Guid, string> sprintNames)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        return field switch
        {
            nameof(Issue.AssigneeUserId) or "Viewer" => displayName(value),
            nameof(Issue.SprintId) => Guid.TryParse(value, out var id)
                ? sprintNames.GetValueOrDefault(id, "a sprint")
                : value,
            nameof(Issue.Status) or nameof(Issue.Type) or nameof(Issue.Priority) => Humanize(value),
            nameof(Issue.Description) or nameof(Issue.Title) => Truncate(value, 80),
            _ => value,
        };
    }

    /// <summary>"InProgress" -> "In Progress", "StoryPoints" -> "Story Points".</summary>
    private static string Humanize(string value)
        => PascalBoundary().Replace(value, " $1");

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max].TrimEnd() + "…";

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex PascalBoundary();
}
