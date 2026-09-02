using TaskManagement.Domain;

namespace TaskManagement.Application.Contracts;

public sealed class CreateIssueRequest
{
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public IssueType Type { get; set; } = IssueType.Task;
    public string? Description { get; set; }
    public IssuePriority Priority { get; set; } = IssuePriority.Medium;
    public string? AssigneeUserId { get; set; }
    public Guid? SprintId { get; set; }
    public int? StoryPoints { get; set; }
}

public sealed class UpdateIssueRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IssueType Type { get; set; }
    public IssuePriority Priority { get; set; }
    public string? AssigneeUserId { get; set; }
    public int? StoryPoints { get; set; }
    public Guid? SprintId { get; set; }
}

public sealed record MoveIssueRequest(Guid IssueId, IssueStatus TargetStatus, Guid? BeforeIssueId, Guid? AfterIssueId);

public sealed record IssueListItemDto(
    Guid Id,
    string Reference,
    string Title,
    IssueType Type,
    IssueStatus Status,
    IssuePriority Priority,
    int? StoryPoints,
    string? AssigneeUserId,
    string? AssigneeDisplayName,
    string? AssigneeAvatarColor,
    Guid? SprintId,
    string BoardRank);

public sealed record IssueDetailDto(
    Guid Id,
    Guid ProjectId,
    string ProjectKey,
    string Reference,
    string Title,
    string? Description,
    IssueType Type,
    IssueStatus Status,
    IssuePriority Priority,
    int? StoryPoints,
    string ReporterUserId,
    string ReporterDisplayName,
    string? AssigneeUserId,
    string? AssigneeDisplayName,
    Guid? SprintId,
    string? SprintName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<CommentDto> Comments,
    IReadOnlyList<ActivityDto> Activity,
    IReadOnlyList<AttachmentDto> Attachments);

public sealed record CommentDto(Guid Id, string AuthorUserId, string AuthorDisplayName, string AuthorAvatarColor, string Body, DateTimeOffset CreatedAt, DateTimeOffset? EditedAt);

public sealed record AddCommentRequest(Guid IssueId, string Body);

public sealed record ActivityDto(Guid Id, string ActorUserId, string ActorDisplayName, string Field, string? OldValue, string? NewValue, DateTimeOffset CreatedAt);

public sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, string UploadedByUserId, DateTimeOffset CreatedAt);

public sealed record BoardColumnDto(IssueStatus Status, string Title, IReadOnlyList<IssueListItemDto> Issues);

public sealed record BoardDto(Guid ProjectId, string ProjectKey, Guid? ActiveSprintId, string? ActiveSprintName, IReadOnlyList<BoardColumnDto> Columns);
