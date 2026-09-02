using System.Security.Cryptography;
using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Organizations;

/// <summary>A pending offer for an email address to join an organization with a given role, redeemed via an opaque token.</summary>
public class Invitation : Entity, ITenantScoped
{
    private Invitation() { }

    internal Invitation(Guid organizationId, string email, OrgRole role, string invitedByUserId, TimeSpan validFor)
    {
        OrganizationId = organizationId;
        Email = Guard.NotBlank(email, nameof(email)).ToLowerInvariant();
        Role = role;
        InvitedByUserId = Guard.NotBlank(invitedByUserId, nameof(invitedByUserId));
        Token = GenerateToken();
        ExpiresAt = DateTimeOffset.UtcNow.Add(validFor);
        Status = InvitationStatus.Pending;
    }

    public Guid OrganizationId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public OrgRole Role { get; private set; }

    public string InvitedByUserId { get; private set; } = string.Empty;

    public string Token { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public string? AcceptedByUserId { get; private set; }

    public InvitationStatus Status { get; private set; }

    public bool IsRedeemable(DateTimeOffset now)
        => Status == InvitationStatus.Pending && now <= ExpiresAt;

    public void Accept(string userId, DateTimeOffset now)
    {
        if (!IsRedeemable(now))
            throw new DomainException("This invitation is no longer valid.");

        Status = InvitationStatus.Accepted;
        AcceptedByUserId = Guard.NotBlank(userId, nameof(userId));
        AcceptedAt = now;
    }

    public void Revoke()
    {
        if (Status != InvitationStatus.Pending)
            throw new DomainException("Only a pending invitation can be revoked.");

        Status = InvitationStatus.Revoked;
    }

    private static string GenerateToken()
        => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
}
