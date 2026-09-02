using Microsoft.AspNetCore.Components;
using TaskManagement.Application.Abstractions;

namespace TaskManagement.Web.Infrastructure;

public sealed class AppUrls(NavigationManager nav) : IAppUrls
{
    private string Base => nav.BaseUri.TrimEnd('/');

    public string InvitationAccept(string token) => $"{Base}/invitations/accept?token={Uri.EscapeDataString(token)}";

    public string Issue(string orgSlug, Guid issueId) => $"{Base}/{orgSlug}/issues/{issueId}";
}
