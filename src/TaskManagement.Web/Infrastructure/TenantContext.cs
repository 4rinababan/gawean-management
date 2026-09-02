using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Domain;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Web.Infrastructure;

/// <summary>Ambient organization for the current circuit/request. Populated by <see cref="TenantResolver"/> after membership is verified.</summary>
public sealed class TenantContext : ITenantContext
{
    public Guid OrganizationId { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public OrgRole Role { get; private set; }
    public bool IsResolved { get; private set; }

    public void Set(Guid organizationId, string slug, OrgRole role)
    {
        OrganizationId = organizationId;
        Slug = slug;
        Role = role;
        IsResolved = true;
    }
}

/// <summary>Resolves an organization slug from the route to the current user's membership, or reports why it can't.</summary>
public sealed class TenantResolver(IDbContextFactory<AppDbContext> dbf, ICurrentUser currentUser, ITenantContext tenant)
{
    public enum Outcome { Resolved, NotFound, NotAMember, NotAuthenticated }

    public async Task<Outcome> ResolveAsync(string slug, CancellationToken ct = default)
    {
        if (tenant.IsResolved && string.Equals(tenant.Slug, slug, StringComparison.OrdinalIgnoreCase))
            return Outcome.Resolved;

        var userId = currentUser.UserId;
        if (userId is null)
            return Outcome.NotAuthenticated;

        await using var db = await dbf.CreateDbContextAsync(ct);
        var match = await db.Organizations
            .IgnoreQueryFilters()
            .Where(o => o.Slug == slug)
            .Select(o => new
            {
                o.Id,
                o.Slug,
                Role = o.Members.Where(m => m.UserId == userId).Select(m => (OrgRole?)m.Role).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);

        if (match is null)
            return Outcome.NotFound;
        if (match.Role is null)
            return Outcome.NotAMember;

        tenant.Set(match.Id, match.Slug, match.Role.Value);
        return Outcome.Resolved;
    }
}
