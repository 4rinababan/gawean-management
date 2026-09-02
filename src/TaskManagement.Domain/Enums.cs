namespace TaskManagement.Domain;

/// <summary>Role a user holds inside a single organization. Higher value = more privilege.</summary>
public enum OrgRole
{
    Viewer = 0,
    Member = 1,
    Admin = 2,
}

public enum IssueType
{
    Task = 0,
    Story = 1,
    Bug = 2,
    Epic = 3,
    SubTask = 4,
}

/// <summary>Workflow status of an issue. Ordered to match the default board columns left-to-right.</summary>
public enum IssueStatus
{
    Backlog = 0,
    Todo = 1,
    InProgress = 2,
    InReview = 3,
    Done = 4,
}

public enum IssuePriority
{
    Lowest = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Highest = 4,
}

public enum SprintState
{
    Planned = 0,
    Active = 1,
    Completed = 2,
}

public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Revoked = 2,
    Expired = 3,
}

public enum NotificationType
{
    IssueAssigned = 0,
    IssueCommented = 1,
    IssueMentioned = 2,
    IssueStatusChanged = 3,
    AddedToOrganization = 4,
    SprintStarted = 5,
}
