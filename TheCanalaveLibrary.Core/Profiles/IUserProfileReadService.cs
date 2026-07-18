namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the public profile display (Feature 21 — User Profile Display).
/// <em>Not</em> the self-edit service; see <see cref="IUserSettingsService"/> for that.
/// Server impl uses <c>ReadOnlyApplicationDbContext</c> (NoTracking).
///
/// Own-vs-other visibility differences are expressed via the <c>bool includePrivate</c> predicate
/// — <c>true</c> when <c>viewerId == profileUserId</c>. This is a data predicate, not a source
/// switch (same pattern as <c>GetByAuthorAsync(includeUnpublished)</c>). See
/// <c>layer2-services.md</c> §"Self-Referential Editing Exception."
/// </summary>
public interface IUserProfileReadService
{
    /// <summary>
    /// Returns the persistent banner data for a profile, or <c>null</c> when the user doesn't
    /// exist or <see cref="ProfileVisibility.Private"/> hides the profile from non-owners.
    ///
    /// <paramref name="includePrivate"/> controls two visibility gates:
    /// <list type="bullet">
    ///   <item>Stats: always included for the owner; hidden (null) for non-owners when
    ///     <c>ShowUserStats = false</c>.</item>
    ///   <item>Profile visibility: <c>Private</c> profile → returns <c>null</c> for non-owners;
    ///     <c>UsersOnly</c> → returns <c>null</c> for anonymous non-owners.</item>
    /// </list>
    /// The <see cref="ProfileHeaderDto.RelationshipState"/> field is populated only when the
    /// caller is authenticated and not the profile owner — the dispatcher supplies it by calling
    /// <see cref="IFollowingReadService.GetRelationshipStateAsync"/> separately.
    /// </summary>
    Task<ProfileHeaderDto?> GetProfileHeaderAsync(int userId, bool includePrivate);

    /// <summary>
    /// Returns the owner's bio HTML (from <c>UserProfile.Text</c>), or <c>null</c> when the user
    /// doesn't exist or the profile is hidden from the current viewer (same
    /// <see cref="ProfileVisibility"/> gate as <see cref="GetProfileHeaderAsync"/>: <c>Private</c>
    /// → owner only; <c>UsersOnly</c> → authenticated viewers; the owner always passes). The
    /// stored HTML is already sanitized (sanitize-once-on-save convention); render via
    /// <c>RichTextView</c> directly.
    /// </summary>
    Task<string?> GetProfileTextAsync(int userId);
}
