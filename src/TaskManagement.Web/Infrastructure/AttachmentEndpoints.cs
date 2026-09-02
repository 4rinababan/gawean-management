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
    }
}
