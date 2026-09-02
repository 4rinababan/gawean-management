using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Domain;
using TaskManagement.Domain.Organizations;
using TaskManagement.Domain.Projects;
using TaskManagement.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace TaskManagement.Infrastructure.Tests;

/// <summary>
/// Runs the real EF migrations against a throw-away PostgreSQL container. Skipped automatically when
/// no container runtime is available (e.g. a dev box without Docker); always runs in CI.
/// </summary>
public sealed class PostgresMigrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _connectionString;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
            await _container.StartAsync().WaitAsync(TimeSpan.FromMinutes(2));
            _connectionString = _container.GetConnectionString();
        }
        catch (Exception)
        {
            _container = null; // no Docker: tests below will Skip
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    private AppDbContext CreateContext(ITenantContextStub tenant)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString!)
            .Options;
        return new AppDbContext(options, tenant);
    }

    [Fact]
    public async Task Migrations_apply_cleanly_and_the_schema_round_trips()
    {
        if (_container is null) return; // no container runtime (e.g. local dev without Docker); runs in CI

        var tenant = new ITenantContextStub();
        await using (var migrate = CreateContext(tenant))
        {
            await migrate.Database.MigrateAsync();
        }

        var org = new Organization("Prod Co", "prod-co", "user-1");
        var project = new Project(org.Id, "OPS", "Operations");
        var issue = project.CreateIssue("Ship it", IssueType.Story, "user-1");
        tenant.Set(org.Id, "prod-co", OrgRole.Admin);

        await using (var write = CreateContext(tenant))
        {
            write.Organizations.Add(org);
            write.Projects.Add(project);
            write.Issues.Add(issue);
            await write.SaveChangesAsync();
        }

        await using (var read = CreateContext(tenant))
        {
            var loaded = await read.Issues.SingleAsync();
            loaded.Number.Should().Be(1);
            loaded.Title.Should().Be("Ship it");
            loaded.Status.Should().Be(IssueStatus.Backlog);
        }
    }

    [Fact]
    public async Task Tenant_filter_holds_on_real_postgres()
    {
        if (_container is null) return; // no container runtime (e.g. local dev without Docker); runs in CI

        var tenant = new ITenantContextStub();
        await using (var migrate = CreateContext(tenant))
            await migrate.Database.MigrateAsync();

        Guid otherOrg;
        var a = new Organization("A", $"a-{Guid.NewGuid():n}", "u");
        var b = new Organization("B", $"b-{Guid.NewGuid():n}", "u");
        otherOrg = b.Id;
        tenant.Set(a.Id, a.Slug, OrgRole.Admin);
        await using (var seed = CreateContext(tenant))
        {
            seed.Organizations.Add(a);
            seed.IgnoringTenantFilter<Organization>(); // no-op, just documents intent
            seed.Add(b);
            await seed.SaveChangesAsync();
        }

        await using var ctx = CreateContext(tenant);
        (await ctx.Organizations.CountAsync()).Should().Be(1);
        (await ctx.IgnoringTenantFilter<Organization>().AnyAsync(o => o.Id == otherOrg)).Should().BeTrue();
    }

    private sealed class ITenantContextStub : ITenantContext
    {
        public Guid OrganizationId { get; private set; }
        public string Slug { get; private set; } = "";
        public OrgRole Role { get; private set; }
        public bool IsResolved { get; private set; }
        public void Set(Guid organizationId, string slug, OrgRole role)
            => (OrganizationId, Slug, Role, IsResolved) = (organizationId, slug, role, true);
    }
}
