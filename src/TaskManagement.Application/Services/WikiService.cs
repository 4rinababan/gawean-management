using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Common;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain;
using TaskManagement.Domain.Authorization;
using TaskManagement.Domain.Wiki;
using TaskManagement.Domain.Wiki.Spreadsheet;

namespace TaskManagement.Application.Services;

/// <summary>Workspace wiki: a tree of rich-text pages, one tree per organization.</summary>
public sealed class WikiService(IAppDbContextFactory dbf, IUserDirectory users, IHtmlSanitizer sanitizer, PermissionGuard guard)
{
    public async Task<IReadOnlyList<WikiPageSummaryDto>> GetTreeAsync(CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ViewContent);
        await using var db = dbf.CreateDbContext();

        return await db.WikiPages
            .Where(w => w.OrganizationId == guard.OrganizationId)
            .OrderBy(w => w.Title)
            .Select(w => new WikiPageSummaryDto(w.Id, w.Title, w.ParentPageId, w.UpdatedAt, w.Type))
            .ToListAsync(ct);
    }

    public async Task<WikiPageDto> GetAsync(Guid pageId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ViewContent);
        await using var db = dbf.CreateDbContext();

        var page = await db.WikiPages.FirstOrDefaultAsync(w => w.Id == pageId && w.OrganizationId == guard.OrganizationId, ct)
            ?? throw NotFoundException.For<WikiPage>(pageId);

        var parentTitle = page.ParentPageId is { } parentId
            ? await db.WikiPages.Where(w => w.Id == parentId).Select(w => w.Title).FirstOrDefaultAsync(ct)
            : null;

        var peopleIds = new[] { page.CreatedByUserId, page.LastEditedByUserId }.Where(id => id is not null).Select(id => id!);
        var people = await users.GetManyAsync(peopleIds, ct);

        return new WikiPageDto(
            page.Id,
            page.Title,
            page.Content,
            page.Type,
            page.ParentPageId,
            parentTitle,
            people.TryGetValue(page.CreatedByUserId, out var creator) ? creator.DisplayName : "Unknown",
            page.CreatedAt,
            page.LastEditedByUserId is not null && people.TryGetValue(page.LastEditedByUserId, out var editor) ? editor.DisplayName : null,
            page.UpdatedAt);
    }

    public async Task<WikiPageSummaryDto> CreateAsync(CreateWikiPageRequest request, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageWiki);
        await using var db = dbf.CreateDbContext();

        if (request.ParentPageId is { } parentId
            && !await db.WikiPages.AnyAsync(w => w.Id == parentId && w.OrganizationId == guard.OrganizationId, ct))
        {
            throw NotFoundException.For<WikiPage>(parentId);
        }

        // A spreadsheet starts with one usable sheet rather than null content, so the editor has
        // something to render immediately instead of special-casing "brand new, no workbook yet".
        var initialContent = request.Type == WikiPageType.Spreadsheet
            ? JsonSerializer.Serialize(SpreadsheetWorkbook.NewDefault())
            : null;

        var page = new WikiPage(guard.OrganizationId, request.Title, initialContent, request.ParentPageId, guard.UserId, request.Type);
        db.WikiPages.Add(page);
        await db.SaveChangesAsync(ct);

        return new WikiPageSummaryDto(page.Id, page.Title, page.ParentPageId, page.UpdatedAt, page.Type);
    }

    public async Task UpdateAsync(Guid pageId, UpdateWikiPageRequest request, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageWiki);
        await using var db = dbf.CreateDbContext();

        var page = await db.WikiPages.FirstOrDefaultAsync(w => w.Id == pageId && w.OrganizationId == guard.OrganizationId, ct)
            ?? throw NotFoundException.For<WikiPage>(pageId);

        // Only a Document's content is HTML — a Spreadsheet's is already-serialized workbook JSON, and
        // running it through the HTML sanitiser would mangle it.
        var content = request.Content is null
            ? null
            : page.Type == WikiPageType.Document ? sanitizer.Sanitize(request.Content) : request.Content;

        page.Update(request.Title, content, guard.UserId);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Deletes the page and, via the FK's cascade, its whole subtree.</summary>
    public async Task DeleteAsync(Guid pageId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageWiki);
        await using var db = dbf.CreateDbContext();

        var page = await db.WikiPages.FirstOrDefaultAsync(w => w.Id == pageId && w.OrganizationId == guard.OrganizationId, ct)
            ?? throw NotFoundException.For<WikiPage>(pageId);

        db.WikiPages.Remove(page);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> CountChildrenAsync(Guid pageId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ViewContent);
        await using var db = dbf.CreateDbContext();
        return await db.WikiPages.CountAsync(w => w.ParentPageId == pageId && w.OrganizationId == guard.OrganizationId, ct);
    }

    /// <summary>The one wiki page (if any) documenting a given issue.</summary>
    public async Task<WikiPageSummaryDto?> GetLinkedPageAsync(Guid issueId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ViewContent);
        await using var db = dbf.CreateDbContext();

        return await db.WikiPages
            .Where(w => w.IssueId == issueId && w.OrganizationId == guard.OrganizationId)
            .Select(w => new WikiPageSummaryDto(w.Id, w.Title, w.ParentPageId, w.UpdatedAt, w.Type))
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Links a page to an issue, clearing any other page's link to that same issue first — at most one
    /// page documents a given issue at a time. Pass <c>issueId: null</c> to just unlink <paramref name="pageId"/>.
    /// </summary>
    public async Task LinkToIssueAsync(Guid pageId, Guid? issueId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageWiki);
        await using var db = dbf.CreateDbContext();

        if (issueId is { } id)
        {
            var previouslyLinked = await db.WikiPages
                .Where(w => w.OrganizationId == guard.OrganizationId && w.IssueId == id && w.Id != pageId)
                .ToListAsync(ct);
            foreach (var previous in previouslyLinked)
                previous.LinkToIssue(null);
        }

        var page = await db.WikiPages.FirstOrDefaultAsync(w => w.Id == pageId && w.OrganizationId == guard.OrganizationId, ct)
            ?? throw NotFoundException.For<WikiPage>(pageId);

        page.LinkToIssue(issueId);
        await db.SaveChangesAsync(ct);
    }
}
