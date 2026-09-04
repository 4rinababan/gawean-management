using System.IO.Compression;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Web.Infrastructure;

/// <summary>
/// Streams attachment content. This runs as a plain HTTP request, not inside a Blazor circuit, so it
/// deliberately does not use ICurrentUser / ITenantContext / AttachmentService — those are resolved per
/// Razor component and AuthenticationStateProvider throws outside that scope. Authorisation is therefore
/// done explicitly here: the caller must be a member of the organisation that owns the attachment, and
/// the slug in the route must be that same organisation.
/// </summary>
public static class AttachmentEndpoints
{
    public static void MapAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/{slug}/attachments").RequireAuthorization();

        group.MapGet("/{attachmentId:guid}", async (
            string slug,
            Guid attachmentId,
            HttpContext http,
            IDbContextFactory<AppDbContext> dbf,
            IFileStorage storage,
            CancellationToken ct) =>
        {
            var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return Results.Unauthorized();

            await using var db = await dbf.CreateDbContextAsync(ct);

            var attachment = await db.Attachments
                .IgnoreQueryFilters()
                .Where(a => a.Id == attachmentId)
                .Select(a => new { a.StorageKey, a.FileName, a.ContentType, a.OrganizationId })
                .FirstOrDefaultAsync(ct);

            if (attachment is null)
                return Results.NotFound();

            var allowed = await db.Organizations
                .IgnoreQueryFilters()
                .AnyAsync(o => o.Id == attachment.OrganizationId
                               && o.Slug == slug
                               && o.Members.Any(m => m.UserId == userId), ct);

            if (!allowed)
                return Results.NotFound(); // don't confirm the attachment exists to a non-member

            Stream stream;
            try
            {
                stream = await storage.OpenReadAsync(attachment.StorageKey, ct);
            }
            catch (FileNotFoundException)
            {
                return Results.NotFound();
            }

            // Images are embedded in descriptions, so they must render inline; everything else downloads.
            return attachment.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                ? Results.File(stream, attachment.ContentType)
                : Results.File(stream, attachment.ContentType, attachment.FileName);
        });

        // Distinct literal "issue" segment so this never collides with the {attachmentId:guid} route above.
        group.MapGet("/issue/{issueId:guid}/zip", async (
            string slug,
            Guid issueId,
            HttpContext http,
            IDbContextFactory<AppDbContext> dbf,
            IFileStorage storage,
            CancellationToken ct) =>
        {
            var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return Results.Unauthorized();

            await using var db = await dbf.CreateDbContextAsync(ct);

            var issue = await db.Issues.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == issueId, ct);
            if (issue is null)
                return Results.NotFound();

            var allowed = await db.Organizations
                .IgnoreQueryFilters()
                .AnyAsync(o => o.Id == issue.OrganizationId
                               && o.Slug == slug
                               && o.Members.Any(m => m.UserId == userId), ct);
            if (!allowed)
                return Results.NotFound();

            var attachments = await db.Attachments.IgnoreQueryFilters()
                .Where(a => a.IssueId == issueId)
                .ToListAsync(ct);
            if (attachments.Count == 0)
                return Results.NotFound();

            var project = await db.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == issue.ProjectId, ct);
            var reference = project is null ? issue.Id.ToString() : $"{project.Key}-{issue.Number}";

            // Built in memory rather than streamed: attachment sets per issue are small (a handful of
            // files, 25MB cap each), so this stays simple instead of hand-rolling a chunked zip writer.
            var buffer = new MemoryStream();
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var attachment in attachments)
                {
                    Stream source;
                    try
                    {
                        source = await storage.OpenReadAsync(attachment.StorageKey, ct);
                    }
                    catch (FileNotFoundException)
                    {
                        continue;
                    }

                    var name = attachment.FileName;
                    if (!usedNames.Add(name))
                    {
                        name = $"{Path.GetFileNameWithoutExtension(attachment.FileName)}-{attachment.Id:N}{Path.GetExtension(attachment.FileName)}";
                        usedNames.Add(name);
                    }

                    var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
                    await using var entryStream = entry.Open();
                    await using (source)
                        await source.CopyToAsync(entryStream, ct);
                }
            }

            buffer.Position = 0;
            return Results.File(buffer, "application/zip", $"{reference}-attachments.zip");
        });
    }
}
