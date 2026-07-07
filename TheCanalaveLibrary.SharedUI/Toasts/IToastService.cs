namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// The transient non-blocking feedback channel (cross-cutting.md §"Error Handling Strategy" —
/// feedback channels). Deliberately minimal: for system events with no inline home ("draft
/// restored", "reconnected"). NEVER for validation errors (InlineAlert, next to the field) and
/// never for anything requiring a decision (ConfirmDialog). Scoped per circuit/user; ToastHost
/// (rendered once by DeviceLayout) is the single subscriber.
/// </summary>
public interface IToastService
{
    /// <summary>Raised for each toast. ToastHost subscribes; producers only call Show.</summary>
    event Action<ToastMessage>? OnShow;

    void Show(string text, ToastLevel level = ToastLevel.Info, TimeSpan? duration = null);
}

public enum ToastLevel
{
    Info,
    Success,
    Warning,
    Danger,
}

/// <summary>One toast. Duration governs auto-dismiss in ToastHost.</summary>
public sealed record ToastMessage(Guid Id, ToastLevel Level, string Text, TimeSpan Duration);
