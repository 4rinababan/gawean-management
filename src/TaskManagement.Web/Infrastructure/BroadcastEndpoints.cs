using TaskManagement.Application.Abstractions;

namespace TaskManagement.Web.Infrastructure;

/// <summary>
/// Streams a broadcast's image. Plain HTTP endpoint (not a Blazor component) for the same reason as
/// <see cref="AttachmentEndpoints"/> — no slug/tenant scoping here, since a broadcast can target every
/// organization at once; any authenticated user may view it.
/// </summary>
public static class BroadcastEndpoints
{
    public static void MapBroadcastEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/broadcasts/{broadcastId:guid}/image", async (
            Guid broadcastId,
            BroadcastService broadcasts,
            IFileStorage storage,
            CancellationToken ct) =>
        {
            var image = await broadcasts.GetImageAsync(broadcastId, ct);
            if (image is null)
                return Results.NotFound();

            Stream stream;
            try
            {
                stream = await storage.OpenReadAsync(image.Value.StorageKey, ct);
            }
            catch (FileNotFoundException)
            {
                return Results.NotFound();
            }

            return Results.File(stream, image.Value.ContentType);
        }).RequireAuthorization();
    }
}
