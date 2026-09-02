using TaskManagement.Domain;

namespace TaskManagement.Domain.Authorization;

public enum OrgPermission
{
    ViewContent,
    CreateIssue,
    EditIssue,
    DeleteIssue,
    CommentOnIssue,
    ManageSprints,
    ManageProjects,
    ManageMembers,
    ManageOrganization,
}

/// <summary>The single source of truth for what each <see cref="OrgRole"/> may do within an organization.</summary>
public static class RolePermissions
{
    private static readonly IReadOnlyDictionary<OrgRole, HashSet<OrgPermission>> Matrix = new Dictionary<OrgRole, HashSet<OrgPermission>>
    {
        [OrgRole.Viewer] =
        [
            OrgPermission.ViewContent,
        ],
        [OrgRole.Member] =
        [
            OrgPermission.ViewContent,
            OrgPermission.CreateIssue,
            OrgPermission.EditIssue,
            OrgPermission.CommentOnIssue,
            OrgPermission.ManageSprints,
        ],
        [OrgRole.Admin] =
        [
            OrgPermission.ViewContent,
            OrgPermission.CreateIssue,
            OrgPermission.EditIssue,
            OrgPermission.DeleteIssue,
            OrgPermission.CommentOnIssue,
            OrgPermission.ManageSprints,
            OrgPermission.ManageProjects,
            OrgPermission.ManageMembers,
            OrgPermission.ManageOrganization,
        ],
    };

    public static bool Allows(OrgRole role, OrgPermission permission)
        => Matrix.TryGetValue(role, out var permissions) && permissions.Contains(permission);

    public static IReadOnlyCollection<OrgPermission> For(OrgRole role)
        => Matrix.TryGetValue(role, out var permissions) ? permissions : [];
}
