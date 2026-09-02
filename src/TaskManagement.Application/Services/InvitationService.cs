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
    IAppDbContext db,
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
                directory.TryGetValue(i.InvitedByUserId, out var u) ? u.DisplayName : "A teammate"))
            .ToList();
    }

    public async Task InviteAsync(InviteMemberRequest request, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageMembers);

        var org = await db.Organizations
            .Include(o => o.Members)
            .Include(o => o.Invitations)
            .FirstAsync(o => o.Id == guard.OrganizationId, ct);

        var address = request.Email.Trim().ToLowerInvariant();
        var existing = await users.FindByEmailAsync(address, ct);
        if (existing is not null && org.Members.Any(m => m.UserId == existing.Id))
            throw new ConflictException("That person is already a member of this workspace.");

        var invitation = org.InviteMember(address, request.Role, currentUser.RequireUserId(), Validity);
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

        var invitation = await db.IgnoringTenantFilter<Invitation>()
            .FirstOrDefaultAsync(i => i.Token == token, ct)
            ?? throw new NotFoundException("This invitation link is invalid.");

        if (!invitation.IsRedeemable(clock.UtcNow))
            throw new ConflictException("This invitation has expired or has already been used.");

        var org = await db.IgnoringTenantFilter<Organization>()
            .Include(o => o.Members)
            .FirstAsync(o => o.Id == invitation.OrganizationId, ct);

        if (org.Members.All(m => m.UserId != userId))
            org.AddMember(userId, invitation.Role);

        invitation.Accept(userId, clock.UtcNow);

        db.Notifications.Add(new Notification(
            org.Id, userId, NotificationType.AddedToOrganization,
            $"You joined {org.Name}.", url: $"/{org.Slug}"));

        await db.SaveChangesAsync(ct);
        return new AcceptInvitationResult(org.Slug, org.Name);
    }
}
