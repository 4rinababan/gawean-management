using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Common;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain;
using TaskManagement.Domain.Authorization;
using TaskManagement.Domain.Organizations;

namespace TaskManagement.Application.Services;

public sealed class OrganizationService(
    IAppDbContextFactory dbf,
    ICurrentUser currentUser,
    IUserDirectory users,
    PermissionGuard guard)
{
    /// <summary>Every organization the current user belongs to, for the workspace switcher.</summary>
    public async Task<IReadOnlyList<OrganizationDto>> GetMyOrganizationsAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        await using var db = dbf.CreateDbContext();

        var rows = await db.IgnoringTenantFilter<Organization>()
            .Where(o => o.Members.Any(m => m.UserId == userId))
            .OrderBy(o => o.Name)
            .Select(o => new
            {
                o.Id,
                o.Name,
                o.Slug,
                Role = o.Members.Where(m => m.UserId == userId).Select(m => (OrgRole?)m.Role).FirstOrDefault(),
                MemberCount = o.Members.Count,
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new OrganizationDto(r.Id, r.Name, r.Slug, r.Role ?? OrgRole.Viewer, r.MemberCount))
            .ToList();
    }

    public async Task<OrganizationDto> CreateAsync(CreateOrganizationRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var slug = Organization.Slugify(request.Slug);
        await using var db = dbf.CreateDbContext();

        if (await db.IgnoringTenantFilter<Organization>().AnyAsync(o => o.Slug == slug, ct))
            throw new ConflictException($"The workspace URL '{slug}' is already taken.");

        var org = new Organization(request.Name, slug, userId);
        db.Organizations.Add(org);
        await db.SaveChangesAsync(ct);

        return new OrganizationDto(org.Id, org.Name, org.Slug, OrgRole.Admin, 1);
    }

    public async Task<IReadOnlyList<OrganizationMemberDto>> GetMembersAsync(CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ViewContent);
        await using var db = dbf.CreateDbContext();

        var members = await db.OrganizationMembers
            .Where(m => m.OrganizationId == guard.OrganizationId)
            .ToListAsync(ct);

        var directory = await users.GetManyAsync(members.Select(m => m.UserId), ct);

        return members
            .Select(m =>
            {
                directory.TryGetValue(m.UserId, out var u);
                return new OrganizationMemberDto(
                    m.UserId,
                    u?.DisplayName ?? "Unknown user",
                    u?.Email ?? "",
                    u?.AvatarColor ?? "#64748b",
                    m.Role);
            })
            .OrderByDescending(m => m.Role)
            .ThenBy(m => m.DisplayName)
            .ToList();
    }

    /// <summary>Workspace settings are admin-only.</summary>
    public async Task RenameAsync(string name, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageOrganization);
        await using var db = dbf.CreateDbContext();

        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == guard.OrganizationId, ct)
            ?? throw NotFoundException.For<Organization>(guard.OrganizationId);

        org.Rename(name);
        await db.SaveChangesAsync(ct);
    }

    public async Task ChangeMemberRoleAsync(string targetUserId, OrgRole role, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageMembers);
        await using var db = dbf.CreateDbContext();

        var org = await LoadOrganizationWithMembersAsync(db, ct);
        org.ChangeMemberRole(targetUserId, role);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveMemberAsync(string targetUserId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageMembers);
        await using var db = dbf.CreateDbContext();

        var org = await LoadOrganizationWithMembersAsync(db, ct);
        org.RemoveMember(targetUserId);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Organization> LoadOrganizationWithMembersAsync(IAppDbContext db, CancellationToken ct)
        => await db.Organizations
               .Include(o => o.Members)
               .FirstOrDefaultAsync(o => o.Id == guard.OrganizationId, ct)
           ?? throw NotFoundException.For<Organization>(guard.OrganizationId);
}
