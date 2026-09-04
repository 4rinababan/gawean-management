using TaskManagement.Application.Abstractions;
using TaskManagement.Domain;
using TaskManagement.Domain.Authorization;

namespace TaskManagement.Application.Common;

/// <summary>Enforces the domain permission matrix against the ambient <see cref="ITenantContext"/> for the current caller.</summary>
public sealed class PermissionGuard(ITenantContext tenant, ICurrentUser currentUser)
{
    public Guid OrganizationId => tenant.OrganizationId;

    public string UserId => currentUser.RequireUserId();

    public OrgRole Role => tenant.Role;

    public bool Allows(OrgPermission permission) => RolePermissions.Allows(tenant.Role, permission);

    public void Require(OrgPermission permission)
    {
        if (!tenant.IsResolved)
            throw new ForbiddenException("No organization is selected.");

        if (!RolePermissions.Allows(tenant.Role, permission))
            throw ForbiddenException.Missing(permission);
    }
}
