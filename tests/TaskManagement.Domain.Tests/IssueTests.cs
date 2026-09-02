using TaskManagement.Domain;
using TaskManagement.Domain.Common;
using TaskManagement.Domain.Issues;
using TaskManagement.Domain.Projects;

namespace TaskManagement.Domain.Tests;

public class IssueTests
{
    private static Project NewProject() => new(Guid.CreateVersion7(), "WEB", "Website");

    [Fact]
    public void Project_hands_out_sequential_issue_numbers()
    {
        var project = NewProject();

        var first = project.CreateIssue("A", IssueType.Task, "u1");
        var second = project.CreateIssue("B", IssueType.Bug, "u1");

        first.Number.Should().Be(1);
        second.Number.Should().Be(2);
        project.IssueSequence.Should().Be(2);
    }

    [Theory]
    [InlineData("w")]
    [InlineData("1AB")]
    [InlineData("TOOLONGKEYX")]
    [InlineData("A B")]
    public void Invalid_project_keys_are_rejected(string key)
        => new Action(() => Project.ValidateKey(key)).Should().Throw<DomainException>();

    [Fact]
    public void New_issue_starts_in_backlog_with_medium_priority()
    {
        var issue = NewProject().CreateIssue("Login page", IssueType.Story, "reporter");

        issue.Status.Should().Be(IssueStatus.Backlog);
        issue.Priority.Should().Be(IssuePriority.Medium);
        issue.BoardRank.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Mutations_record_field_level_changes_for_the_activity_log()
    {
        var issue = NewProject().CreateIssue("Title", IssueType.Task, "reporter");
        issue.DequeueChanges();

        issue.Assign("dev-1", "reporter");
        issue.ChangeStatus(IssueStatus.InProgress, "dev-1");

        var changes = issue.DequeueChanges();
        changes.Should().HaveCount(2);
        changes.Should().ContainSingle(c => c.Field == "AssigneeUserId" && c.NewValue == "dev-1");
        changes.Should().ContainSingle(c => c.Field == "Status" && c.OldValue == "Backlog" && c.NewValue == "InProgress");
    }

    [Fact]
    public void Setting_a_field_to_its_current_value_records_nothing()
    {
        var issue = NewProject().CreateIssue("Title", IssueType.Task, "reporter");
        issue.DequeueChanges();

        issue.ChangePriority(IssuePriority.Medium, "reporter");

        issue.DequeueChanges().Should().BeEmpty();
    }

    [Fact]
    public void DequeueChanges_clears_the_buffer()
    {
        var issue = NewProject().CreateIssue("Title", IssueType.Task, "reporter");

        issue.DequeueChanges();
        issue.DequeueChanges().Should().BeEmpty();
    }

    [Fact]
    public void Estimate_rejects_out_of_range_points()
    {
        var issue = NewProject().CreateIssue("Title", IssueType.Story, "reporter");

        new Action(() => issue.Estimate(500, "reporter")).Should().Throw<DomainException>();
    }

    [Fact]
    public void MoveOnBoard_updates_rank_and_status_together()
    {
        var issue = NewProject().CreateIssue("Title", IssueType.Task, "reporter");
        issue.DequeueChanges();

        issue.MoveOnBoard(IssueStatus.InReview, "abc", "dev-1");

        issue.BoardRank.Should().Be("abc");
        issue.Status.Should().Be(IssueStatus.InReview);
        issue.DequeueChanges().Should().ContainSingle(c => c.Field == "Status");
    }

    [Fact]
    public void Comment_extracts_distinct_lowercase_mentions()
    {
        var issue = NewProject().CreateIssue("Title", IssueType.Task, "reporter");

        var comment = issue.AddComment("dev-1", "cc @Alice and @bob, also @alice again. email a@b.com not a mention");

        comment.ExtractMentions().Should().BeEquivalentTo(["alice", "bob"]);
    }
}
