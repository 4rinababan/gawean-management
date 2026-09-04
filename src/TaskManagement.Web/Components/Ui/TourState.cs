using Microsoft.JSInterop;

namespace TaskManagement.Web.Components.Ui;

public enum TourStep { CreateWorkspace, CreateProject, AddIssue, InviteMember, UseAi }

/// <summary>Circuit-scoped onboarding-tour progress, persisted client-side in a cookie (a per-browser
/// UI preference, not account data). <c>ProductTour</c> renders it as a step-by-step spotlight overlay;
/// pages call <see cref="MarkDone"/> right after the real action succeeds — this is never a
/// simulated/scripted walkthrough, only the guided navigation between steps is scripted.</summary>
public sealed class TourState(IJSRuntime js)
{
    private const int SchemaVersion = 1;
    private HashSet<TourStep> _done = [];
    private bool _dismissed;

    public event Action? Changed;

    public bool Loaded { get; private set; }
    public bool Active { get; private set; }
    public int CurrentIndex { get; private set; }
    public bool Dismissed => _dismissed;
    public bool IsDone(TourStep step) => _done.Contains(step);

    /// <summary>Reads the cookie once per circuit. JS interop is unavailable during prerender, so this
    /// must be called from a component's <c>OnAfterRenderAsync(firstRender)</c>, not <c>OnInitializedAsync</c>.
    /// A brand new visitor (no cookie at all) auto-starts the tour; a returning visitor never auto-starts
    /// again, whether they finished, skipped, or just closed it.</summary>
    public async Task LoadAsync()
    {
        if (Loaded) return;
        Loaded = true;

        var cookie = await js.InvokeAsync<TourCookie?>("tm.tour.read");
        if (cookie is null)
        {
            Active = true;
        }
        else if (cookie.V == SchemaVersion)
        {
            _done = cookie.Done?.Select(i => (TourStep)i).ToHashSet() ?? [];
            _dismissed = cookie.Dismissed;
        }
        Changed?.Invoke();
    }

    public void MarkDone(TourStep step)
    {
        if (_done.Add(step))
        {
            Save();
            Changed?.Invoke();
        }
    }

    /// <summary>Reopens the tour from the first step, regardless of prior progress — used by "Take a tour".</summary>
    public void Restart()
    {
        _done = [];
        _dismissed = false;
        Active = true;
        CurrentIndex = 0;
        Save();
        Changed?.Invoke();
    }

    public void Next(int totalSteps)
    {
        if (CurrentIndex + 1 >= totalSteps)
        {
            Close();
            return;
        }
        CurrentIndex++;
        Changed?.Invoke();
    }

    public void Prev()
    {
        if (CurrentIndex > 0)
        {
            CurrentIndex--;
            Changed?.Invoke();
        }
    }

    public void Close()
    {
        Active = false;
        _dismissed = true;
        Save();
        Changed?.Invoke();
    }

    private void Save() => _ = js.InvokeVoidAsync("tm.tour.write",
        new TourCookie(SchemaVersion, _done.Select(s => (int)s).ToArray(), _dismissed)).AsTask();

    private sealed record TourCookie(int V, int[]? Done, bool Dismissed);
}
