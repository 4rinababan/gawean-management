using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Organizations;

/// <summary>Join entity linking an Identity user to an <see cref="Organization"/> with a role.</summary>
public class OrganizationMember : Entity, ITenantScoped
{
    private OrganizationMember() { }

    internal OrganizationMember(Guid organizationId, string userId, OrgRole role)
    {
        OrganizationId = organizationId;
        UserId = Guard.NotBlank(userId, nameof(userId));
        Role = role;
    }

    public Guid OrganizationId { get; private set; }

    public string UserId { get; private set; } = string.Empty;

    public OrgRole Role { get; private set; }

    internal void SetRole(OrgRole role) => Role = role;
}
