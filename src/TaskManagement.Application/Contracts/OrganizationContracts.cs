using TaskManagement.Domain;

namespace TaskManagement.Application.Contracts;

/// <summary>Form-bound classes are mutable with parameterless constructors so Blazor <c>EditForm</c> can two-way bind them.</summary>
public sealed class CreateOrganizationRequest
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public sealed class InviteMemberRequest
{
    public string Email { get; set; } = string.Empty;
    public OrgRole Role { get; set; } = OrgRole.Member;
}

public sealed record OrganizationDto(Guid Id, string Name, string Slug, OrgRole CurrentUserRole, int MemberCount);

public sealed record OrganizationMemberDto(string UserId, string DisplayName, string Email, string AvatarColor, OrgRole Role, string MentionHandle);

public sealed record InvitationDto(Guid Id, string Email, OrgRole Role, DateTimeOffset ExpiresAt, string InvitedByDisplayName, string AcceptUrl);

public sealed record AcceptInvitationResult(string OrganizationSlug, string OrganizationName);

public sealed record OrgAuditLogEntryDto(Guid Id, string EventType, string Detail, string ActorDisplayName, string? TargetDisplayName, DateTimeOffset CreatedAt);

/// <summary>The application-level content behind a "download your data" export — everything beyond the Identity account fields already exported.</summary>
public sealed record PersonalDataSummaryDto(IReadOnlyList<string> Workspaces, IReadOnlyList<string> ReportedIssues, int CommentCount);
