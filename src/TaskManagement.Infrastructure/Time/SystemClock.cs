using TaskManagement.Application.Abstractions;

namespace TaskManagement.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
