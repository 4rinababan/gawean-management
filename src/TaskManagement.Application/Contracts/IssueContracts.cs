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
    public DateOnly? DueDate { get; set; }
}

/// <summary>
/// A suggested ticket from <see cref="Abstractions.IAiAssistant"/>. Every field is a proposal the
/// author reviews and may overwrite — nothing here is persisted until they submit the form.
/// </summary>
public sealed record IssueDraft(
    string Title,
    string? Description,
    IssueType Type,
    IssuePriority Priority,
    int? StoryPoints);

public sealed class UpdateIssueRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IssueType Type { get; set; }
    public IssuePriority Priority { get; set; }
    public string? AssigneeUserId { get; set; }
    public int? StoryPoints { get; set; }
    public Guid? SprintId { get; set; }
    public DateOnly? DueDate { get; set; }
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
    string BoardRank,
    DateOnly? DueDate,
    bool IsOverdue);

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
    string ReporterAvatarColor,
    string? AssigneeUserId,
    string? AssigneeDisplayName,
    DateOnly? DueDate,
    Guid? SprintId,
    string? SprintName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<CommentDto> Comments,
    IReadOnlyList<ActivityDto> Activity,
    IReadOnlyList<AttachmentDto> Attachments,
    IReadOnlyList<IssueMemberDto> Viewers,
    IReadOnlyList<IssueAiNoteDto> AiNotes);

/// <summary>A person referenced on an issue (currently: a viewer) resolved for display.</summary>
public sealed record IssueMemberDto(string UserId, string DisplayName, string AvatarColor);

/// <summary>A saved "Ask GAWE AI" question/answer pair for an issue.</summary>
public sealed record IssueAiNoteDto(Guid Id, string AskedByUserId, string AskedByDisplayName, string Question, string Answer, DateTimeOffset CreatedAt);

public sealed record CommentDto(Guid Id, string AuthorUserId, string AuthorDisplayName, string AuthorAvatarColor, string Body, DateTimeOffset CreatedAt, DateTimeOffset? EditedAt);

public sealed record AddCommentRequest(Guid IssueId, string Body);

/// <summary>
/// One activity-feed entry, already rendered for display: field and values are humanised
/// (ids resolved to names, enums spaced) by the application layer so the UI just prints them.
/// </summary>
public sealed record ActivityDto(
    Guid Id,
    string ActorUserId,
    string ActorDisplayName,
    string FieldLabel,
    string? OldLabel,
    string? NewLabel,
    bool IsComment,
    DateTimeOffset CreatedAt);

public sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, string UploadedByUserId, DateTimeOffset CreatedAt);

public sealed record BoardColumnDto(IssueStatus Status, string Title, IReadOnlyList<IssueListItemDto> Issues);

public sealed record BoardDto(Guid ProjectId, string ProjectKey, Guid? ActiveSprintId, string? ActiveSprintName, IReadOnlyList<BoardColumnDto> Columns);
