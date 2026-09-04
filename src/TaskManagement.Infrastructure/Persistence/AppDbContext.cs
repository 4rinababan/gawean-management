using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TaskManagement.Application.Abstractions;
using TaskManagement.Domain.Automation;
using TaskManagement.Domain.Common;
using TaskManagement.Domain.Issues;
using TaskManagement.Domain.Notifications;
using TaskManagement.Domain.Organizations;
using TaskManagement.Domain.Projects;
using TaskManagement.Domain.Sprints;
using TaskManagement.Infrastructure.Identity;

namespace TaskManagement.Infrastructure.Persistence;

/// <summary>
/// Single EF Core context for both Identity and the task domain. Applies per-tenant global query filters
/// driven by the ambient <see cref="ITenantContext"/> so a request only ever sees its own organization's data.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant)
    : IdentityDbContext<ApplicationUser>(options), IAppDbContext
{
    private Guid TenantId => tenant.IsResolved ? tenant.OrganizationId : Guid.Empty;

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<OrganizationAuditLog> OrganizationAuditLogs => Set<OrganizationAuditLog>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<IssueViewer> IssueViewers => Set<IssueViewer>();
    public DbSet<IssueAiNote> IssueAiNotes => Set<IssueAiNote>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();

    public IQueryable<TEntity> IgnoringTenantFilter<TEntity>() where TEntity : class
        => Set<TEntity>().IgnoreQueryFilters();

    /// <summary>
    /// SQLite has no native DateTimeOffset and refuses to ORDER BY one, which would make the whole
    /// "newest first" family of queries untestable against the in-memory SQLite harness. Storing them
    /// as a sortable binary value there keeps those paths covered; PostgreSQL uses timestamptz as normal.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        base.ConfigureConventions(builder);

        if (Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
            builder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Tenant isolation: every ITenantScoped entity is filtered to the ambient organization.
        builder.Entity<Organization>().HasQueryFilter(o => o.Id == TenantId);
        builder.Entity<OrganizationMember>().HasQueryFilter(e => e.OrganizationId == TenantId);
        builder.Entity<Invitation>().HasQueryFilter(e => e.OrganizationId == TenantId);
        builder.Entity<OrganizationAuditLog>().HasQueryFilter(e => e.OrganizationId == TenantId);
        builder.Entity<Project>().HasQueryFilter(e => e.OrganizationId == TenantId);
        builder.Entity<Issue>().HasQueryFilter(e => e.OrganizationId == TenantId);
        builder.Entity<Comment>().HasQueryFilter(e => e.OrganizationId == TenantId);
        builder.Entity<Attachment>().HasQueryFilter(e => e.OrganizationId == TenantId);
        builder.Entity<IssueViewer>().HasQueryFilter(e => e.OrganizationId == TenantId);
        builder.Entity<IssueAiNote>().HasQueryFilter(e => e.OrganizationId == TenantId);
        builder.Entity<ActivityLog>().HasQueryFilter(e => e.OrganizationId == TenantId);
        builder.Entity<Sprint>().HasQueryFilter(e => e.OrganizationId == TenantId);
        builder.Entity<Notification>().HasQueryFilter(e => e.OrganizationId == TenantId);
        builder.Entity<AutomationRule>().HasQueryFilter(e => e.OrganizationId == TenantId);
    }

    public override int SaveChanges()
    {
        TouchTimestamps();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        TouchTimestamps();
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // The default message doesn't say which row vanished; name the entities so the log is actionable.
            throw new DbUpdateConcurrencyException($"{ex.Message} Offending entries: {DescribeEntries(ex.Entries)}.", ex);
        }
    }

    private static string DescribeEntries(IReadOnlyList<EntityEntry> entries)
        => entries.Count == 0
            ? "(none reported)"
            : string.Join(", ", entries.Select(e =>
            {
                var key = e.Metadata.FindPrimaryKey()?.Properties
                    .Select(p => $"{p.Name}={e.Property(p.Name).CurrentValue}");
                return $"{e.Metadata.ClrType.Name}[{e.State}] {(key is null ? "" : string.Join('/', key))}";
            }));

    private void TouchTimestamps()
    {
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
