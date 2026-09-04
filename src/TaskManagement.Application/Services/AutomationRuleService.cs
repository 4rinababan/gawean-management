using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Common;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain.Authorization;
using TaskManagement.Domain.Automation;

namespace TaskManagement.Application.Services;

public sealed class AutomationRuleService(
    IAppDbContextFactory dbf,
    IUserDirectory users,
    PermissionGuard guard)
{
    public async Task<IReadOnlyList<AutomationRuleDto>> GetForProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ViewContent);
        await using var db = dbf.CreateDbContext();
        await IssueService.RequireProjectAsync(db, projectId, guard.OrganizationId, ct);

        var rules = await db.AutomationRules
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        var directory = await users.GetManyAsync(rules.Select(r => r.CreatedByUserId), ct);
        return rules.Select(r => ToDto(r, directory)).ToList();
    }

    public async Task<Guid> CreateAsync(CreateAutomationRuleRequest request, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageProjects);
        await using var db = dbf.CreateDbContext();
        await IssueService.RequireProjectAsync(db, request.ProjectId, guard.OrganizationId, ct);

        var rule = new AutomationRule(
            guard.OrganizationId, request.ProjectId, request.Name, request.TriggerType, request.TriggerValue,
            request.Actions.Select(a => new AutomationAction(a.Type, a.Value)).ToList(), guard.UserId);

        db.AutomationRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return rule.Id;
    }

    public async Task UpdateAsync(Guid ruleId, UpdateAutomationRuleRequest request, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageProjects);
        await using var db = dbf.CreateDbContext();
        var rule = await RequireRuleAsync(db, ruleId, ct);

        rule.Update(request.Name, request.TriggerType, request.TriggerValue,
            request.Actions.Select(a => new AutomationAction(a.Type, a.Value)).ToList());
        await db.SaveChangesAsync(ct);
    }

    public async Task SetEnabledAsync(Guid ruleId, bool enabled, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageProjects);
        await using var db = dbf.CreateDbContext();
        var rule = await RequireRuleAsync(db, ruleId, ct);

        rule.SetEnabled(enabled);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid ruleId, CancellationToken ct = default)
    {
        guard.Require(OrgPermission.ManageProjects);
        await using var db = dbf.CreateDbContext();
        var rule = await RequireRuleAsync(db, ruleId, ct);

        db.AutomationRules.Remove(rule);
        await db.SaveChangesAsync(ct);
    }

    private async Task<AutomationRule> RequireRuleAsync(IAppDbContext db, Guid ruleId, CancellationToken ct)
        => await db.AutomationRules.FirstOrDefaultAsync(r => r.Id == ruleId && r.OrganizationId == guard.OrganizationId, ct)
           ?? throw NotFoundException.For<AutomationRule>(ruleId);

    private static AutomationRuleDto ToDto(AutomationRule r, IReadOnlyDictionary<string, UserSummary> directory)
        => new(r.Id, r.ProjectId, r.Name, r.Enabled, r.TriggerType, r.TriggerValue,
            r.Actions.Select(a => new AutomationActionDto(a.Type, a.Value)).ToList(),
            directory.TryGetValue(r.CreatedByUserId, out var u) ? u.DisplayName : "Unknown", r.CreatedAt);
}
