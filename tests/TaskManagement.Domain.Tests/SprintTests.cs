using TaskManagement.Domain;
using TaskManagement.Domain.Common;
using TaskManagement.Domain.Sprints;

namespace TaskManagement.Domain.Tests;

public class SprintTests
{
    private static Sprint NewSprint() => new(Guid.CreateVersion7(), Guid.CreateVersion7(), "Sprint 1");

    [Fact]
    public void Start_moves_a_planned_sprint_to_active()
    {
        var sprint = NewSprint();
        var start = new DateOnly(2026, 1, 6);

        sprint.Start(start, start.AddDays(14));

        sprint.State.Should().Be(SprintState.Active);
        sprint.StartDate.Should().Be(start);
    }

    [Fact]
    public void Start_rejects_an_end_date_that_is_not_after_the_start()
    {
        var sprint = NewSprint();
        var day = new DateOnly(2026, 1, 6);

        new Action(() => sprint.Start(day, day)).Should().Throw<DomainException>();
    }

    [Fact]
    public void Cannot_start_an_already_active_sprint()
    {
        var sprint = NewSprint();
        var start = new DateOnly(2026, 1, 6);
        sprint.Start(start, start.AddDays(7));

        new Action(() => sprint.Start(start, start.AddDays(7))).Should().Throw<DomainException>();
    }

    [Fact]
    public void Complete_requires_an_active_sprint()
    {
        var sprint = NewSprint();

        new Action(sprint.Complete).Should().Throw<DomainException>();
    }

    [Fact]
    public void Completed_sprint_is_immutable()
    {
        var sprint = NewSprint();
        var start = new DateOnly(2026, 1, 6);
        sprint.Start(start, start.AddDays(7));
        sprint.Complete();

        new Action(() => sprint.Update("x", null)).Should().Throw<DomainException>();
    }
}
