namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by <c>ServerFollowingWriteService</c> when a follow/vouch operation is rejected by a
/// business rule that is <em>not</em> an authentication failure — self-follow, self-vouch, or
/// toggling the alert bell on a user you don't follow. Inherits <see cref="CanalaveValidationException"/>
/// so the shared <c>EndpointHelpers.ExecuteWriteAsync</c> maps it to 400 (the accurate status for a
/// rejected-but-well-formed request), not the auth-safety-net 401 these guards previously fell into.
/// Mirrors <see cref="GroupValidationException"/> / <see cref="RecommendationValidationException"/>.
/// (The <see cref="FollowingConstants.MaxVouchesPerUser"/> limit keeps its own dedicated
/// <see cref="VouchLimitException"/> — also a 400 — because its message is baked in.)
/// </summary>
public class FollowingValidationException(IReadOnlyList<string> errors)
    : CanalaveValidationException(string.Join(" ", errors), errors);
