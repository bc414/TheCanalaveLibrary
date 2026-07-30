namespace TheCanalaveLibrary.Core;

/// <summary>
/// Live snapshot of the viewer's own <see cref="User.AccountStatus"/> — the read half of
/// WU-AccountEnforcement's mid-session-responsiveness fix. <see cref="SuspendedUntilUtc"/> is
/// carried alongside the status (not just the bare enum) so a Suspended-state consumer can render
/// the same "suspended until {date}" copy <c>Login.razor</c> already shows, rather than a second,
/// diverging sentence.
/// </summary>
public record AccountStatusDto(AccountStatusEnum Status, DateTime? SuspendedUntilUtc);
