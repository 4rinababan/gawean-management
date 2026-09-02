using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using TaskManagement.Application.Abstractions;

namespace TaskManagement.Web.Infrastructure;

/// <summary>
/// Adapts the Blazor <see cref="AuthenticationStateProvider"/> to the application's <see cref="ICurrentUser"/>.
/// The auth state is already materialized by the time a component calls a service, so the synchronous read is safe.
/// </summary>
public sealed class CurrentUser(AuthenticationStateProvider authStateProvider) : ICurrentUser
{
    private ClaimsPrincipal Principal =>
        authStateProvider.GetAuthenticationStateAsync().GetAwaiter().GetResult().User;

    public string? UserId => Principal.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Email => Principal.FindFirstValue(ClaimTypes.Email);
}
