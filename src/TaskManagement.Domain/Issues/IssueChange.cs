namespace TaskManagement.Domain.Issues;

/// <summary>An in-memory record of a single field change on an issue, emitted by the aggregate and consumed by the app layer.</summary>
public sealed record IssueChange(Guid IssueId, string Field, string? OldValue, string? NewValue, string ActorUserId);
