using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Common;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain;
using TaskManagement.Domain.Authorization;
using TaskManagement.Domain.Issues;
using TaskManagement.Domain.Organizations;

namespace TaskManagement.Application.Services;

public sealed class OrganizationService(
    IAppDbContextFactory dbf,
    ICurrentUser currentUser,
    IUserDirectory users,
    PermissionGuard guard,
    IFileStorage storage)
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

    /// <summary>
    /// Organizations where the current user is the only Admin — must be resolved (promote a co-admin,
    /// or delete/transfer the org) before their account can be deleted, or the org would be orphaned.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetOrganizationsRequiringAdminHandoffAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        await using var db = dbf.CreateDbContext();

        var adminOrgIds = await db.IgnoringTenantFilter<OrganizationMember>()
            .Where(m => m.UserId == userId && m.Role == OrgRole.Admin)
            .Select(m => m.OrganizationId)
            .ToListAsync(ct);
        if (adminOrgIds.Count == 0) return [];

        var orgsWithAnotherAdmin = await db.IgnoringTenantFilter<OrganizationMember>()
            .Where(m => adminOrgIds.Contains(m.OrganizationId) && m.Role == OrgRole.Admin && m.UserId != userId)
            .Select(m => m.OrganizationId)
            .Distinct()
            .ToListAsync(ct);

        var soleAdminOrgIds = adminOrgIds.Except(orgsWithAnotherAdmin).ToList();
        if (soleAdminOrgIds.Count == 0) return [];

        return await db.IgnoringTenantFilter<Organization>()
            .Where(o => soleAdminOrgIds.Contains(o.Id))
            .Select(o => o.Name)
            .ToListAsync(ct);
    }

    public async Task<OrganizationDto> CreateAsync(CreateOrganizationRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var slug = Organization.Slugify(request.Slug);
        await using var db = dbf.CreateDbContext();

        if (await db.IgnoringTenantFilter<Organization>().AnyAsync(o => o.Slug == slug, ct))
            throw new ConflictException($"The workspace URL '{slug}' is already taken.");

        // Anti-abuse cap on self-created workspaces only — joining an existing one via invite
        // (InvitationService.AcceptAsync) isn't subject to this, so a real team invite never bounces.
        var existingCount = await db.IgnoringTenantFilter<OrganizationMember>().CountAsync(m => m.UserId == userId, ct);
        if (existingCount >= WorkspaceLimits.MaxWorkspacesPerUser)
            throw new ConflictException($"You've reached the limit of {WorkspaceLimits.MaxWorkspacesPerUser} workspaces per account.");

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
                var displayName = u?.DisplayName ?? "Unknown user";
                var email = u?.Email ?? "";
                return new OrganizationMemberDto(
                    m.UserId,
                    displayName,
                    email,
                    u?.AvatarColor ?? "#64748b",
                    m.Role,
                    Mentions.HandleFor(displayName, email));
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

        var oldName = org.Name;
        org.Rename(name);
        db.OrganizationAuditLogs.Add(new OrganizationAuditLog(guard.OrganizationId, guard.UserId, "WorkspaceRenamed", $"Renamed workspace from \"{oldName}\" to \"{org.Name}\""));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Deletes the workspace and everything inside it. Admin-only, and the caller must retype the
    /// workspace name — this removes every project, issue, comment, attachment and sprint by cascade.
    /// </summary>
    public async Task DeleteAsync(string confirmationName, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageOrganization);
        await using var db = dbf.CreateDbContext();

        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == guard.OrganizationId, ct)
            ?? throw NotFoundException.For<Organization>(guard.OrganizationId);

        if (!string.Equals(confirmationName.Trim(), org.Name, StringComparison.Ordinal))
            throw new ConflictException("The name you typed doesn't match this workspace.");

        // No audit-log entry here on purpose: OrganizationAuditLog cascades on the org's FK, so a
        // "WorkspaceDeleted" row would just be deleted along with everything else in this same
        // operation — and nobody could view it afterwards anyway (GetAuditLogAsync needs a live org).

        // Collect blob keys before the rows go: the database cascade cannot clean up the file store.
        var storageKeys = await db.Attachments
            .Where(a => a.OrganizationId == org.Id)
            .Select(a => a.StorageKey)
            .ToListAsync(ct);

        db.Organizations.Remove(org);
        await db.SaveChangesAsync(ct);

        foreach (var key in storageKeys)
        {
            try
            {
                await storage.DeleteAsync(key, ct);
            }
            catch (Exception)
            {
                // The workspace is already gone; a leftover blob is not worth failing the operation.
            }
        }
    }

    public async Task ChangeMemberRoleAsync(string targetUserId, OrgRole role, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageMembers);
        await using var db = dbf.CreateDbContext();

        var org = await LoadOrganizationWithMembersAsync(db, ct);
        org.ChangeMemberRole(targetUserId, role);
        db.OrganizationAuditLogs.Add(new OrganizationAuditLog(
            guard.OrganizationId, guard.UserId, "MemberRoleChanged", $"Changed a member's role to {role}", targetUserId));
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveMemberAsync(string targetUserId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageMembers);
        await using var db = dbf.CreateDbContext();

        var org = await LoadOrganizationWithMembersAsync(db, ct);
        org.RemoveMember(targetUserId);
        db.OrganizationAuditLogs.Add(new OrganizationAuditLog(
            guard.OrganizationId, guard.UserId, "MemberRemoved", "Removed a member from the workspace", targetUserId));
        await db.SaveChangesAsync(ct);
    }

    private async Task<Organization> LoadOrganizationWithMembersAsync(IAppDbContext db, CancellationToken ct)
        => await db.Organizations
               .Include(o => o.Members)
               .FirstOrDefaultAsync(o => o.Id == guard.OrganizationId, ct)
           ?? throw NotFoundException.For<Organization>(guard.OrganizationId);

    /// <summary>
    /// Login is an account-level event, not tied to one org, so it's recorded once per organization
    /// the user belongs to — each workspace's own admins see it in their own audit log, since there's
    /// no cross-org admin view to put a single entry in instead. Called from every sign-in success path.
    /// </summary>
    public async Task LogLoginAsync(string userId, CancellationToken ct = default)
    {
        await using var db = dbf.CreateDbContext();

        var orgIds = await db.IgnoringTenantFilter<OrganizationMember>()
            .Where(m => m.UserId == userId)
            .Select(m => m.OrganizationId)
            .ToListAsync(ct);
        if (orgIds.Count == 0) return;

        foreach (var orgId in orgIds)
            db.OrganizationAuditLogs.Add(new OrganizationAuditLog(orgId, userId, "Login", "Signed in"));

        await db.SaveChangesAsync(ct);
    }

    /// <summary>The last 200 audit entries for the current workspace. Admin-only, same gate as Rename/Delete.</summary>
    public async Task<IReadOnlyList<OrgAuditLogEntryDto>> GetAuditLogAsync(CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageOrganization);
        await using var db = dbf.CreateDbContext();

        var entries = await db.OrganizationAuditLogs
            .Where(a => a.OrganizationId == guard.OrganizationId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        var userIds = entries.Select(e => e.ActorUserId)
            .Concat(entries.Where(e => e.TargetUserId != null).Select(e => e.TargetUserId!))
            .Distinct();
        var directory = await users.GetManyAsync(userIds, ct);
        string Name(string id) => directory.TryGetValue(id, out var u) ? u.DisplayName : "Unknown";

        return entries
            .Select(e => new OrgAuditLogEntryDto(e.Id, e.EventType, e.Detail, Name(e.ActorUserId), e.TargetUserId is null ? null : Name(e.TargetUserId), e.CreatedAt))
            .ToList();
    }

    /// <summary>
    /// Everything GaweAn holds about this user beyond their Identity account fields, for the "download
    /// your data" export: workspaces they belong to, issues they reported, comments they authored.
    /// </summary>
    public async Task<PersonalDataSummaryDto> GetMyDataSummaryAsync(string userId, CancellationToken ct = default)
    {
        await using var db = dbf.CreateDbContext();

        var organizations = await db.IgnoringTenantFilter<OrganizationMember>()
            .Where(m => m.UserId == userId)
            .Join(db.IgnoringTenantFilter<Organization>(), m => m.OrganizationId, o => o.Id, (m, o) => new { o.Name, m.Role })
            .ToListAsync(ct);

        var issues = await db.IgnoringTenantFilter<Issue>()
            .Where(i => i.ReporterUserId == userId)
            .Select(i => new { i.Title, i.Number })
            .ToListAsync(ct);

        var comments = await db.IgnoringTenantFilter<Comment>()
            .Where(c => c.AuthorUserId == userId)
            .Select(c => c.CreatedAt)
            .ToListAsync(ct);

        return new PersonalDataSummaryDto(
            organizations.Select(o => $"{o.Name} ({o.Role})").ToList(),
            issues.Select(i => $"#{i.Number} {i.Title}").ToList(),
            comments.Count);
    }
}
