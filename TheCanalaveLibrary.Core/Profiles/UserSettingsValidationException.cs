namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by <c>ServerUserSettingsService</c> when a settings write is rejected by a business rule
/// that is <em>not</em> an authentication failure — currently the pinned-story ownership/visibility
/// guard in <c>UpdateAuthorSettingsAsync</c> (the pinned story must be the caller's own, published,
/// visible story). Inherits <see cref="CanalaveValidationException"/> so the shared
/// <c>EndpointHelpers.ExecuteWriteAsync</c> maps it to 400 (the accurate status), not the
/// auth-safety-net 401 this guard previously fell into. Mirrors <see cref="GroupValidationException"/>.
/// </summary>
public class UserSettingsValidationException(IReadOnlyList<string> errors)
    : CanalaveValidationException(string.Join(" ", errors), errors);
