using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Issues;

/// <summary>Persisted audit trail entry for an issue. Created by the application layer from an <see cref="IssueChange"/>.</summary>
public class ActivityLog : Entity, ITenantScoped
{
    private ActivityLog() { }

    public ActivityLog(Guid organizationId, Guid issueId, string actorUserId, string field, string? oldValue, string? newValue)
    {
        OrganizationId = organizationId;
        IssueId = issueId;
        ActorUserId = Guard.NotBlank(actorUserId, nameof(actorUserId));
        Field = Guard.NotBlank(field, nameof(field));
        OldValue = oldValue;
        NewValue = newValue;
    }

    public Guid OrganizationId { get; private set; }

    public Guid IssueId { get; private set; }

    public string ActorUserId { get; private set; } = string.Empty;

    public string Field { get; private set; } = string.Empty;

    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }
}
