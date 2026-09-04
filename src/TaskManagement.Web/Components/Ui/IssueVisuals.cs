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

    public static string AttachmentIcon(string fileName) => Extension(fileName) switch
    {
        ".xlsx" or ".xls" or ".csv" => "table-cells",
        ".pdf" => "document-text",
        ".doc" or ".docx" or ".rtf" or ".txt" => "document",
        ".zip" or ".rar" or ".7z" or ".gz" => "archive-box",
        ".ppt" or ".pptx" => "document-chart-bar",
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".svg" => "photo",
        ".sql" or ".json" or ".xml" or ".js" or ".ts" or ".cs" or ".py" or ".html" or ".css" or ".yml" or ".yaml" => "code-bracket",
        _ => "paper-clip",
    };

    public static string AttachmentIconColor(string fileName) => Extension(fileName) switch
    {
        ".xlsx" or ".xls" or ".csv" => "text-emerald-600 dark:text-emerald-400",
        ".pdf" => "text-rose-600 dark:text-rose-400",
        ".doc" or ".docx" or ".rtf" or ".txt" => "text-blue-600 dark:text-blue-400",
        ".zip" or ".rar" or ".7z" or ".gz" => "text-amber-600 dark:text-amber-400",
        ".ppt" or ".pptx" => "text-orange-600 dark:text-orange-400",
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".svg" => "text-purple-600 dark:text-purple-400",
        ".sql" or ".json" or ".xml" or ".js" or ".ts" or ".cs" or ".py" or ".html" or ".css" or ".yml" or ".yaml" => "text-sky-600 dark:text-sky-400",
        _ => "text-slate-500 dark:text-slate-400",
    };

    private static string Extension(string fileName) => Path.GetExtension(fileName).ToLowerInvariant();
}
