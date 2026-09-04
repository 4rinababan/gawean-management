using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Issues;

/// <summary>
/// A person who should see/follow an issue without being its accountable <see cref="Issue.AssigneeUserId"/>.
/// An issue can have any number of viewers; a user can only be added once.
/// </summary>
public class IssueViewer : Entity, ITenantScoped
{
    private IssueViewer() { }

    internal IssueViewer(Guid issueId, Guid organizationId, string userId)
    {
        IssueId = issueId;
        OrganizationId = organizationId;
        UserId = Guard.NotBlank(userId, nameof(userId));
    }

    public Guid OrganizationId { get; private set; }

    public Guid IssueId { get; private set; }

    public string UserId { get; private set; } = string.Empty;
}
