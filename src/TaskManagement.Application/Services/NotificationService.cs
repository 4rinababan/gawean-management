using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Common;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain.Notifications;

namespace TaskManagement.Application.Services;

public sealed class NotificationService(IAppDbContextFactory dbf, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<NotificationDto>> GetRecentAsync(int take = 20, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        await using var db = dbf.CreateDbContext();

        return await db.IgnoringTenantFilter<Notification>()
            .Where(n => n.RecipientUserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Message, n.IssueId, n.Url, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        await using var db = dbf.CreateDbContext();
        return await db.IgnoringTenantFilter<Notification>()
            .CountAsync(n => n.RecipientUserId == userId && !n.IsRead, ct);
    }

    public async Task MarkReadAsync(Guid notificationId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        await using var db = dbf.CreateDbContext();
        var notification = await db.IgnoringTenantFilter<Notification>()
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientUserId == userId, ct)
            ?? throw NotFoundException.For<Notification>(notificationId);

        notification.MarkRead();
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        await using var db = dbf.CreateDbContext();
        await db.IgnoringTenantFilter<Notification>()
            .Where(n => n.RecipientUserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }
}
