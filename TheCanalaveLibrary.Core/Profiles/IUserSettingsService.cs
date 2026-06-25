namespace TheCanalaveLibrary.Core;

/// <summary>
/// Self-referential read+write service for a user editing their own settings (spec §3.5).
/// This is the sanctioned exception to CQRS-lite — applicable ONLY when reader and writer are
/// identical by definition. See <c>layer2-services.md</c> §"Self-Referential Editing Exception."
///
/// All methods resolve the target user from <see cref="IActiveUserContext"/> and throw
/// <see cref="InvalidOperationException"/> when the caller is not authenticated.
/// No method takes a <c>userId</c> parameter.
///
/// Server impl injects: <c>ApplicationDbContext</c>, <c>IActiveUserContext</c>,
/// <c>IImageStorageService</c>, <c>IHtmlSanitizationService</c>.
/// </summary>
public interface IUserSettingsService
{
    /// <summary>
    /// Returns the current user's full settings, projected to <see cref="UserSettingsDto"/>.
    /// Profile bio text is NOT included — load it separately via
    /// <see cref="IUserProfileReadService.GetProfileTextAsync"/> (cold partition).
    /// </summary>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task<UserSettingsDto> GetMySettingsAsync();

    /// <summary>
    /// Updates the Profile sub-form: tagline (hot column on <c>User</c>) and bio
    /// (<c>UserProfile.Text</c>, cold partition). Bio is sanitized before persisting.
    /// A <c>null</c> field in <paramref name="dto"/> means "leave unchanged."
    /// </summary>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task UpdateProfileAsync(UpdateProfileDto dto);

    /// <summary>
    /// Replaces the current user's <see cref="ReaderSettings"/> JSON group in full.
    /// </summary>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task UpdateReaderSettingsAsync(ReaderSettingsDto dto);

    /// <summary>
    /// Replaces the current user's <see cref="PrivacySettings"/> JSON group in full.
    /// Also patches the <c>ShowMatureContent</c> hot column via a separate scalar update if
    /// the effective mature-content preference changed (the hot column is outside the JSON blob).
    /// </summary>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task UpdatePrivacySettingsAsync(PrivacySettingsDto dto);

    /// <summary>
    /// Replaces the current user's <see cref="AuthorSettings"/> JSON group in full.
    /// </summary>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task UpdateAuthorSettingsAsync(AuthorSettingsDto dto);

    /// <summary>
    /// Updates the Appearance sub-form: theme FK (<c>User.ThemeId</c>), animated-sprite pref
    /// (<c>User.PrefersAnimatedSprites</c>), and data-saver pref (<c>User.PrefersDataSaverMode</c>).
    /// These are all hot scalar columns, not part of a JSON group.
    /// This is the Feature-3 theme-selection write path (L3-Logic gate for F3 L3/L3.5 cells).
    /// </summary>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task UpdateAppearanceAsync(int themeId, bool prefersAnimated, bool prefersDataSaver);

    /// <summary>
    /// Uploads a new profile picture, stores it via <see cref="IImageStorageService"/> under
    /// the key <c>users/{UserId}/profile-{uuid}.{ext}</c>, and patches
    /// <c>User.ProfilePictureRelativeUrl</c> with the resulting relative path.
    /// </summary>
    /// <param name="content">The raw file stream from <c>&lt;InputFile&gt;</c>.</param>
    /// <param name="contentType">MIME type (e.g. <c>"image/jpeg"</c>).</param>
    /// <returns>The new relative URL stored on the user.</returns>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task<string> UploadProfilePictureAsync(Stream content, string contentType);
}
