using TaskManagement.Domain;

namespace TaskManagement.Web.Components.Ui;

/// <summary>Shared colour / label / icon mappings for issue enums so every screen renders them identically.</summary>
public static class IssueVisuals
{
    public static Badge.BadgeColor StatusColor(IssueStatus status) => status switch
    {
        IssueStatus.Backlog => Badge.BadgeColor.Slate,
        IssueStatus.Todo => Badge.BadgeColor.Blue,
        IssueStatus.InProgress => Badge.BadgeColor.Amber,
        IssueStatus.InReview => Badge.BadgeColor.Purple,
        IssueStatus.Done => Badge.BadgeColor.Green,
        _ => Badge.BadgeColor.Slate,
    };

    public static string StatusLabel(IssueStatus status) => status switch
    {
        IssueStatus.InProgress => "In Progress",
        IssueStatus.InReview => "In Review",
        _ => status.ToString(),
    };

    public static Badge.BadgeColor PriorityColor(IssuePriority priority) => priority switch
    {
        IssuePriority.Highest or IssuePriority.High => Badge.BadgeColor.Rose,
        IssuePriority.Medium => Badge.BadgeColor.Amber,
        _ => Badge.BadgeColor.Slate,
    };

    public static string TypeIcon(IssueType type) => type switch
    {
        IssueType.Story => "text-emerald-500",
        IssueType.Bug => "text-rose-500",
        IssueType.Epic => "text-purple-500",
        IssueType.SubTask => "text-sky-500",
        _ => "text-blue-500",
    };

    public static string TypeSymbol(IssueType type) => type switch
    {
        IssueType.Story => "▮",
        IssueType.Bug => "●",
        IssueType.Epic => "◆",
        IssueType.SubTask => "▸",
        _ => "▪",
    };
}
