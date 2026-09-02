namespace TaskManagement.Application.Contracts;

public sealed class CreateProjectRequest
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LeadUserId { get; set; }
}

public sealed class UpdateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LeadUserId { get; set; }
}

public sealed record ProjectDto(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    string? LeadUserId,
    string? LeadDisplayName,
    int OpenIssueCount);
