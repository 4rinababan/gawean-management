using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain.Issues;
using TaskManagement.Domain.Organizations;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Web.Infrastructure;

public sealed record AdminUserDto(string Id, string Email, string DisplayName, bool EmailConfirmed, bool LockedOut, bool IsSiteAdmin, DateTimeOffset CreatedAt);
public sealed record AdminWorkspaceDto(Guid Id, string Name, string Slug, int MemberCount, DateTimeOffset CreatedAt);
public sealed record AdminErrorLogDto(Guid Id, string Level, string Category, string Message, string? Exception, DateTimeOffset CreatedAt);
public sealed record AdminOverviewDto(int UserCount, int WorkspaceCount, int IssueCount, int ErrorCount24h);

/// <summary>
/// Cross-tenant, site-wide queries for the /admin dashboard. Unlike the application services under
/// TaskManagement.Application, this one lives in the Web project because it needs direct access to
/// Identity's ApplicationUser table, which the Application layer's IAppDbContext abstraction doesn't
/// expose (Identity is wired up in the Web host, not the Application layer).
/// </summary>
public sealed class AdminService(IDbContextFactory<AppDbContext> dbf)
{
    public async Task<bool> IsSiteAdminAsync(string? userId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userId))
            return false;

        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Users.AnyAsync(u => u.Id == userId && u.IsSiteAdmin, ct);
    }

    public async Task<AdminOverviewDto> GetOverviewAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var since = DateTimeOffset.UtcNow.AddHours(-24);

        return new AdminOverviewDto(
            await db.Users.CountAsync(ct),
            await db.IgnoringTenantFilter<Organization>().CountAsync(ct),
            await db.IgnoringTenantFilter<Issue>().CountAsync(ct),
            await db.ErrorLogs.CountAsync(e => e.CreatedAt >= since, ct));
    }

    public async Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Users
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new AdminUserDto(
                u.Id,
                u.Email ?? "",
                u.DisplayName,
                u.EmailConfirmed,
                u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow,
                u.IsSiteAdmin,
                u.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AdminWorkspaceDto>> GetWorkspacesAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.IgnoringTenantFilter<Organization>()
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new AdminWorkspaceDto(o.Id, o.Name, o.Slug, o.Members.Count, o.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AdminErrorLogDto>> GetErrorLogsAsync(int take = 100, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.ErrorLogs
            .OrderByDescending(e => e.CreatedAt)
            .Take(take)
            .Select(e => new AdminErrorLogDto(e.Id, e.Level, e.Category, e.Message, e.Exception, e.CreatedAt))
            .ToListAsync(ct);
    }
}
