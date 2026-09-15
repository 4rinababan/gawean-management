using Microsoft.AspNetCore.Identity;
using TaskManagement.Application.Abstractions;
using TaskManagement.Infrastructure.Identity;

namespace TaskManagement.Web.Infrastructure;

/// <summary>Uploads/removes the current user's profile photo. Web-layer because it needs UserManager directly.</summary>
public sealed class ProfileService(UserManager<ApplicationUser> userManager, IFileStorage storage)
{
    public static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/webp", "image/gif"];

    public async Task UpdateAvatarAsync(string userId, Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        if (!AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Please upload a JPEG, PNG, WEBP or GIF image.");

        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");

        var oldKey = user.AvatarStorageKey;
        var newKey = await storage.SaveAsync(content, fileName, contentType, ct);

        user.AvatarStorageKey = newKey;
        user.AvatarContentType = contentType;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            await storage.DeleteAsync(newKey, ct);
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        if (!string.IsNullOrEmpty(oldKey))
        {
            try { await storage.DeleteAsync(oldKey, ct); }
            catch (FileNotFoundException) { /* already gone */ }
        }
    }

    public async Task RemoveAvatarAsync(string userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");

        var oldKey = user.AvatarStorageKey;
        if (oldKey is null)
            return;

        user.AvatarStorageKey = null;
        user.AvatarContentType = null;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));

        try { await storage.DeleteAsync(oldKey, ct); }
        catch (FileNotFoundException) { /* already gone */ }
    }
}
