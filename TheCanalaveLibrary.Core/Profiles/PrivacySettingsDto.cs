namespace TheCanalaveLibrary.Core;

/// <summary>
/// DTO for the Privacy &amp; Content Preferences settings sub-form (read + write). Covers:
/// <list type="bullet">
///   <item>The <see cref="PrivacySettings"/> JSON complex property (all 6 fields).</item>
///   <item>
///     <see cref="ShowMatureContent"/> and <see cref="AllowDiscoveryFromHiddenFavorites"/> —
///     hot scalar columns on <see cref="User"/> that sit outside the JSON blob but belong to
///     the same sub-form conceptually.
///   </item>
/// </list>
/// Used as a sub-record on <see cref="UserSettingsDto"/> (read) and as the payload for
/// <see cref="IUserSettingsService.UpdatePrivacySettingsAsync"/> (write).
/// </summary>
public record PrivacySettingsDto(
    ProfileVisibility ProfileVisibility,
    bool ShowActivityStatus,
    SocialInteractionPermission AllowProfileComments,
    SocialInteractionPermission AllowPrivateMessages,
    bool ShowUserStats,
    bool ShowCurrentlyReading,
    // Hot scalar columns (outside the JSON blob, patched via ExecuteUpdateAsync).
    bool ShowMatureContent,
    bool AllowDiscoveryFromHiddenFavorites);
