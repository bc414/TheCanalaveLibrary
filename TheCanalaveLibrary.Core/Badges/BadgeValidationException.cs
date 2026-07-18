namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by <c>ServerBadgeWriteService.SetDisplayOrderAsync</c> when a requested display key has
/// not been earned by the user — a business-rule rejection, not an authentication failure. Inherits
/// <see cref="CanalaveValidationException"/> so the shared <c>EndpointHelpers.ExecuteWriteAsync</c>
/// maps it to 400 (the accurate status), not the auth-safety-net 401 this guard previously fell into.
/// Mirrors <see cref="GroupValidationException"/> / <see cref="RecommendationValidationException"/>.
/// </summary>
public class BadgeValidationException(IReadOnlyList<string> errors)
    : CanalaveValidationException(string.Join(" ", errors), errors);
