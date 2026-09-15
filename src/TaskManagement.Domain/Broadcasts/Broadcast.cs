using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Broadcasts;

/// <summary>
/// A site-admin-authored announcement shown to matching users as a one-time popup. Deliberately not
/// <see cref="ITenantScoped"/> — a broadcast can target every organization at once (see
/// <see cref="TargetType"/>), so it can't live inside a single tenant's data.
/// </summary>
public class Broadcast : Entity
{
    private Broadcast() { }

    public Broadcast(string message, string? imageStorageKey, string createdByUserId, BroadcastTargetType targetType, Guid? targetOrganizationId)
    {
        Message = Guard.NotBlank(message, nameof(message));
        ImageStorageKey = imageStorageKey;
        CreatedByUserId = Guard.NotBlank(createdByUserId, nameof(createdByUserId));
        TargetType = targetType;
        TargetOrganizationId = targetType == BroadcastTargetType.Organization
            ? targetOrganizationId ?? throw new DomainException("An organization-targeted broadcast needs a target organization.")
            : null;
        IsActive = true;
    }

    public string Message { get; private set; } = string.Empty;

    public string? ImageStorageKey { get; private set; }

    public string CreatedByUserId { get; private set; } = string.Empty;

    public BroadcastTargetType TargetType { get; private set; }

    /// <summary>Set only when <see cref="TargetType"/> is <see cref="BroadcastTargetType.Organization"/>.</summary>
    public Guid? TargetOrganizationId { get; private set; }

    /// <summary>An inactive broadcast is retracted — it stops showing to anyone who hasn't already
    /// dismissed it, without deleting the row (keeps it in the admin's history).</summary>
    public bool IsActive { get; private set; }

    public void Deactivate() => IsActive = false;
}

/// <summary>One explicit recipient of a <see cref="BroadcastTargetType.Users"/> broadcast.</summary>
public class BroadcastTargetUser : Entity
{
    private BroadcastTargetUser() { }

    public BroadcastTargetUser(Guid broadcastId, string userId)
    {
        BroadcastId = broadcastId;
        UserId = Guard.NotBlank(userId, nameof(userId));
    }

    public Guid BroadcastId { get; private set; }

    public string UserId { get; private set; } = string.Empty;
}

/// <summary>Records that a user has seen and closed a broadcast, so it never shows to them again.</summary>
public class BroadcastDismissal : Entity
{
    private BroadcastDismissal() { }

    public BroadcastDismissal(Guid broadcastId, string userId)
    {
        BroadcastId = broadcastId;
        UserId = Guard.NotBlank(userId, nameof(userId));
    }

    public Guid BroadcastId { get; private set; }

    public string UserId { get; private set; } = string.Empty;
}
