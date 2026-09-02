using TaskManagement.Domain;
using TaskManagement.Domain.Authorization;

namespace TaskManagement.Domain.Tests;

public class RolePermissionsTests
{
    [Theory]
    [InlineData(OrgRole.Viewer, OrgPermission.ViewContent, true)]
    [InlineData(OrgRole.Viewer, OrgPermission.CreateIssue, false)]
    [InlineData(OrgRole.Viewer, OrgPermission.CommentOnIssue, false)]
    [InlineData(OrgRole.Member, OrgPermission.CreateIssue, true)]
    [InlineData(OrgRole.Member, OrgPermission.ManageSprints, true)]
    [InlineData(OrgRole.Member, OrgPermission.DeleteIssue, false)]
    [InlineData(OrgRole.Member, OrgPermission.ManageMembers, false)]
    [InlineData(OrgRole.Admin, OrgPermission.ManageOrganization, true)]
    [InlineData(OrgRole.Admin, OrgPermission.DeleteIssue, true)]
    public void Allows_matches_the_permission_matrix(OrgRole role, OrgPermission permission, bool expected)
        => RolePermissions.Allows(role, permission).Should().Be(expected);

    [Fact]
    public void Admin_is_a_strict_superset_of_member_which_is_a_strict_superset_of_viewer()
    {
        var viewer = RolePermissions.For(OrgRole.Viewer).ToHashSet();
        var member = RolePermissions.For(OrgRole.Member).ToHashSet();
        var admin = RolePermissions.For(OrgRole.Admin).ToHashSet();

        member.IsProperSupersetOf(viewer).Should().BeTrue();
        admin.IsProperSupersetOf(member).Should().BeTrue();
    }
}
