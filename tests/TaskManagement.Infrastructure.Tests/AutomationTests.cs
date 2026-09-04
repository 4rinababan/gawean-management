using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Contracts;
using TaskManagement.Application.Services;
using TaskManagement.Domain;
using TaskManagement.Domain.Automation;
using TaskManagement.Domain.Organizations;
using TaskManagement.Domain.Projects;

namespace TaskManagement.Infrastructure.Tests;

public class AutomationTests : IDisposable
{
    private readonly ServiceFixture _fx = new();
    private readonly Guid _projectId;

    public AutomationTests()
    {
        using var db = _fx.Db();
        var org = new Organization("Alpha", "alpha", "user-1");
        var project = new Project(org.Id, "WEB", "Web");
        db.Organizations.Add(org);
        db.Projects.Add(project);
        db.SaveChanges();
        _projectId = project.Id;
        _fx.Tenant.Set(org.Id, "alpha", OrgRole.Admin);
    }

    private Task<Guid> CreateRuleAsync(AutomationTriggerType trigger, string? triggerValue, params AutomationActionDto[] actions)
        => _fx.Build<AutomationRuleService>().CreateAsync(new CreateAutomationRuleRequest
        {
            ProjectId = _projectId,
            Name = "Test rule",
            TriggerType = trigger,
            TriggerValue = triggerValue,
            Actions = [.. actions],
        });

    [Fact]
    public async Task Status_change_rule_clears_the_assignee()
    {
        var issues = _fx.Build<IssueService>();
        var issueId = await issues.CreateAsync(new CreateIssueRequest { ProjectId = _projectId, Title = "T", Type = IssueType.Task });
        await issues.AssignAsync(issueId, "user-2");

        await CreateRuleAsync(AutomationTriggerType.StatusChanged, nameof(IssueStatus.Done),
            new AutomationActionDto(AutomationActionType.SetAssignee, null));

        await issues.ChangeStatusAsync(issueId, IssueStatus.Done);

        await using var db = _fx.Db();
        (await db.Issues.FindAsync(issueId))!.AssigneeUserId.Should().BeNull();
    }

    [Fact]
    public async Task Issue_created_rule_fires_even_for_a_bare_title_create()
    {
        await CreateRuleAsync(AutomationTriggerType.IssueCreated, null,
            new AutomationActionDto(AutomationActionType.AddComment, "Welcome!"));

        var issueId = await _fx.Build<IssueService>()
            .CreateAsync(new CreateIssueRequest { ProjectId = _projectId, Title = "Bare", Type = IssueType.Task });

        await using var db = _fx.Db();
        (await db.Comments.CountAsync(c => c.IssueId == issueId && c.Body == "Welcome!")).Should().Be(1);
    }

    [Fact]
    public async Task A_rules_own_action_does_not_cascade_into_a_second_rule()
    {
        // Rule A: moving to InProgress pushes the issue straight to Done.
        await CreateRuleAsync(AutomationTriggerType.StatusChanged, nameof(IssueStatus.InProgress),
            new AutomationActionDto(AutomationActionType.SetStatus, nameof(IssueStatus.Done)));
        // Rule B: reaching Done would (if cascading were allowed) add a comment.
        await CreateRuleAsync(AutomationTriggerType.StatusChanged, nameof(IssueStatus.Done),
            new AutomationActionDto(AutomationActionType.AddComment, "B fired"));

        var issueId = await _fx.Build<IssueService>()
            .CreateAsync(new CreateIssueRequest { ProjectId = _projectId, Title = "T", Type = IssueType.Task });

        await _fx.Build<IssueService>().ChangeStatusAsync(issueId, IssueStatus.InProgress);

        await using var db = _fx.Db();
        (await db.Issues.FindAsync(issueId))!.Status.Should().Be(IssueStatus.Done); // Rule A did fire
        (await db.Comments.CountAsync(c => c.IssueId == issueId)).Should().Be(0);    // Rule B did not
    }

    [Fact]
    public async Task A_disabled_rule_never_fires()
    {
        var ruleId = await CreateRuleAsync(AutomationTriggerType.StatusChanged, nameof(IssueStatus.Done),
            new AutomationActionDto(AutomationActionType.AddComment, "Should not appear"));
        await _fx.Build<AutomationRuleService>().SetEnabledAsync(ruleId, false);

        var issueId = await _fx.Build<IssueService>()
            .CreateAsync(new CreateIssueRequest { ProjectId = _projectId, Title = "T", Type = IssueType.Task });
        await _fx.Build<IssueService>().ChangeStatusAsync(issueId, IssueStatus.Done);

        await using var db = _fx.Db();
        (await db.Comments.CountAsync(c => c.IssueId == issueId)).Should().Be(0);
    }

    public void Dispose() => _fx.Dispose();
}
