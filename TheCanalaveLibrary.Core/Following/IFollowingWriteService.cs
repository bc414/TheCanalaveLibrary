namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the Following/Vouches feature cluster. Inherits the read side so the profile
/// dispatcher can inject the narrowest needed interface while still having a single DI registration
/// pair. Implemented by <c>ServerFollowingWriteService</c>.
/// </summary>
public interface IFollowingWriteService : IFollowingReadService
{
    /// <summary>
    /// Records the current user as following <paramref name="targetUserId"/>. Idempotent — no-op if
    /// the row already exists. Guards against self-follow (throws <see cref="FollowingValidationException"/>).
    /// <c>ReceiveAlerts</c> defaults to <c>true</c> on the new row.
    /// <!-- New-follower notification ships via NotifyNewFollowerAsync (WU22, done). -->
    /// </summary>
    Task FollowAsync(int targetUserId);

    /// <summary>
    /// Removes the current user's follow of <paramref name="targetUserId"/>. Idempotent — no-op if
    /// no row exists.
    /// </summary>
    Task UnfollowAsync(int targetUserId);

    /// <summary>
    /// Toggles the alert-bell preference on an existing follow row. Throws
    /// <see cref="FollowingValidationException"/> if the current user does not follow
    /// <paramref name="targetUserId"/>.
    /// </summary>
    Task SetReceiveAlertsAsync(int targetUserId, bool receiveAlerts);

    /// <summary>
    /// Creates a vouch from the current user to <paramref name="targetUserId"/> with an optional
    /// rich-text note. Sanitizes <paramref name="vouchText"/> via <c>IHtmlSanitizationService</c>
    /// before persisting (sanitize-once-on-save — <c>layer2-services.md</c>). Enforces the
    /// <see cref="FollowingConstants.MaxVouchesPerUser"/> limit (throws <see cref="VouchLimitException"/>
    /// at the 6th vouch). Guards against self-vouch (throws <see cref="FollowingValidationException"/>)
    /// and duplicate vouches (idempotent no-op).
    /// <!-- New-vouch notification ships via NotifyNewVouchAsync (WU22, done). -->
    /// </summary>
    Task VouchAsync(int targetUserId, string? vouchText);

    /// <summary>
    /// Removes the current user's vouch for <paramref name="targetUserId"/>. Idempotent — no-op if
    /// the vouch does not exist.
    /// </summary>
    Task RemoveVouchAsync(int targetUserId);
}
