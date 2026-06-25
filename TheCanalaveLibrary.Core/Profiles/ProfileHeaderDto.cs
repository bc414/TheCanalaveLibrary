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
    bool ShowUserStats);
