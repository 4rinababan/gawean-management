using Microsoft.AspNetCore.Components;
using TaskManagement.Application.Abstractions;
using TaskManagement.Web.Infrastructure;

namespace TaskManagement.Web.Components.Pages.App;

/// <summary>
/// Base for interactive pages under <c>/{slug}/…</c>. The static <c>OrgLayout</c> resolves the tenant during the
/// prerender pass, but an interactive page runs in a separate circuit scope with its own <see cref="ITenantContext"/>,
/// so each page must resolve it again before loading data.
/// </summary>
public abstract class OrgPageBase : ComponentBase
{
    [Parameter] public string Slug { get; set; } = "";

    [Inject] protected TenantResolver TenantResolver { get; set; } = default!;
    [Inject] protected ITenantContext Tenant { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;

    /// <summary>True once the current user's membership of <see cref="Slug"/> has been confirmed for this scope.</summary>
    protected bool TenantReady { get; private set; }

    protected sealed override async Task OnParametersSetAsync()
    {
        var outcome = await TenantResolver.ResolveAsync(Slug);
        TenantReady = outcome == TenantResolver.Outcome.Resolved;

        if (!TenantReady)
        {
            Nav.NavigateTo("/dashboard", replace: true);
            return;
        }

        await OnTenantReadyAsync();
    }

    /// <summary>Load page data here; <see cref="Tenant"/> is resolved and permission checks will work.</summary>
    protected virtual Task OnTenantReadyAsync() => Task.CompletedTask;
}
