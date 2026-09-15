using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Wiki;

/// <summary>
/// One page in a workspace's wiki. Pages can nest under a parent to form a Confluence-style tree;
/// nesting depth is unbounded but only set at creation time — there's no "move" operation yet, which
/// sidesteps having to guard against a page becoming its own ancestor.
/// </summary>
public class WikiPage : Entity, ITenantScoped
{
    private WikiPage() { }

    public WikiPage(Guid organizationId, string title, string? content, Guid? parentPageId, string createdByUserId)
    {
        OrganizationId = organizationId;
        Title = Guard.NotBlank(title, nameof(title));
        Content = content;
        ParentPageId = parentPageId;
        CreatedByUserId = Guard.NotBlank(createdByUserId, nameof(createdByUserId));
    }

    public Guid OrganizationId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    /// <summary>Sanitised rich-text HTML, same shape as an issue description.</summary>
    public string? Content { get; private set; }

    public Guid? ParentPageId { get; private set; }

    public string CreatedByUserId { get; private set; } = string.Empty;

    public string? LastEditedByUserId { get; private set; }

    public void Update(string title, string? content, string editedByUserId)
    {
        Title = Guard.NotBlank(title, nameof(title));
        Content = content;
        LastEditedByUserId = Guard.NotBlank(editedByUserId, nameof(editedByUserId));
    }
}
