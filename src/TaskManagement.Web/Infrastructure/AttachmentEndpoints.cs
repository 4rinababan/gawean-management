using TaskManagement.Application.Common;
using TaskManagement.Application.Services;

namespace TaskManagement.Web.Infrastructure;

/// <summary>Minimal-API endpoints for streaming attachment content, scoped to the organization in the route.</summary>
public static class AttachmentEndpoints
{
    public static void MapAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/{slug}/attachments").RequireAuthorization();

        group.MapGet("/{attachmentId:guid}", async (
            string slug,
            Guid attachmentId,
            TenantResolver tenantResolver,
            AttachmentService attachments,
            CancellationToken ct) =>
        {
            if (await tenantResolver.ResolveAsync(slug, ct) != TenantResolver.Outcome.Resolved)
                return Results.Forbid();

            try
            {
                var content = await attachments.DownloadAsync(attachmentId, ct);
                return Results.Stream(content.Stream, content.ContentType, content.FileName);
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
            catch (ForbiddenException)
            {
                return Results.Forbid();
            }
        });
    }
}
