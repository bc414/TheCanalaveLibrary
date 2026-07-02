using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server implementation of <see cref="IDiscoveryDefaultsReadService"/> (WU28, spec §8.7).
/// Reads the <see cref="DefaultUserStoryInteractionFilterSetting"/> system matrix and overlays
/// sparse <see cref="UserStoryInteractionFilterSetting"/> per-user rows in C#.
/// </summary>
public class ServerDiscoveryDefaultsReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser) : IDiscoveryDefaultsReadService
{
    /// <summary>
    /// Maps the seven catalog filter-key strings (from <see cref="UserStoryInteractionFilters"/>)
    /// to the six <see cref="UserStoryInteractionTypeEnum"/> values.
    /// <c>HasStarted</c> is intentionally absent — no enum value covers it and the filter panel
    /// does not expose it. Rows with that key are silently dropped from the merged result.
    /// </summary>
    private static readonly Dictionary<string, UserStoryInteractionTypeEnum> KeyToEnum = new()
    {
        [UserStoryInteractionFilters.Completed]      = UserStoryInteractionTypeEnum.Complete,
        [UserStoryInteractionFilters.Favorited]      = UserStoryInteractionTypeEnum.Favorite,
        [UserStoryInteractionFilters.HiddenFavorited] = UserStoryInteractionTypeEnum.PrivateFavorite,
        [UserStoryInteractionFilters.Followed]       = UserStoryInteractionTypeEnum.Follow,
        [UserStoryInteractionFilters.ReadItLater]    = UserStoryInteractionTypeEnum.ReadLater,
        [UserStoryInteractionFilters.Ignored]        = UserStoryInteractionTypeEnum.Ignore,
        // HasStarted deliberately omitted — no enum value; not panel-exposable.
    };

    public async Task<IReadOnlyList<UserStoryInteractionTypeEnum>> GetDefaultExcludedInteractionsAsync(
        string searchModeKey)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

        // Load system defaults for this mode.
        List<(string Key, bool IsEnabled)> defaults = await readDb
            .DefaultUserStoryInteractionFilterSettings
            .Where(d => d.SearchModeKey == searchModeKey)
            .Select(d => new ValueTuple<string, bool>(d.UserStoryInteractionFilterKey, d.IsEnabled))
            .ToListAsync();

        // Build an effective dictionary: key → IsEnabled (system default as baseline).
        Dictionary<string, bool> effective = defaults.ToDictionary(r => r.Key, r => r.IsEnabled);

        // Overlay per-user overrides when the viewer is authenticated.
        if (activeUser.UserId.HasValue)
        {
            int userId = activeUser.UserId.Value;
            List<(string Key, bool IsEnabled)> userOverrides = await readDb
                .UserStoryInteractionFilterSettings
                .Where(u => u.UserId == userId && u.SearchModeKey == searchModeKey)
                .Select(u => new ValueTuple<string, bool>(u.UserStoryInteractionFilterKey, u.IsEnabled))
                .ToListAsync();

            // User row wins per key.
            foreach ((string key, bool isEnabled) in userOverrides)
                effective[key] = isEnabled;
        }

        // Return only the enabled keys that map to a known enum value.
        return effective
            .Where(kv => kv.Value && KeyToEnum.ContainsKey(kv.Key))
            .Select(kv => KeyToEnum[kv.Key])
            .ToList();
    }
}
