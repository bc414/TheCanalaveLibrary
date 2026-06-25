namespace TheCanalaveLibrary.Core;

/// <summary>
/// Aggregate read DTO for the settings page (<c>/settings</c>). Loaded once by
/// <see cref="IUserSettingsService.GetMySettingsAsync"/> and used to seed all five sub-forms.
/// <c>ThemeId</c>, <c>PrefersAnimatedSprites</c>, and <c>PrefersDataSaverMode</c> are lifted
/// to the top level because they are updated by the Appearance sub-form handler
/// (<see cref="IUserSettingsService.UpdateAppearanceAsync"/>), separate from all JSON groups.
/// <c>ShowMatureContent</c> and <c>AllowDiscoveryFromHiddenFavorites</c> live inside
/// <see cref="PrivacySettingsDto"/> — they are hot scalar columns updated together with the
/// Privacy JSON group by <see cref="IUserSettingsService.UpdatePrivacySettingsAsync"/>.
/// Profile text is NOT included — it belongs to the cold <c>UserProfile.Text</c> partition
/// and is loaded separately via <see cref="IUserProfileReadService.GetProfileTextAsync"/>.
/// </summary>
public record UserSettingsDto(
    // Profile section
    string? Tagline,
    string? ProfilePictureRelativeUrl,
    // Appearance section (Feature 3)
    int ThemeId,
    bool PrefersAnimatedSprites,
    bool PrefersDataSaverMode,
    // JSON group sub-records (Privacy also carries ShowMatureContent + AllowDiscoveryFromHiddenFavorites)
    ReaderSettingsDto Reader,
    PrivacySettingsDto Privacy,
    AuthorSettingsDto Author);
