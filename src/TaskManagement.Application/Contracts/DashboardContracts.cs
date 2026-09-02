using TaskManagement.Domain;

namespace TaskManagement.Application.Contracts;

/// <summary>Headline counts for the signed-in user across every workspace they belong to.</summary>
public sealed record DashboardStatsDto(
    int Done,
    int InProgress,
    int Overdue,
    int Projects,
    int Workspaces,
    int OpenTotal);

/// <summary>One of the current user's open issues, with enough context to link straight to it.</summary>
public sealed record MyTaskDto(
    Guid Id,
    string Reference,
    string Title,
    IssueStatus Status,
    IssuePriority Priority,
    DateOnly? DueDate,
    bool IsOverdue,
    string ProjectName,
    string OrganizationSlug);

public sealed record DashboardDto(
    DashboardStatsDto Stats,
    IReadOnlyList<MyTaskDto> MyOpenTasks,
    IReadOnlyList<OrganizationDto> Workspaces);
