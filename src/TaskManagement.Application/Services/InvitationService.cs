using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Common;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain;
using TaskManagement.Domain.Authorization;
using TaskManagement.Domain.Notifications;
using TaskManagement.Domain.Organizations;

namespace TaskManagement.Application.Services;

public sealed class InvitationService(
    IAppDbContextFactory dbf,
    ICurrentUser currentUser,
    IUserDirectory users,
    IEmailSender email,
    PermissionGuard guard,
    IClock clock,
    IAppUrls urls)
{
    private static readonly TimeSpan Validity = TimeSpan.FromDays(14);

    public async Task<IReadOnlyList<InvitationDto>> GetPendingAsync(CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageMembers);
        await using var db = dbf.CreateDbContext();

        var invites = await db.Invitations
            .Where(i => i.OrganizationId == guard.OrganizationId && i.Status == InvitationStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        var directory = await users.GetManyAsync(invites.Select(i => i.InvitedByUserId), ct);

        return invites
            .Select(i => new InvitationDto(
                i.Id,
                i.Email,
                i.Role,
                i.ExpiresAt,
                directory.TryGetValue(i.InvitedByUserId, out var u) ? u.DisplayName : "A teammate",
                urls.InvitationAccept(i.Token)))
            .ToList();
    }

    public async Task InviteAsync(InviteMemberRequest request, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageMembers);
        await using var db = dbf.CreateDbContext();

        var org = await db.Organizations
            .Include(o => o.Members)
            .Include(o => o.Invitations)
            .FirstAsync(o => o.Id == guard.OrganizationId, ct);

        var address = request.Email.Trim().ToLowerInvariant();
        var existing = await users.FindByEmailAsync(address, ct);
        if (existing is not null && org.Members.Any(m => m.UserId == existing.Id))
            throw new ConflictException("That person is already a member of this workspace.");

        // Checked here too, not just at AcceptAsync: the inviter should know immediately rather than
        // the invitee hitting a wall later. AcceptAsync still enforces it — invites can be sent
        // concurrently, so this check alone can't guarantee the seat is still free by the time it's redeemed.
        if (org.Members.Count >= WorkspaceLimits.MaxMembersPerWorkspace)
            throw new ConflictException($"This workspace has reached its limit of {WorkspaceLimits.MaxMembersPerWorkspace} members.");

        var actor = currentUser.RequireUserId();
        var invitation = org.InviteMember(address, request.Role, actor, Validity);
        // Entity ids are client-generated, so EF's "key is already set" heuristic would mark a child
        // discovered through a navigation collection as Modified (an UPDATE of a row that doesn't
        // exist yet). Adding it to its DbSet forces the Added state.
        db.Invitations.Add(invitation);
        db.OrganizationAuditLogs.Add(new OrganizationAuditLog(org.Id, actor, "MemberInvited", $"Invited {address} as {request.Role}"));
        await db.SaveChangesAsync(ct);

        var link = urls.InvitationAccept(invitation.Token);
        await email.SendAsync(
            address,
            $"You've been invited to join {org.Name}",
            $"""
             <p>You've been invited to collaborate in the <strong>{org.Name}</strong> workspace.</p>
             <p><a href="{link}">Accept the invitation</a> (expires {invitation.ExpiresAt:d}).</p>
             """,
            ct);
    }

    public async Task RevokeAsync(Guid invitationId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageMembers);
        await using var db = dbf.CreateDbContext();

        var invitation = await db.Invitations
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.OrganizationId == guard.OrganizationId, ct)
            ?? throw NotFoundException.For<Invitation>(invitationId);

        invitation.Revoke();
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Redeems a token for the signed-in user. Cross-tenant: the invitation is looked up outside any tenant filter.</summary>
    public async Task<AcceptInvitationResult> AcceptAsync(string token, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        await using var db = dbf.CreateDbContext();

        var invitation = await db.IgnoringTenantFilter<Invitation>()
            .FirstOrDefaultAsync(i => i.Token == token, ct)
            ?? throw new NotFoundException("This invitation link is invalid.");

        var org = await db.IgnoringTenantFilter<Organization>()
            .Include(o => o.Members)
            .FirstAsync(o => o.Id == invitation.OrganizationId, ct);

        // Idempotent: a component can render twice (prerender + circuit). If this user already
        // holds the invitation, or is already a member, just report success.
        var alreadyMember = org.Members.Any(m => m.UserId == userId);
        if (invitation.Status == InvitationStatus.Accepted && invitation.AcceptedByUserId == userId || alreadyMember)
            return new AcceptInvitationResult(org.Slug, org.Name);

        if (!invitation.IsRedeemable(clock.UtcNow))
            throw new ConflictException("This invitation has expired or has already been used.");

        if (org.Members.Count >= WorkspaceLimits.MaxMembersPerWorkspace)
            throw new ConflictException($"This workspace has reached its limit of {WorkspaceLimits.MaxMembersPerWorkspace} members.");

        db.OrganizationMembers.Add(org.AddMember(userId, invitation.Role));
        invitation.Accept(userId, clock.UtcNow);
        db.OrganizationAuditLogs.Add(new OrganizationAuditLog(org.Id, userId, "MemberJoined", "Joined the workspace"));

        db.Notifications.Add(new Notification(
            org.Id, userId, NotificationType.AddedToOrganization,
            $"You joined {org.Name}.", url: $"/{org.Slug}"));

        await db.SaveChangesAsync(ct);
        return new AcceptInvitationResult(org.Slug, org.Name);
    }
}
