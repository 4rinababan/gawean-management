using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TaskManagement.Application.Abstractions;
using TaskManagement.Domain;

namespace TaskManagement.Infrastructure.Persistence;

/// <summary>Design-time factory so <c>dotnet ef</c> can build the context without the web host. Uses a throw-away connection string.</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MIGRATIONS_CONNECTION")
            ?? "Host=localhost;Database=taskmanagement;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(AppDbContextFactory).Assembly.FullName))
            .Options;

        return new AppDbContext(options, new NullTenantContext());
    }

    private sealed class NullTenantContext : ITenantContext
    {
        public Guid OrganizationId => Guid.Empty;
        public string Slug => string.Empty;
        public OrgRole Role => OrgRole.Viewer;
        public bool IsResolved => false;
        public void Set(Guid organizationId, string slug, OrgRole role) { }
    }
}
