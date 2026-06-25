using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side implementation of <see cref="IUserSettingsService"/> (Features 20 + 3, WU30).
/// This is the sanctioned self-referential editing service (spec §3.5): all methods resolve the
/// target user from <see cref="IActiveUserContext"/>; none take a userId parameter; all throw
/// <see cref="InvalidOperationException"/> when the caller is not authenticated.
/// See <c>layer2-services.md</c> §"Self-Referential Editing Exception" for the full rationale.
///
/// Injection: <see cref="ApplicationDbContext"/> (write),
///            <see cref="ReadOnlyApplicationDbContext"/> (read, for <c>GetMySettingsAsync</c>),
///            <see cref="IActiveUserContext"/>,
///            <see cref="IImageStorageService"/>,
///            <see cref="IHtmlSanitizationService"/>.
/// </summary>
public class ServerUserSettingsService(
    ApplicationDbContext writeDb,
    ReadOnlyApplicationDbContext readDb,
    IActiveUserContext activeUser,
    IImageStorageService imageStorage,
    IHtmlSanitizationService sanitizer) : IUserSettingsService
{
    // Resolves and validates the current user id — throws when anonymous.
    private int RequireCurrentUserId()
    {
        if (activeUser.UserId is not int id)
            throw new InvalidOperationException("This operation requires an authenticated user.");
        return id;
    }

    // ── Read ────────────────────────────────────────────────────────────────────

    public async Task<UserSettingsDto> GetMySettingsAsync()
    {
        int userId = RequireCurrentUserId();

        var row = await readDb.Users
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Tagline,
                u.ProfilePictureRelativeUrl,
                u.ThemeId,
                u.PrefersAnimatedSprites,
                u.PrefersDataSaverMode,
                u.ShowMatureContent,
                u.AllowDiscoveryFromHiddenFavorites,
                u.ReaderSettings,
                u.PrivacySettings,
                u.AuthorSettings
            })
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"User {userId} not found.");

        ReaderSettingsDto reader = new(
            row.ReaderSettings.FontName,
            row.ReaderSettings.FontSize,
            row.ReaderSettings.LineHeight,
            row.ReaderSettings.TextWidth,
            row.ReaderSettings.JustifyText,
            row.ReaderSettings.AutoLoadNextChapter,
            row.ReaderSettings.CollapseCommentThreads,
            row.ReaderSettings.DefaultPaginationSize,
            row.ReaderSettings.DefaultSearchSort);

        PrivacySettingsDto privacy = new(
            row.PrivacySettings.ProfileVisibility,
            row.PrivacySettings.ShowActivityStatus,
            row.PrivacySettings.AllowProfileComments,
            row.PrivacySettings.AllowPrivateMessages,
            row.PrivacySettings.ShowUserStats,
            row.PrivacySettings.ShowCurrentlyReading,
            row.ShowMatureContent,
            row.AllowDiscoveryFromHiddenFavorites);

        AuthorSettingsDto author = new(row.AuthorSettings.DefaultStoryRating);

        return new UserSettingsDto(
            row.Tagline,
            row.ProfilePictureRelativeUrl,
            row.ThemeId,
            row.PrefersAnimatedSprites,
            row.PrefersDataSaverMode,
            reader,
            privacy,
            author);
    }

    // ── Write — Profile section ─────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task UpdateProfileAsync(UpdateProfileDto dto)
    {
        int userId = RequireCurrentUserId();

        User user = await writeDb.Users.FindAsync(userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        if (dto.Tagline is not null)
            user.Tagline = dto.Tagline.Trim();

        await writeDb.SaveChangesAsync();

        // Update the cold partition (UserProfile.Text) only when the caller passed a value.
        if (dto.ProfileTextHtml is not null)
        {
            string sanitized = sanitizer.Sanitize(dto.ProfileTextHtml);

            UserProfile? profile = await writeDb.UserProfiles.FindAsync(userId);
            if (profile is null)
            {
                // Ensure the cold row exists (should have been created on registration;
                // defensive insert here guards against legacy rows that pre-date the partition).
                profile = new UserProfile { UserId = userId };
                writeDb.UserProfiles.Add(profile);
            }

            profile.Text = sanitized;
            await writeDb.SaveChangesAsync();
        }
    }

    // ── Write — JSON group sections ─────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task UpdateReaderSettingsAsync(ReaderSettingsDto dto)
    {
        int userId = RequireCurrentUserId();

        User user = await writeDb.Users.FindAsync(userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        user.ReaderSettings = new ReaderSettings
        {
            FontName                 = dto.FontName,
            FontSize                 = dto.FontSize,
            LineHeight               = dto.LineHeight,
            TextWidth                = dto.TextWidth,
            JustifyText              = dto.JustifyText,
            AutoLoadNextChapter      = dto.AutoLoadNextChapter,
            CollapseCommentThreads   = dto.CollapseCommentThreads,
            DefaultPaginationSize    = dto.DefaultPaginationSize,
            DefaultSearchSort        = dto.DefaultSearchSort
        };

        await writeDb.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task UpdatePrivacySettingsAsync(PrivacySettingsDto dto)
    {
        int userId = RequireCurrentUserId();

        User user = await writeDb.Users.FindAsync(userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        user.PrivacySettings = new PrivacySettings
        {
            ProfileVisibility    = dto.ProfileVisibility,
            ShowActivityStatus   = dto.ShowActivityStatus,
            AllowProfileComments = dto.AllowProfileComments,
            AllowPrivateMessages = dto.AllowPrivateMessages,
            ShowUserStats        = dto.ShowUserStats,
            ShowCurrentlyReading = dto.ShowCurrentlyReading
        };

        // Patch the hot scalar columns that live outside the JSON blob.
        // These are updated atomically in the same SaveChangesAsync call as the JSON group.
        user.ShowMatureContent                  = dto.ShowMatureContent;
        user.AllowDiscoveryFromHiddenFavorites  = dto.AllowDiscoveryFromHiddenFavorites;

        await writeDb.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task UpdateAuthorSettingsAsync(AuthorSettingsDto dto)
    {
        int userId = RequireCurrentUserId();

        User user = await writeDb.Users.FindAsync(userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        user.AuthorSettings = new AuthorSettings
        {
            DefaultStoryRating = dto.DefaultStoryRating
        };

        await writeDb.SaveChangesAsync();
    }

    // ── Write — Appearance section (Feature 3) ───────────────────────────────────

    /// <inheritdoc/>
    public async Task UpdateAppearanceAsync(int themeId, bool prefersAnimated, bool prefersDataSaver)
    {
        int userId = RequireCurrentUserId();

        // All three are hot scalar columns — ExecuteUpdateAsync avoids a round-trip load.
        int rows = await writeDb.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.ThemeId,               themeId)
                .SetProperty(u => u.PrefersAnimatedSprites, prefersAnimated)
                .SetProperty(u => u.PrefersDataSaverMode,  prefersDataSaver));

        if (rows == 0)
            throw new InvalidOperationException($"User {userId} not found.");
    }

    // ── Write — Profile picture ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<string> UploadProfilePictureAsync(Stream content, string contentType)
    {
        int userId = RequireCurrentUserId();

        // Delegate storage and key construction to IImageStorageService.
        // Stored key: users/{userId}/profile-{uuid}.{ext} (per layer2-services.md §"Image Storage").
        string relativeUrl = await imageStorage.SaveAsync(
            content, contentType, ImageKind.ProfilePicture, ownerId: userId);

        // Patch the hot column directly — avoid a round-trip load.
        await writeDb.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.ProfilePictureRelativeUrl, relativeUrl));

        return relativeUrl;
    }
}
