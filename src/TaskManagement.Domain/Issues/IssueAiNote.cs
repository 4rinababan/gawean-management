using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Issues;

/// <summary>A saved AI answer to a question asked about this issue, kept as its own record rather than an activity-log entry.</summary>
public class IssueAiNote : Entity, ITenantScoped
{
    private IssueAiNote() { }

    internal IssueAiNote(Guid issueId, Guid organizationId, string askedByUserId, string question, string answer)
    {
        IssueId = issueId;
        OrganizationId = organizationId;
        AskedByUserId = Guard.NotBlank(askedByUserId, nameof(askedByUserId));
        Question = Guard.NotBlank(question, nameof(question));
        Answer = Guard.NotBlank(answer, nameof(answer));
    }

    public Guid OrganizationId { get; private set; }

    public Guid IssueId { get; private set; }

    public string AskedByUserId { get; private set; } = string.Empty;

    public string Question { get; private set; } = string.Empty;

    public string Answer { get; private set; } = string.Empty;
}
