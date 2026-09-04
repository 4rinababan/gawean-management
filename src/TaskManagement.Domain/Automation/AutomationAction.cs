namespace TaskManagement.Domain.Automation;

/// <summary>
/// One step in a rule's action list. <see cref="Value"/>'s meaning depends on <see cref="Type"/>:
/// SetStatus/SetPriority — the target enum name; SetAssignee — a user id, or empty/null to unassign;
/// AddComment — literal comment text; Notify — "assignee", "reporter", or a specific user id.
/// </summary>
public sealed record AutomationAction(AutomationActionType Type, string? Value);
