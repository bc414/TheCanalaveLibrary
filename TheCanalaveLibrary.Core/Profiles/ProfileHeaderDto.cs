namespace TheCanalaveLibrary.Core;

/// <summary>
/// Everything the persistent <c>ProfileBanner</c> composite needs, loaded once by the dispatcher
/// on init (tab-independent). Loaded via
/// <see cref="IUserProfileReadService.GetProfileHeaderAsync"/>.
///
/// <see cref="Stats"/> is <c>null</c> when the profile owner's
/// <c>PrivacySettings.ShowUserStats</c> is <c>false</c> AND the viewer is not the owner.
/// The banner hides the <c>UserStatsBlock</c> in that case.
///
/// <see cref="RelationshipState"/> is <c>null</c> for the owner viewing their own profile
/// (no Follow/Vouch controls rendered) and for anonymous visitors. The dispatcher sets it by
/// calling <see cref="IFollowingReadService.GetRelationshipStateAsync"/> when authenticated
/// and not the owner.
///
/// <see cref="LastSeenUtc"/> (WU-SiteDailyStat, Feature 62) is <c>null</c> when the owner's
/// <c>PrivacySettings.ShowActivityStatus</c> is <c>false</c> AND the viewer is not the owner (same
/// gating shape as <see cref="Stats"/>), or when the user has no <c>User.LastActiveUtc</c> stamp
/// yet (go-forward signal — see layer8-data-marts.md §"site_daily_stats"). The banner hides the
/// "last seen" line in either case.
/// </summary>
public record ProfileHeaderDto(
    int UserId,
    string Username,
    string? AvatarUrl,
    string? Tagline,
    IReadOnlyList<UserCardBadgeDto> Badges,
    IReadOnlyList<VouchDisplayDto> OutgoingVouches,
    UserStatsDto? Stats,
    UserRelationshipStateDto? RelationshipState,
    ProfileVisibility ProfileVisibility,
    SocialInteractionPermission AllowProfileComments,
    bool ShowUserStats,
    DateTime? LastSeenUtc);
