namespace TaskManagement.Domain.Common;

/// <summary>Base type for all persisted aggregate roots and entities. Uses a GUID identity generated on construction.</summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public override bool Equals(object? obj) => obj is Entity other && other.GetType() == GetType() && other.Id == Id;

    public override int GetHashCode() => Id.GetHashCode();
}

/// <summary>Marks an entity that belongs to a single <c>Organization</c> and is isolated by tenant query filters.</summary>
public interface ITenantScoped
{
    Guid OrganizationId { get; }
}
