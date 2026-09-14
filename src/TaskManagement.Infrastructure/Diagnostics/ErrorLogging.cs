using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskManagement.Domain.Diagnostics;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Infrastructure.Diagnostics;

public sealed record ErrorLogEntry(string Level, string Category, string Message, string? Exception);

/// <summary>
/// Captures Warning/Error/Critical log entries from the app's own namespaces so operators can see what
/// went wrong from the admin dashboard instead of grepping server logs. Scoped to "TaskManagement."
/// categories only: framework/EF Core/Npgsql chatter is deliberately excluded, both to keep the table
/// meaningful and so this provider's own database writes (logged under
/// TaskManagement.Infrastructure.Diagnostics) can never feed back into itself — see
/// <see cref="ErrorLogDispatcher"/>, which never logs through <see cref="ILogger"/> for the same reason.
/// </summary>
public sealed class DatabaseLoggerProvider(Channel<ErrorLogEntry> queue) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new DatabaseLogger(categoryName, queue);

    public void Dispose()
    {
    }

    private sealed class DatabaseLogger(string categoryName, Channel<ErrorLogEntry> queue) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
            => logLevel >= LogLevel.Warning && categoryName.StartsWith("TaskManagement.", StringComparison.Ordinal);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            // Best-effort: a full queue silently drops rather than blocking whatever code just logged.
            queue.Writer.TryWrite(new ErrorLogEntry(logLevel.ToString(), categoryName, formatter(state, exception), exception?.ToString()));
        }
    }
}

/// <summary>
/// Drains captured entries into the <c>error_logs</c> table, one at a time. <see cref="IDbContextFactory{TContext}"/>
/// is registered scoped (so it resolves the ambient per-tenant <c>ITenantContext</c> correctly for normal
/// request/circuit work) — a singleton <see cref="BackgroundService"/> can't consume it directly, so a
/// fresh DI scope is created per entry instead. <c>ErrorLog</c> isn't tenant-scoped, so that scope having
/// no ambient tenant (background work has no HTTP/circuit context) doesn't affect what gets written.
/// </summary>
public sealed class ErrorLogDispatcher(Channel<ErrorLogEntry> queue, IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var entry in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbf = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
                await using var db = await dbf.CreateDbContextAsync(stoppingToken);
                db.ErrorLogs.Add(new ErrorLog(entry.Level, entry.Category, Truncate(entry.Message, 2000) ?? "", Truncate(entry.Exception, 8000)));
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never ILogger here: this category also starts with "TaskManagement." and would
                // immediately re-enter the same channel this method is draining.
                Console.Error.WriteLine($"ErrorLogDispatcher failed to persist a log entry: {ex}");
            }
        }
    }

    private static string? Truncate(string? value, int maxLength)
        => value is null || value.Length <= maxLength ? value : value[..maxLength];
}
