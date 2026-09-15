using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Domain;
using TaskManagement.Domain.Broadcasts;
using TaskManagement.Domain.Organizations;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Web.Infrastructure;

public sealed record BroadcastDto(Guid Id, string Message, string? ImageUrl, string TargetDescription, bool IsActive, DateTimeOffset CreatedAt);
public sealed record BroadcastPopupDto(Guid Id, string Message, string? ImageUrl);
public sealed record BroadcastUserSummaryDto(string Id, string Email, string DisplayName);

/// <summary>
/// Site-admin announcements shown as a one-time popup to matching users. Lives in the Web project
/// (like <see cref="AdminService"/>) because it needs direct access to Identity's ApplicationUser
/// table for the "specific users" picker, which the Application layer's IAppDbContext doesn't expose.
/// </summary>
public sealed class BroadcastService(IDbContextFactory<AppDbContext> dbf, IFileStorage storage)
{
    public async Task<IReadOnlyList<BroadcastDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var broadcasts = await db.Broadcasts.OrderByDescending(b => b.CreatedAt).ToListAsync(ct);

        var orgIds = broadcasts.Where(b => b.TargetOrganizationId is not null).Select(b => b.TargetOrganizationId!.Value).Distinct().ToList();
        var orgNames = await db.IgnoringTenantFilter<Organization>()
            .Where(o => orgIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Name, ct);

        return broadcasts.Select(b => new BroadcastDto(
            b.Id,
            b.Message,
            ImageUrl(b),
            b.TargetType switch
            {
                BroadcastTargetType.All => "All users",
                BroadcastTargetType.Organization => orgNames.GetValueOrDefault(b.TargetOrganizationId ?? Guid.Empty, "(deleted organization)"),
                BroadcastTargetType.Users => "Specific users",
                _ => "",
            },
            b.IsActive,
            b.CreatedAt)).ToList();
    }

    public async Task<Guid> CreateAsync(
        string message, Stream? image, string? imageFileName, string? imageContentType,
        string createdByUserId, BroadcastTargetType targetType, Guid? targetOrganizationId,
        IReadOnlyList<string>? targetUserIds, CancellationToken ct = default)
    {
        string? storageKey = image is not null && imageFileName is not null && imageContentType is not null
            ? await storage.SaveAsync(image, imageFileName, imageContentType, ct)
            : null;

        var broadcast = new Broadcast(message, storageKey, createdByUserId, targetType, targetOrganizationId);

        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Broadcasts.Add(broadcast);

        if (targetType == BroadcastTargetType.Users && targetUserIds is { Count: > 0 })
        {
            foreach (var userId in targetUserIds.Distinct())
                db.BroadcastTargetUsers.Add(new BroadcastTargetUser(broadcast.Id, userId));
        }

        await db.SaveChangesAsync(ct);
        return broadcast.Id;
    }

    public async Task DeactivateAsync(Guid broadcastId, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var broadcast = await db.Broadcasts.FirstOrDefaultAsync(b => b.Id == broadcastId, ct);
        if (broadcast is null) return;
        broadcast.Deactivate();
        await db.SaveChangesAsync(ct);
    }

    public async Task<(string StorageKey, string ContentType)?> GetImageAsync(Guid broadcastId, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var key = await db.Broadcasts.Where(b => b.Id == broadcastId).Select(b => b.ImageStorageKey).FirstOrDefaultAsync(ct);
        if (key is null) return null;
        var ext = Path.GetExtension(key).TrimStart('.').ToLowerInvariant();
        var contentType = ext switch { "png" => "image/png", "gif" => "image/gif", "webp" => "image/webp", _ => "image/jpeg" };
        return (key, contentType);
    }

    /// <summary>Search by partial email or display name for the "specific users" target picker.</summary>
    public async Task<IReadOnlyList<BroadcastUserSummaryDto>> SearchUsersAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2) return [];

        await using var db = await dbf.CreateDbContextAsync(ct);
        var q = query.Trim().ToLowerInvariant();
        return await db.Users
            .Where(u => u.Email!.ToLower().Contains(q) || u.DisplayName.ToLower().Contains(q))
            .OrderBy(u => u.Email)
            .Take(10)
            .Select(u => new BroadcastUserSummaryDto(u.Id, u.Email!, u.DisplayName))
            .ToListAsync(ct);
    }

    /// <summary>Every active, undismissed broadcast targeting this user — "All", the organization
    /// identified by <paramref name="currentOrgSlug"/> (if any), or an explicit pick of this user.</summary>
    public async Task<IReadOnlyList<BroadcastPopupDto>> GetUndismissedForUserAsync(string userId, string? currentOrgSlug, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);

        Guid? currentOrgId = null;
        if (!string.IsNullOrEmpty(currentOrgSlug))
        {
            currentOrgId = await db.IgnoringTenantFilter<Organization>()
                .Where(o => o.Slug == currentOrgSlug)
                .Select(o => (Guid?)o.Id)
                .FirstOrDefaultAsync(ct);
        }

        var dismissedIds = await db.BroadcastDismissals.Where(d => d.UserId == userId).Select(d => d.BroadcastId).ToListAsync(ct);
        var targetedIds = await db.BroadcastTargetUsers.Where(t => t.UserId == userId).Select(t => t.BroadcastId).ToListAsync(ct);

        var matches = await db.Broadcasts
            .Where(b => b.IsActive && !dismissedIds.Contains(b.Id))
            .Where(b => b.TargetType == BroadcastTargetType.All
                     || (b.TargetType == BroadcastTargetType.Organization && currentOrgId != null && b.TargetOrganizationId == currentOrgId)
                     || (b.TargetType == BroadcastTargetType.Users && targetedIds.Contains(b.Id)))
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(ct);

        return matches.Select(b => new BroadcastPopupDto(b.Id, b.Message, ImageUrl(b))).ToList();
    }

    public async Task DismissAsync(Guid broadcastId, string userId, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        if (await db.BroadcastDismissals.AnyAsync(d => d.BroadcastId == broadcastId && d.UserId == userId, ct))
            return;
        db.BroadcastDismissals.Add(new BroadcastDismissal(broadcastId, userId));
        await db.SaveChangesAsync(ct);
    }

    private static string? ImageUrl(Broadcast b) => b.ImageStorageKey is null ? null : $"/broadcasts/{b.Id}/image";
}
