namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by <see cref="IFollowingWriteService.VouchAsync"/> when the acting user already holds
/// <see cref="FollowingConstants.MaxVouchesPerUser"/> outgoing vouches and attempts to add a sixth.
/// The UI's primary prevention mechanism is disabling <c>VouchButton</c> when
/// <c>UserRelationshipStateDto.OutgoingVouchCount &gt;= MaxVouchesPerUser</c>; this exception is the
/// server-side backstop.
/// </summary>
public sealed class VouchLimitException() : Exception(
    $"You have reached the maximum of {FollowingConstants.MaxVouchesPerUser} vouches. " +
    "Remove an existing vouch before adding a new one.");
