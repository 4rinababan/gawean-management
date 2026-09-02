using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain.Issues;
using TaskManagement.Domain.Notifications;
using TaskManagement.Domain.Organizations;
using TaskManagement.Domain.Projects;
using TaskManagement.Domain.Sprints;

namespace TaskManagement.Application.Abstractions;

/// <summary>
/// The persistence surface the application layer depends on. Implemented by the EF Core
/// <c>AppDbContext</c> in the infrastructure layer, which also applies tenant query filters.
/// </summary>
public interface IAppDbContext
{
    DbSet<Organization> Organizations { get; }
    DbSet<OrganizationMember> OrganizationMembers { get; }
    DbSet<Invitation> Invitations { get; }
    DbSet<Project> Projects { get; }
    DbSet<Issue> Issues { get; }
    DbSet<Comment> Comments { get; }
    DbSet<Attachment> Attachments { get; }
    DbSet<ActivityLog> ActivityLogs { get; }
    DbSet<Sprint> Sprints { get; }
    DbSet<Notification> Notifications { get; }

    /// <summary>Runs the given query with all tenant query filters disabled — for cross-tenant lookups such as redeeming an invitation.</summary>
    IQueryable<TEntity> IgnoringTenantFilter<TEntity>() where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
