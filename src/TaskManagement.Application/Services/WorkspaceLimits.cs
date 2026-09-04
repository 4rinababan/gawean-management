namespace TaskManagement.Application.Services;

/// <summary>
/// Hard caps in place of billing: keeps the service free but bounded rather than letting one account
/// spin up unlimited workspaces, or one workspace collect unlimited members.
/// </summary>
internal static class WorkspaceLimits
{
    public const int MaxWorkspacesPerUser = 5;

    public const int MaxMembersPerWorkspace = 10;
}
