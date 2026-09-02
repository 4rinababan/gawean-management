namespace TaskManagement.Web.Components.Ui;

public enum ToastLevel { Info, Success, Warning, Error }

public sealed record Toast(Guid Id, ToastLevel Level, string Message);

/// <summary>Circuit-scoped toast queue. Components raise toasts; <c>Toaster</c> renders and auto-dismisses them.</summary>
public sealed class ToastService
{
    private readonly List<Toast> _toasts = [];

    public IReadOnlyList<Toast> Current => _toasts;

    public event Action? Changed;

    public void Show(string message, ToastLevel level = ToastLevel.Info)
    {
        var toast = new Toast(Guid.NewGuid(), level, message);
        _toasts.Add(toast);
        Changed?.Invoke();
        _ = DismissLaterAsync(toast.Id);
    }

    public void Success(string message) => Show(message, ToastLevel.Success);
    public void Error(string message) => Show(message, ToastLevel.Error);
    public void Warning(string message) => Show(message, ToastLevel.Warning);

    public void Dismiss(Guid id)
    {
        if (_toasts.RemoveAll(t => t.Id == id) > 0)
            Changed?.Invoke();
    }

    private async Task DismissLaterAsync(Guid id)
    {
        await Task.Delay(TimeSpan.FromSeconds(5));
        Dismiss(id);
    }
}
