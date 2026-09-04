namespace TaskManagement.Domain.Automation;

public enum AutomationTriggerType
{
    IssueCreated = 0,
    StatusChanged = 1,
    AssigneeChanged = 2,
    PriorityChanged = 3,
}
