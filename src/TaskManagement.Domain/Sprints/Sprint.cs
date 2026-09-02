using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Sprints;

/// <summary>A time-boxed iteration inside a project. State transitions are one-way: Planned → Active → Completed.</summary>
public class Sprint : Entity, ITenantScoped
{
    private Sprint() { }

    public Sprint(Guid organizationId, Guid projectId, string name, string? goal = null)
    {
        OrganizationId = organizationId;
        ProjectId = projectId;
        Name = Guard.NotBlank(name, nameof(name));
        Goal = goal?.Trim();
        State = SprintState.Planned;
    }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Goal { get; private set; }

    public DateOnly? StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public SprintState State { get; private set; }

    public void Update(string name, string? goal)
    {
        EnsureNotCompleted();
        Name = Guard.NotBlank(name, nameof(name));
        Goal = goal?.Trim();
    }

    public void Start(DateOnly startDate, DateOnly endDate)
    {
        if (State != SprintState.Planned)
            throw new DomainException("Only a planned sprint can be started.");
        if (endDate <= startDate)
            throw new DomainException("Sprint end date must be after its start date.");

        StartDate = startDate;
        EndDate = endDate;
        State = SprintState.Active;
    }

    public void Complete()
    {
        if (State != SprintState.Active)
            throw new DomainException("Only an active sprint can be completed.");

        State = SprintState.Completed;
    }

    private void EnsureNotCompleted()
    {
        if (State == SprintState.Completed)
            throw new DomainException("A completed sprint cannot be modified.");
    }
}
