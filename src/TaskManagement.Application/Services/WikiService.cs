using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Common;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain.Authorization;
using TaskManagement.Domain.Wiki;

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
            .Select(w => new WikiPageSummaryDto(w.Id, w.Title, w.ParentPageId, w.UpdatedAt))
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

        var page = new WikiPage(guard.OrganizationId, request.Title, null, request.ParentPageId, guard.UserId);
        db.WikiPages.Add(page);
        await db.SaveChangesAsync(ct);

        return new WikiPageSummaryDto(page.Id, page.Title, page.ParentPageId, page.UpdatedAt);
    }

    public async Task UpdateAsync(Guid pageId, UpdateWikiPageRequest request, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageWiki);
        await using var db = dbf.CreateDbContext();

        var page = await db.WikiPages.FirstOrDefaultAsync(w => w.Id == pageId && w.OrganizationId == guard.OrganizationId, ct)
            ?? throw NotFoundException.For<WikiPage>(pageId);

        page.Update(request.Title, request.Content is null ? null : sanitizer.Sanitize(request.Content), guard.UserId);
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
}
