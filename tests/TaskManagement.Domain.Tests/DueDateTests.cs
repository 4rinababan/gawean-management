using TaskManagement.Domain;
using TaskManagement.Domain.Projects;

namespace TaskManagement.Domain.Tests;

public class DueDateTests
{
    private static readonly DateOnly Today = new(2026, 3, 10);

    private static Issues.Issue NewIssue()
        => new Project(Guid.CreateVersion7(), "WEB", "Web").CreateIssue("T", IssueType.Task, "u1");

    [Fact]
    public void An_issue_without_a_due_date_is_never_overdue()
        => NewIssue().IsOverdue(Today).Should().BeFalse();

    [Fact]
    public void A_due_date_in_the_past_makes_an_open_issue_overdue()
    {
        var issue = NewIssue();
        issue.SetDueDate(Today.AddDays(-1), "u1");

        issue.IsOverdue(Today).Should().BeTrue();
    }

    [Fact]
    public void Due_today_is_not_yet_overdue()
    {
        var issue = NewIssue();
        issue.SetDueDate(Today, "u1");

        issue.IsOverdue(Today).Should().BeFalse();
    }

    [Fact]
    public void A_done_issue_is_never_overdue_however_late_it_was()
    {
        var issue = NewIssue();
        issue.SetDueDate(Today.AddDays(-30), "u1");
        issue.ChangeStatus(IssueStatus.Done, "u1");

        issue.IsOverdue(Today).Should().BeFalse();
    }

    [Fact]
    public void Setting_a_due_date_is_recorded_for_the_activity_log()
    {
        var issue = NewIssue();
        issue.DequeueChanges();

        issue.SetDueDate(new DateOnly(2026, 4, 1), "u1");

        issue.DequeueChanges().Should().ContainSingle(c => c.Field == "DueDate" && c.NewValue == "2026-04-01");
    }

    [Fact]
    public void Clearing_a_due_date_is_recorded()
    {
        var issue = NewIssue();
        issue.SetDueDate(Today, "u1");
        issue.DequeueChanges();

        issue.SetDueDate(null, "u1");

        issue.DequeueChanges().Should().ContainSingle(c => c.Field == "DueDate" && c.NewValue == null);
    }
}
