using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Automation;

/// <summary>
/// A project-scoped "when X happens, do Y" rule. <c>AutomationEngine</c> evaluates enabled rules against
/// the same field-diff <c>IssueChange</c> list that already flows through <c>IssueChangeProcessor</c> on
/// every issue mutation — see that class for how rule actions are applied without cascading into further
/// rule evaluation.
/// </summary>
public sealed class AutomationRule : Entity, ITenantScoped
{
    private AutomationRule() { }

    public AutomationRule(
        Guid organizationId, Guid projectId, string name, AutomationTriggerType triggerType,
        string? triggerValue, IReadOnlyList<AutomationAction> actions, string createdByUserId)
    {
        OrganizationId = organizationId;
        ProjectId = projectId;
        CreatedByUserId = Guard.NotBlank(createdByUserId, nameof(createdByUserId));
        Enabled = true;
        Update(name, triggerType, triggerValue, actions);
    }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool Enabled { get; private set; }

    public AutomationTriggerType TriggerType { get; private set; }

    /// <summary>The trigger's target value, e.g. the status name for StatusChanged. Unused (null) for
    /// IssueCreated/AssigneeChanged, which have no further condition beyond the trigger itself.</summary>
    public string? TriggerValue { get; private set; }

    public IReadOnlyList<AutomationAction> Actions { get; private set; } = [];

    /// <summary>Actions run under this user's identity — they're attributed in the activity log to
    /// whoever configured the rule, not a synthetic system actor, and are as authorized as that person was.</summary>
    public string CreatedByUserId { get; private set; } = string.Empty;

    public void Update(string name, AutomationTriggerType triggerType, string? triggerValue, IReadOnlyList<AutomationAction> actions)
    {
        Name = Guard.NotBlank(name, nameof(name));
        TriggerType = triggerType;
        TriggerValue = ValidateTriggerValue(triggerType, triggerValue);
        Actions = ValidateActions(actions);
    }

    public void SetEnabled(bool enabled) => Enabled = enabled;

    private static string? ValidateTriggerValue(AutomationTriggerType type, string? value) => type switch
    {
        AutomationTriggerType.StatusChanged when !Enum.TryParse<IssueStatus>(value, out _) =>
            throw new DomainException("A status-change trigger needs a valid target status."),
        AutomationTriggerType.PriorityChanged when !Enum.TryParse<IssuePriority>(value, out _) =>
            throw new DomainException("A priority-change trigger needs a valid target priority."),
        AutomationTriggerType.StatusChanged or AutomationTriggerType.PriorityChanged => value,
        _ => null,
    };

    private static IReadOnlyList<AutomationAction> ValidateActions(IReadOnlyList<AutomationAction> actions)
    {
        if (actions is not { Count: > 0 })
            throw new DomainException("A rule needs at least one action.");

        foreach (var action in actions)
        {
            var valid = action.Type switch
            {
                AutomationActionType.SetStatus => Enum.TryParse<IssueStatus>(action.Value, out _),
                AutomationActionType.SetPriority => Enum.TryParse<IssuePriority>(action.Value, out _),
                AutomationActionType.SetAssignee => true, // empty/null value means "unassign"
                AutomationActionType.AddComment or AutomationActionType.Notify => !string.IsNullOrWhiteSpace(action.Value),
                _ => false,
            };
            if (!valid)
                throw new DomainException($"Action \"{action.Type}\" has an invalid value.");
        }
        return actions;
    }
}
