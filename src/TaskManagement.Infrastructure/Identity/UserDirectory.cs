using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Infrastructure.Identity;

/// <summary>Read-only lookups over the Identity user table for display names, avatars and @mention resolution.</summary>
public sealed class UserDirectory(IDbContextFactory<AppDbContext> dbf) : IUserDirectory
{
    private static readonly Expression<Func<ApplicationUser, UserSummary>> ToSummary = u => new UserSummary(
        u.Id,
        (u.DisplayName == null || u.DisplayName == "") ? (u.UserName ?? u.Email ?? "User") : u.DisplayName,
        u.Email ?? string.Empty,
        u.UserName,
        u.AvatarColor);

    public async Task<IReadOnlyDictionary<string, UserSummary>> GetManyAsync(IEnumerable<string> userIds, CancellationToken ct = default)
    {
        var ids = userIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<string, UserSummary>();

        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Users.Where(u => ids.Contains(u.Id)).Select(ToSummary).ToDictionaryAsync(u => u.Id, ct);
    }

    public async Task<UserSummary?> GetAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Users.Where(u => u.Id == userId).Select(ToSummary).FirstOrDefaultAsync(ct);
    }

    public async Task<UserSummary?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToUpperInvariant();
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Users.Where(u => u.NormalizedEmail == normalized).Select(ToSummary).FirstOrDefaultAsync(ct);
    }

    public async Task<UserSummary?> FindByUsernameAsync(string username, CancellationToken ct = default)
    {
        var normalized = username.Trim().ToUpperInvariant();
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Users.Where(u => u.NormalizedUserName == normalized).Select(ToSummary).FirstOrDefaultAsync(ct);
    }
}
