using TaskManagement.Domain;

namespace TaskManagement.Application.Contracts;

/// <summary>One node in the page tree — no content, cheap enough to load the whole workspace's tree at once.</summary>
public sealed record WikiPageSummaryDto(Guid Id, string Title, Guid? ParentPageId, DateTimeOffset UpdatedAt, WikiPageType Type);

public sealed record WikiPageDto(
    Guid Id,
    string Title,
    string? Content,
    WikiPageType Type,
    Guid? ParentPageId,
    string? ParentTitle,
    string CreatedByDisplayName,
    DateTimeOffset CreatedAt,
    string? LastEditedByDisplayName,
    DateTimeOffset UpdatedAt);

public sealed class CreateWikiPageRequest
{
    public string Title { get; set; } = "";
    public Guid? ParentPageId { get; set; }
    public WikiPageType Type { get; set; } = WikiPageType.Document;
}

public sealed class UpdateWikiPageRequest
{
    public string Title { get; set; } = "";

    /// <summary>Sanitised as HTML on save for a Document page; stored as-is (already-serialized
    /// workbook JSON) for a Spreadsheet page — see <see cref="Services.WikiService"/>.</summary>
    public string? Content { get; set; }
}
