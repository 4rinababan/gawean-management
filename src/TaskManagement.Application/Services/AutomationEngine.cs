using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Domain;
using TaskManagement.Domain.Automation;
using TaskManagement.Domain.Issues;
using TaskManagement.Domain.Notifications;

namespace TaskManagement.Application.Services;

/// <summary>
/// Matches enabled <see cref="AutomationRule"/>s for an issue's project against the <see cref="IssueChange"/>
/// list a mutation just produced, and applies matching rules' actions directly against the same
/// <see cref="Issue"/> aggregate — not through <see cref="IssueService"/>'s public methods, since those
/// enforce permissions against the interactively signed-in user, which has no meaning for a rule
/// execution. A rule's actions run under its <see cref="AutomationRule.CreatedByUserId"/> instead.
/// </summary>
public sealed class AutomationEngine(IAppUrls urls, ITenantContext tenant)
{
    /// <summary>
    /// Evaluates every enabled rule for <paramref name="issue"/>'s project exactly once, against the fixed
    /// <paramref name="changes"/> snapshot from the mutation that triggered this call. A rule's own actions
    /// can queue further <see cref="IssueChange"/>s on the aggregate, but those are never fed back into this
    /// matching loop — only the original snapshot is scanned — so one rule's action can never cause another
    /// rule (or itself) to fire again within the same call. That single design choice is the whole loop guard.
    /// </summary>
    public async Task<IReadOnlyList<(AutomationRule Rule, IReadOnlyCollection<IssueChange> Changes)>> MatchAndApplyAsync(
        IAppDbContext db, Issue issue, IReadOnlyCollection<IssueChange> changes, bool created,
        Dictionary<string, Notification> queued, CancellationToken ct)
    {
        var rules = await db.AutomationRules
            .Where(r => r.ProjectId == issue.ProjectId && r.Enabled)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

        var fired = new List<(AutomationRule, IReadOnlyCollection<IssueChange>)>();
        foreach (var rule in rules)
        {
            if (!Matches(rule, changes, created))
                continue;

            foreach (var action in rule.Actions)
                Apply(db, issue, rule, action, queued);

            var ruleChanges = issue.DequeueChanges();
            if (ruleChanges.Count > 0)
                fired.Add((rule, ruleChanges));
        }
        return fired;
    }

    private static bool Matches(AutomationRule rule, IReadOnlyCollection<IssueChange> changes, bool created) => rule.TriggerType switch
    {
        AutomationTriggerType.IssueCreated => created,
        AutomationTriggerType.StatusChanged => changes.Any(c =>
            c.Field == nameof(Issue.Status) && c.NewValue == rule.TriggerValue),
        AutomationTriggerType.AssigneeChanged => changes.Any(c => c.Field == nameof(Issue.AssigneeUserId)),
        AutomationTriggerType.PriorityChanged => changes.Any(c =>
            c.Field == nameof(Issue.Priority) && c.NewValue == rule.TriggerValue),
        _ => false,
    };

    private void Apply(IAppDbContext db, Issue issue, AutomationRule rule, AutomationAction action, Dictionary<string, Notification> queued)
    {
        switch (action.Type)
        {
            case AutomationActionType.SetStatus when Enum.TryParse<IssueStatus>(action.Value, out var status):
                issue.ChangeStatus(status, rule.CreatedByUserId);
                break;

            case AutomationActionType.SetAssignee:
                issue.Assign(string.IsNullOrEmpty(action.Value) ? null : action.Value, rule.CreatedByUserId);
                break;

            case AutomationActionType.SetPriority when Enum.TryParse<IssuePriority>(action.Value, out var priority):
                issue.ChangePriority(priority, rule.CreatedByUserId);
                break;

            case AutomationActionType.AddComment when !string.IsNullOrWhiteSpace(action.Value):
                var comment = issue.AddComment(rule.CreatedByUserId, action.Value);
                db.Comments.Add(comment); // client-generated id: force Added rather than EF's key-is-set heuristic
                break;

            case AutomationActionType.Notify:
                var targetUserId = action.Value switch
                {
                    "assignee" => issue.AssigneeUserId,
                    "reporter" => issue.ReporterUserId,
                    _ => action.Value,
                };
                if (!string.IsNullOrEmpty(targetUserId))
                {
                    queued[targetUserId] = new Notification(
                        issue.OrganizationId, targetUserId, NotificationType.AutomationRuleFired,
                        $"Automation \"{rule.Name}\" ran on {issue.Title}", issue.Id, urls.Issue(tenant.Slug, issue.Id));
                }
                break;
        }
    }
}
