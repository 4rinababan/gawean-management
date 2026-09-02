using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;

namespace TaskManagement.Infrastructure.Persistence;

/// <summary>Adapts EF Core's <see cref="IDbContextFactory{TContext}"/> to the application's <see cref="IAppDbContextFactory"/>.</summary>
public sealed class AppDbContextFactoryAdapter(IDbContextFactory<AppDbContext> inner) : IAppDbContextFactory
{
    public IAppDbContext CreateDbContext() => inner.CreateDbContext();
}
