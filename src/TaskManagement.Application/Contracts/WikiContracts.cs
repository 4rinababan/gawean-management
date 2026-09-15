namespace TaskManagement.Application.Contracts;

/// <summary>One node in the page tree — no content, cheap enough to load the whole workspace's tree at once.</summary>
public sealed record WikiPageSummaryDto(Guid Id, string Title, Guid? ParentPageId, DateTimeOffset UpdatedAt);

public sealed record WikiPageDto(
    Guid Id,
    string Title,
    string? Content,
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
}

public sealed class UpdateWikiPageRequest
{
    public string Title { get; set; } = "";
    public string? Content { get; set; }
}
