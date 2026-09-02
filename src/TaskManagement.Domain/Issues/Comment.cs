using System.Text.RegularExpressions;
using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Issues;

/// <summary>A threaded note on an issue. Supports <c>@username</c> mentions parsed from the body.</summary>
public partial class Comment : Entity, ITenantScoped
{
    private Comment() { }

    internal Comment(Guid issueId, Guid organizationId, string authorUserId, string body)
    {
        IssueId = issueId;
        OrganizationId = organizationId;
        AuthorUserId = Guard.NotBlank(authorUserId, nameof(authorUserId));
        Body = Guard.NotBlank(body, nameof(body));
    }

    public Guid OrganizationId { get; private set; }

    public Guid IssueId { get; private set; }

    public string AuthorUserId { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public DateTimeOffset? EditedAt { get; private set; }

    public void Edit(string body, string editorUserId)
    {
        if (editorUserId != AuthorUserId)
            throw new DomainException("Only the author can edit a comment.");

        Body = Guard.NotBlank(body, nameof(body));
        EditedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Distinct lowercase usernames referenced as <c>@name</c> in the body.</summary>
    public IReadOnlyCollection<string> ExtractMentions()
        => MentionPattern().Matches(Body)
            .Select(m => m.Groups[1].Value.ToLowerInvariant())
            .Distinct()
            .ToArray();

    [GeneratedRegex(@"(?<![\w])@([A-Za-z0-9_.-]{2,64})")]
    private static partial Regex MentionPattern();
}
