using TaskManagement.Domain;
using TaskManagement.Domain.Sprints;

namespace TaskManagement.Application.Contracts;

public sealed class CreateSprintRequest
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Goal { get; set; }
}

public sealed class UpdateSprintRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Goal { get; set; }
}

public sealed class StartSprintRequest
{
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(14));
}

public sealed record SprintDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Goal,
    SprintState State,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int IssueCount,
    int CompletedPoints,
    int TotalPoints);

public sealed record BurndownDto(Guid SprintId, string SprintName, IReadOnlyList<BurndownPoint> Points);

public sealed record NotificationDto(Guid Id, NotificationType Type, string Message, Guid? IssueId, string? Url, bool IsRead, DateTimeOffset CreatedAt);
