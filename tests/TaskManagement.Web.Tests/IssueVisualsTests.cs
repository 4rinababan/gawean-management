using TaskManagement.Domain;
using TaskManagement.Web.Components.Ui;

namespace TaskManagement.Web.Tests;

public class IssueVisualsTests
{
    [Theory]
    [InlineData(IssueStatus.InProgress, "In Progress")]
    [InlineData(IssueStatus.InReview, "In Review")]
    [InlineData(IssueStatus.Done, "Done")]
    public void StatusLabel_humanises_the_enum(IssueStatus status, string expected)
        => IssueVisuals.StatusLabel(status).Should().Be(expected);

    [Fact]
    public void PriorityColor_flags_high_priorities_as_rose()
    {
        IssueVisuals.PriorityColor(IssuePriority.Highest).Should().Be(Badge.BadgeColor.Rose);
        IssueVisuals.PriorityColor(IssuePriority.Low).Should().Be(Badge.BadgeColor.Slate);
    }

    [Fact]
    public void Every_issue_type_has_a_distinct_symbol()
    {
        var symbols = Enum.GetValues<IssueType>().Select(IssueVisuals.TypeSymbol).ToList();
        symbols.Should().OnlyHaveUniqueItems();
    }
}
