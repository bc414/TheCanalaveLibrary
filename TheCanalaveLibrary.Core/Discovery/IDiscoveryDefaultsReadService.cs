namespace TheCanalaveLibrary.Core;

/// <summary>
/// Provides the viewer's effective interaction-exclusion defaults for a given search-mode surface
/// (WU28, spec §8.7). Merges the site-wide system matrix with sparse per-user overrides in C#:
/// system default is the baseline; user row wins when present for a given key.
/// </summary>
public interface IDiscoveryDefaultsReadService
{
    /// <summary>
    /// Returns the set of <see cref="UserStoryInteractionTypeEnum"/> values that should be
    /// excluded by default on the given search-mode surface, after merging the system matrix
    /// with any per-user overrides.
    ///
    /// <b>Anonymous viewers</b> receive the system defaults only (no user-specific rows exist).
    ///
    /// <b>HasStarted key</b> exists in the catalog but has no <see cref="UserStoryInteractionTypeEnum"/>
    /// counterpart; it is dropped from the returned set.
    ///
    /// Use the result to seed <see cref="StoryFilterDto.ExcludedInteractions"/> before displaying
    /// a discovery surface — the user can adjust it via the interaction-filter panel, and the DTO
    /// carries their effective intent.
    /// </summary>
    /// <param name="searchModeKey">A <see cref="SiteSearchModes"/> constant identifying the surface.</param>
    Task<IReadOnlyList<UserStoryInteractionTypeEnum>> GetDefaultExcludedInteractionsAsync(string searchModeKey);
}
