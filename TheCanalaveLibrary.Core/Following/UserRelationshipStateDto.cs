namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read-once state the current viewer holds toward a target user — drives both FollowButton and
/// VouchButton initial state. Loaded by the profile dispatcher (WU30) via
/// <see cref="IFollowingReadService.GetRelationshipStateAsync"/> and passed down as parameters;
/// no child component should re-query this.
///
/// <c>OutgoingVouchCount</c> allows the UI to derive "at limit" without a separate call:
/// <c>AtVouchLimit = OutgoingVouchCount &gt;= FollowingConstants.MaxVouchesPerUser</c>.
/// This is the viewer's *total* outgoing vouch count, not whether they vouched for this specific
/// user — <c>IsVouched</c> answers that.
/// </summary>
public record UserRelationshipStateDto(
    bool IsFollowing,
    bool ReceiveAlerts,
    bool IsVouched,
    int OutgoingVouchCount
);
