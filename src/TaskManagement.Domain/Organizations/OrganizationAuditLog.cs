using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Organizations;

/// <summary>
/// One organization-level history entry — logins, role changes, membership changes, workspace
/// rename/delete. Distinct from <see cref="Issues.ActivityLog"/>, which is a per-issue field diff
/// and requires an <c>IssueId</c>; nothing here is scoped to a single issue.
/// </summary>
public class OrganizationAuditLog : Entity, ITenantScoped
{
    private OrganizationAuditLog() { }

    public OrganizationAuditLog(Guid organizationId, string actorUserId, string eventType, string detail, string? targetUserId = null)
    {
        OrganizationId = organizationId;
        ActorUserId = Guard.NotBlank(actorUserId, nameof(actorUserId));
        EventType = Guard.NotBlank(eventType, nameof(eventType));
        Detail = Guard.NotBlank(detail, nameof(detail));
        TargetUserId = targetUserId;
    }

    public Guid OrganizationId { get; private set; }

    public string ActorUserId { get; private set; } = string.Empty;

    public string EventType { get; private set; } = string.Empty;

    /// <summary>Whose role changed, or who was removed/invited — null when the event has no second person.</summary>
    public string? TargetUserId { get; private set; }

    public string Detail { get; private set; } = string.Empty;
}
