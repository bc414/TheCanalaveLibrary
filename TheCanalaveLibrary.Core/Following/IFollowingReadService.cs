namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the Following/Vouches feature cluster. Implemented server-side by
/// <c>ServerFollowingReadService</c> (uses <c>ReadOnlyApplicationDbContext</c>).
/// MVP is InteractiveServer-only; a client HTTP impl lives in the Post-MVP L5 batch.
/// </summary>
public interface IFollowingReadService
{
    /// <summary>
    /// The current viewer's relationship state toward <paramref name="targetUserId"/>. Returns a
    /// zero-state (not following, not vouched, 0 outgoing vouches) for anonymous callers or when no
    /// row exists. Used by the profile dispatcher to initialise FollowButton and VouchButton.
    /// </summary>
    Task<UserRelationshipStateDto> GetRelationshipStateAsync(int targetUserId);

    /// <summary>
    /// Returns the users that <paramref name="userId"/> follows, projected to <see cref="UserCardDto"/>.
    /// Public — any caller may load another user's followed list.
    /// </summary>
    Task<IReadOnlyList<UserCardDto>> GetFollowedUsersAsync(int userId);

    /// <summary>
    /// Returns the vouches that <paramref name="userId"/> has given out, with the recipient's
    /// <see cref="UserCardDto"/> and the optional rich-text note. Public — outgoing vouches are
    /// visible to all (§5.8 display asymmetry).
    /// </summary>
    Task<IReadOnlyList<VouchDisplayDto>> GetOutgoingVouchesAsync(int userId);

    /// <summary>
    /// Returns the vouches that other users have given to the <em>currently authenticated user</em>.
    /// Private — incoming vouches are visible only to their recipient (§5.8 display asymmetry).
    /// The active user's id comes from <c>IActiveUserContext</c>, not a parameter, so this method
    /// cannot be called for an arbitrary user's incoming list — by design.
    /// </summary>
    Task<IReadOnlyList<VouchDisplayDto>> GetIncomingVouchesAsync();
}
