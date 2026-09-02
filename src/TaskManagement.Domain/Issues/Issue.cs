using TaskManagement.Domain.Common;
using TaskManagement.Domain.Projects;

namespace TaskManagement.Domain.Issues;

/// <summary>The core work item. Records <see cref="IssueChange"/> entries for every mutation so the app layer can log activity and notify.</summary>
public class Issue : Entity, ITenantScoped
{
    private readonly List<Comment> _comments = [];
    private readonly List<Attachment> _attachments = [];
    private readonly List<IssueChange> _changes = [];

    private Issue() { }

    internal Issue(Project project, string title, IssueType type, string reporterUserId)
    {
        OrganizationId = project.OrganizationId;
        ProjectId = project.Id;
        Number = project.NextIssueNumber();
        Title = Guard.NotBlank(title, nameof(title));
        Type = type;
        ReporterUserId = Guard.NotBlank(reporterUserId, nameof(reporterUserId));
        Status = IssueStatus.Backlog;
        Priority = IssuePriority.Medium;
        BoardRank = LexoRank.Between(null, null);
    }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    /// <summary>Sequential number within the project. Combined with the project key for display: <c>{Key}-{Number}</c>.</summary>
    public int Number { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public IssueType Type { get; private set; }

    public IssueStatus Status { get; private set; }

    public IssuePriority Priority { get; private set; }

    public string ReporterUserId { get; private set; } = string.Empty;

    public string? AssigneeUserId { get; private set; }

    public int? StoryPoints { get; private set; }

    public Guid? SprintId { get; private set; }

    public Guid? ParentIssueId { get; private set; }

    /// <summary>Lexicographic sort key for the board column the issue currently sits in.</summary>
    public string BoardRank { get; private set; } = LexoRank.Min;

    public IReadOnlyCollection<Comment> Comments => _comments.AsReadOnly();

    public IReadOnlyCollection<Attachment> Attachments => _attachments.AsReadOnly();

    /// <summary>Field-level changes accumulated since the entity was loaded; consumed and cleared by the application layer.</summary>
    public IReadOnlyCollection<IssueChange> DequeueChanges()
    {
        var snapshot = _changes.ToArray();
        _changes.Clear();
        return snapshot;
    }

    public void Rename(string title, string actorUserId)
    {
        title = Guard.NotBlank(title, nameof(title));
        Record(nameof(Title), Title, title, actorUserId);
        Title = title;
    }

    public void Describe(string? description, string actorUserId)
    {
        description = description?.Trim();
        Record(nameof(Description), Description, description, actorUserId);
        Description = description;
    }

    public void ChangeType(IssueType type, string actorUserId)
    {
        Record(nameof(Type), Type.ToString(), type.ToString(), actorUserId);
        Type = type;
    }

    public void ChangePriority(IssuePriority priority, string actorUserId)
    {
        Record(nameof(Priority), Priority.ToString(), priority.ToString(), actorUserId);
        Priority = priority;
    }

    public void Estimate(int? storyPoints, string actorUserId)
    {
        if (storyPoints is < 0 or > 100)
            throw new DomainException("Story points must be between 0 and 100.");

        Record(nameof(StoryPoints), StoryPoints?.ToString(), storyPoints?.ToString(), actorUserId);
        StoryPoints = storyPoints;
    }

    public void Assign(string? assigneeUserId, string actorUserId)
    {
        Record(nameof(AssigneeUserId), AssigneeUserId, assigneeUserId, actorUserId);
        AssigneeUserId = assigneeUserId;
    }

    public void ChangeStatus(IssueStatus status, string actorUserId)
    {
        Record(nameof(Status), Status.ToString(), status.ToString(), actorUserId);
        Status = status;
    }

    public void MoveOnBoard(IssueStatus status, string rank, string actorUserId)
    {
        BoardRank = Guard.NotBlank(rank, nameof(rank));
        if (Status != status)
            ChangeStatus(status, actorUserId);
        else
            UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AssignToSprint(Guid? sprintId, string actorUserId)
    {
        Record(nameof(SprintId), SprintId?.ToString(), sprintId?.ToString(), actorUserId);
        SprintId = sprintId;
    }

    public Comment AddComment(string authorUserId, string body)
    {
        var comment = new Comment(Id, OrganizationId, authorUserId, body);
        _comments.Add(comment);
        _changes.Add(new IssueChange(Id, nameof(Comment), null, comment.Body, authorUserId));
        UpdatedAt = DateTimeOffset.UtcNow;
        return comment;
    }

    public Attachment AddAttachment(string uploadedByUserId, string fileName, string contentType, long sizeBytes, string storageKey)
    {
        var attachment = new Attachment(Id, OrganizationId, uploadedByUserId, fileName, contentType, sizeBytes, storageKey);
        _attachments.Add(attachment);
        UpdatedAt = DateTimeOffset.UtcNow;
        return attachment;
    }

    private void Record(string field, string? oldValue, string? newValue, string actorUserId)
    {
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
            return;

        _changes.Add(new IssueChange(Id, field, oldValue, newValue, Guard.NotBlank(actorUserId, nameof(actorUserId))));
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
