using TaskManagement.Domain.Automation;

namespace TaskManagement.Application.Contracts;

/// <summary>Mutable (not a record) like the other form-bound contracts, so Blazor's <c>EditForm</c> can
/// two-way bind individual rows in an <see cref="CreateAutomationRuleRequest.Actions"/> list.</summary>
public sealed class AutomationActionDto
{
    public AutomationActionType Type { get; set; }
    public string? Value { get; set; }

    public AutomationActionDto() { }

    public AutomationActionDto(AutomationActionType type, string? value)
    {
        Type = type;
        Value = value;
    }
}

public sealed class CreateAutomationRuleRequest
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AutomationTriggerType TriggerType { get; set; }
    public string? TriggerValue { get; set; }
    public List<AutomationActionDto> Actions { get; set; } = [];
}

public sealed class UpdateAutomationRuleRequest
{
    public string Name { get; set; } = string.Empty;
    public AutomationTriggerType TriggerType { get; set; }
    public string? TriggerValue { get; set; }
    public List<AutomationActionDto> Actions { get; set; } = [];
}

public sealed record AutomationRuleDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    bool Enabled,
    AutomationTriggerType TriggerType,
    string? TriggerValue,
    IReadOnlyList<AutomationActionDto> Actions,
    string CreatedByDisplayName,
    DateTimeOffset CreatedAt);
