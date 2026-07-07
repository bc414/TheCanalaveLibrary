namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// Scoped event funnel between producers (any component/page) and the single ToastHost.
/// Holds no queue of its own — a Show with no subscriber (host not rendered yet, e.g. static
/// SSR Identity pages) is deliberately dropped: toasts are transient by contract.
/// </summary>
public sealed class ToastService : IToastService
{
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(5);

    public event Action<ToastMessage>? OnShow;

    public void Show(string text, ToastLevel level = ToastLevel.Info, TimeSpan? duration = null)
        => OnShow?.Invoke(new ToastMessage(Guid.NewGuid(), level, text, duration ?? DefaultDuration));
}
