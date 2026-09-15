using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Wiki;

/// <summary>
/// One page in a workspace's wiki. Pages can nest under a parent to form a Confluence-style tree;
/// nesting depth is unbounded but only set at creation time — there's no "move" operation yet, which
/// sidesteps having to guard against a page becoming its own ancestor.
///
/// <see cref="Content"/> is shaped by <see cref="Type"/>: sanitised rich-text HTML for
/// <see cref="WikiPageType.Document"/>, or serialized <see cref="Spreadsheet.SpreadsheetWorkbook"/> JSON
/// for <see cref="WikiPageType.Spreadsheet"/> — the type never changes after creation, so nothing has to
/// reinterpret one shape as the other.
/// </summary>
public class WikiPage : Entity, ITenantScoped
{
    private WikiPage() { }

    public WikiPage(Guid organizationId, string title, string? content, Guid? parentPageId, string createdByUserId, WikiPageType type = WikiPageType.Document)
    {
        OrganizationId = organizationId;
        Title = Guard.NotBlank(title, nameof(title));
        Content = content;
        ParentPageId = parentPageId;
        CreatedByUserId = Guard.NotBlank(createdByUserId, nameof(createdByUserId));
        Type = type;
    }

    public Guid OrganizationId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Content { get; private set; }

    public WikiPageType Type { get; private set; } = WikiPageType.Document;

    public Guid? ParentPageId { get; private set; }

    public string CreatedByUserId { get; private set; } = string.Empty;

    public string? LastEditedByUserId { get; private set; }

    /// <summary>The issue this page documents, if any — at most one page links to a given issue
    /// (enforced by the service, not the schema: setting a second page's IssueId clears the first's).</summary>
    public Guid? IssueId { get; private set; }

    public void Update(string title, string? content, string editedByUserId)
    {
        Title = Guard.NotBlank(title, nameof(title));
        Content = content;
        LastEditedByUserId = Guard.NotBlank(editedByUserId, nameof(editedByUserId));
    }

    public void LinkToIssue(Guid? issueId) => IssueId = issueId;
}
