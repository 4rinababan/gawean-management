using System.Text.RegularExpressions;
using TaskManagement.Domain.Common;
using TaskManagement.Domain.Issues;

namespace TaskManagement.Domain.Projects;

/// <summary>A project groups issues and sprints. Its <see cref="Key"/> prefixes every issue reference (e.g. <c>WEB-42</c>).</summary>
public partial class Project : Entity, ITenantScoped
{
    private Project() { }

    public Project(Guid organizationId, string key, string name, string? description = null, string? leadUserId = null)
    {
        OrganizationId = organizationId;
        Key = ValidateKey(key);
        Name = Guard.NotBlank(name, nameof(name));
        Description = description?.Trim();
        LeadUserId = leadUserId;
    }

    public Guid OrganizationId { get; private set; }

    /// <summary>Uppercase 2–10 char alphanumeric prefix, unique within the organization.</summary>
    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? LeadUserId { get; private set; }

    /// <summary>Monotonic counter for issue numbers. The last value handed out; the next issue is <c>IssueSequence + 1</c>.</summary>
    public int IssueSequence { get; private set; }

    public void Update(string name, string? description, string? leadUserId)
    {
        Name = Guard.NotBlank(name, nameof(name));
        Description = description?.Trim();
        LeadUserId = leadUserId;
    }

    /// <summary>Reserves and returns the next issue number for this project. Not thread-safe on its own — callers rely on the DB row lock / unique index.</summary>
    public int NextIssueNumber() => ++IssueSequence;

    public Issue CreateIssue(string title, IssueType type, string reporterUserId)
        => new(this, title, type, reporterUserId);

    public static string ValidateKey(string key)
    {
        key = Guard.NotBlank(key, nameof(key)).ToUpperInvariant();
        return KeyPattern().IsMatch(key)
            ? key
            : throw new DomainException("Project key must be 2–10 uppercase letters or digits and start with a letter.");
    }

    [GeneratedRegex("^[A-Z][A-Z0-9]{1,9}$")]
    private static partial Regex KeyPattern();
}
