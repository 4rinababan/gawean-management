using Microsoft.AspNetCore.Identity;
using TaskManagement.Application.Abstractions;
using TaskManagement.Infrastructure.Identity;

namespace TaskManagement.Web.Infrastructure;

/// <summary>
/// Streams a user's uploaded profile photo. Plain HTTP endpoint (not a Blazor component) so the
/// <c>&lt;img&gt;</c> tags in <see cref="Components.Ui.Avatar"/> can reference it directly by user id
/// without every caller needing to know whether that user actually has a photo — a missing one just 404s
/// and the component's <c>onerror</c> handler falls back to the initials.
/// </summary>
public static class AvatarEndpoints
{
    public static void MapAvatarEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/avatars/{userId}", async (
            string userId,
            UserManager<ApplicationUser> users,
            IFileStorage storage,
            CancellationToken ct) =>
        {
            var user = await users.FindByIdAsync(userId);
            if (user?.AvatarStorageKey is not { Length: > 0 } key)
                return Results.NotFound();

            Stream stream;
            try
            {
                stream = await storage.OpenReadAsync(key, ct);
            }
            catch (FileNotFoundException)
            {
                return Results.NotFound();
            }

            return Results.File(stream, user.AvatarContentType ?? "image/jpeg");
        }).RequireAuthorization();
    }
}
