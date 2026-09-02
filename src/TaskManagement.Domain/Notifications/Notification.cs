using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Notifications;

/// <summary>An in-app notification for a single recipient. Also mirrored by email for a subset of types.</summary>
public class Notification : Entity, ITenantScoped
{
    private Notification() { }

    public Notification(Guid organizationId, string recipientUserId, NotificationType type, string message, Guid? issueId = null, string? url = null)
    {
        OrganizationId = organizationId;
        RecipientUserId = Guard.NotBlank(recipientUserId, nameof(recipientUserId));
        Type = type;
        Message = Guard.NotBlank(message, nameof(message));
        IssueId = issueId;
        Url = url;
    }

    public Guid OrganizationId { get; private set; }

    public string RecipientUserId { get; private set; } = string.Empty;

    public NotificationType Type { get; private set; }

    public string Message { get; private set; } = string.Empty;

    public Guid? IssueId { get; private set; }

    public string? Url { get; private set; }

    public bool IsRead { get; private set; }

    public void MarkRead() => IsRead = true;
}
