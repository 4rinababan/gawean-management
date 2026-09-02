using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TaskManagement.Application.Abstractions;
using TaskManagement.Domain;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Infrastructure.Tests;

/// <summary>Mutable tenant/current-user doubles the application services and DbContext filters read from.</summary>
public sealed class FakeTenant : ITenantContext
{
    public Guid OrganizationId { get; private set; }
    public string Slug { get; private set; } = "";
    public OrgRole Role { get; private set; } = OrgRole.Admin;
    public bool IsResolved { get; private set; }

    public void Set(Guid organizationId, string slug, OrgRole role)
    {
        OrganizationId = organizationId;
        Slug = slug;
        Role = role;
        IsResolved = true;
    }

    public void Clear() => IsResolved = false;
}

public sealed class FakeCurrentUser : ICurrentUser
{
    public string? UserId { get; set; } = "user-1";
    public string? Email { get; set; } = "user-1@example.com";
}

public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 5, 9, 0, 0, TimeSpan.Zero);
}

/// <summary>
/// A disposable AppDbContext backed by a private in-memory SQLite database. Fast, transactional, and honours
/// EF Core global query filters, which is what the tenant-isolation tests exercise.
/// </summary>
public sealed class SqliteHarness : IDisposable
{
    private readonly SqliteConnection _connection;

    public FakeTenant Tenant { get; } = new();

    public IAppDbContextFactory Factory { get; }

    public SqliteHarness()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Factory = new SharedConnectionFactory(this);

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options, Tenant);
    }

    public void Dispose() => _connection.Dispose();

    private sealed class SharedConnectionFactory(SqliteHarness harness) : IAppDbContextFactory
    {
        public IAppDbContext CreateDbContext() => harness.CreateContext();
    }
}
