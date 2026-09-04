using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using TaskManagement.Application.Abstractions;

namespace TaskManagement.Infrastructure.Ai;

/// <summary>
/// In-process, per-user fixed-window limiter for <see cref="IAiAssistant"/> calls. Registered as a
/// singleton — the whole point is that its state outlives any single request.
/// </summary>
public sealed class AiRateLimiter : IAiRateLimiter, IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter;

    public AiRateLimiter(IOptions<AiOptions> options)
    {
        var permitLimit = Math.Max(1, options.Value.RateLimitPerHour);
        _limiter = PartitionedRateLimiter.Create<string, string>(userId =>
            RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
            }));
    }

    public bool TryAcquire(string userId) => _limiter.AttemptAcquire(userId).IsAcquired;

    public void Dispose() => _limiter.Dispose();
}
