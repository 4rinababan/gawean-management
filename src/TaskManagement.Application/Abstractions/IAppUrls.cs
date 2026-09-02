namespace TaskManagement.Application.Abstractions;

/// <summary>Builds absolute URLs for use in emails and notifications. Implemented in the web layer where the request host is known.</summary>
public interface IAppUrls
{
    string InvitationAccept(string token);

    string Issue(string orgSlug, Guid issueId);
}
