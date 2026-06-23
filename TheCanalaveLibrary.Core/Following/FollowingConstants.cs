namespace TheCanalaveLibrary.Core;

/// <summary>
/// Shared constants for the Following/Vouches feature cluster. Values are used by both the service
/// layer (enforcement) and UI components (display — e.g. disabling VouchButton at limit).
/// </summary>
public static class FollowingConstants
{
    /// <summary>
    /// Maximum number of outgoing vouches a single user may hold at any time (§5.8, anti-snowball
    /// scarcity lever — settled WU21 as kept). Enforced in C# by
    /// <c>IFollowingWriteService.VouchAsync</c>; the database imposes no separate constraint.
    /// </summary>
    public const int MaxVouchesPerUser = 5;
}
