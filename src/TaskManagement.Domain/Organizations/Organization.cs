using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Organizations;

/// <summary>A workspace / team. The tenant boundary: every project, issue and sprint belongs to exactly one organization.</summary>
public class Organization : Entity
{
    private readonly List<OrganizationMember> _members = [];
    private readonly List<Invitation> _invitations = [];

    private Organization() { }

    public Organization(string name, string slug, string ownerUserId)
    {
        Name = Guard.NotBlank(name, nameof(name));
        Slug = Slugify(slug);
        _members.Add(new OrganizationMember(Id, ownerUserId, OrgRole.Admin));
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>URL-safe unique identifier used in routes: <c>/{slug}/board</c>.</summary>
    public string Slug { get; private set; } = string.Empty;

    public IReadOnlyCollection<OrganizationMember> Members => _members.AsReadOnly();

    public IReadOnlyCollection<Invitation> Invitations => _invitations.AsReadOnly();

    public void Rename(string name) => Name = Guard.NotBlank(name, nameof(name));

    public OrganizationMember AddMember(string userId, OrgRole role)
    {
        if (_members.Any(m => m.UserId == userId))
            throw new DomainException("User is already a member of this organization.");

        var member = new OrganizationMember(Id, userId, role);
        _members.Add(member);
        return member;
    }

    public void RemoveMember(string userId)
    {
        var member = _members.SingleOrDefault(m => m.UserId == userId)
            ?? throw new DomainException("User is not a member of this organization.");

        if (member.Role == OrgRole.Admin && _members.Count(m => m.Role == OrgRole.Admin) == 1)
            throw new DomainException("Cannot remove the last administrator of an organization.");

        _members.Remove(member);
    }

    public void ChangeMemberRole(string userId, OrgRole role)
    {
        var member = _members.SingleOrDefault(m => m.UserId == userId)
            ?? throw new DomainException("User is not a member of this organization.");

        if (member.Role == OrgRole.Admin && role != OrgRole.Admin
            && _members.Count(m => m.Role == OrgRole.Admin) == 1)
            throw new DomainException("Cannot demote the last administrator of an organization.");

        member.SetRole(role);
    }

    public Invitation InviteMember(string email, OrgRole role, string invitedByUserId, TimeSpan validFor)
    {
        email = email.Trim().ToLowerInvariant();

        if (_members.Count != 0 && _invitations.Any(i => i.Status == InvitationStatus.Pending && i.Email == email))
            throw new DomainException("An invitation for this email is already pending.");

        var invitation = new Invitation(Id, email, role, invitedByUserId, validFor);
        _invitations.Add(invitation);
        return invitation;
    }

    public static string Slugify(string value)
    {
        var slug = new string(Guard.NotBlank(value, nameof(value))
            .Trim()
            .ToLowerInvariant()
            .Select(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') ? c : '-')
            .ToArray());

        while (slug.Contains("--"))
            slug = slug.Replace("--", "-");

        slug = slug.Trim('-');
        return slug.Length == 0 ? throw new DomainException("Slug must contain at least one alphanumeric character.") : slug;
    }
}
