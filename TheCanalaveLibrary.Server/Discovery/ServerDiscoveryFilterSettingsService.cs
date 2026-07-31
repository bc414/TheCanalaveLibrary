using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server implementation of <see cref="IDiscoveryFilterSettingsService"/> (WU-DiscoveryOverrideUI,
/// closes tracker item B7 — spec §8.7). Sibling of <see cref="ServerDiscoveryDefaultsReadService"/>:
/// reads the same two tables but always resolves the authenticated caller (no anonymous path),
/// and adds the write half that service deliberately does not carry (it's anonymous-callable and
/// stays a pure read).
/// </summary>
public class ServerDiscoveryFilterSettingsService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser) : IDiscoveryFilterSettingsService
{
    /// <summary>
    /// Search modes with a confirmed live consumer of
    /// <see cref="IDiscoveryDefaultsReadService.GetDefaultExcludedInteractionsAsync"/> today
    /// (<c>SearchPage.razor</c>, <c>TreeSearchPage.razor</c> → <c>AutoTreeSearch</c>,
    /// <c>RelatedStoriesSection.razor</c> → <c>AlsoFavorited</c>,
    /// <c>ServerCoOccurrenceReadService</c> → <c>AlsoRecommended</c>). Manual <c>TreeSearch</c> and
    /// the three <c>Profile*</c> modes have no consumer and are deliberately excluded — see
    /// <see cref="DiscoveryFilterModeDto"/>'s doc comment. Order here is display order.
    /// </summary>
    private static readonly string[] ConsumedModes =
    [
        SiteSearchModes.SearchPage,
        SiteSearchModes.AutoTreeSearch,
        SiteSearchModes.AlsoFavorited,
        SiteSearchModes.AlsoRecommended
    ];

    /// <summary>
    /// The six mappable (FilterKey, EnumValue) pairs, in <see cref="UserStoryInteractionTypeEnum"/>
    /// declaration order — the same left-to-right order <c>UserStoryInteractionFilter</c>'s
    /// checkboxes render in, so the settings matrix and the live filter panel read identically.
    /// Derived from <see cref="ServerDiscoveryDefaultsReadService.KeyToEnum"/> (single source of
    /// truth for which catalog keys are mappable — <c>HasStarted</c> is not).
    /// </summary>
    private static readonly (string FilterKey, UserStoryInteractionTypeEnum Type)[] OrderedFilterKeys =
        [.. Enum.GetValues<UserStoryInteractionTypeEnum>()
            .Select(t => (ServerDiscoveryDefaultsReadService.KeyToEnum.Single(kv => kv.Value == t).Key, t))];

    public async Task<IReadOnlyList<DiscoveryFilterModeDto>> GetMyMatrixAsync()
    {
        int userId = activeUser.RequireUserId();

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

        Dictionary<string, string> modeNames = await readDb.SearchModes
            .Where(m => ConsumedModes.Contains(m.SearchModeKey))
            .ToDictionaryAsync(m => m.SearchModeKey, m => m.Name);

        // System-default matrix: only rows that were seeded exist (today, just "Ignored" per
        // mode) — an absent (mode, key) pair means "system default is false," matching how
        // ServerDiscoveryDefaultsReadService's merge treats un-seeded keys.
        Dictionary<(string Mode, string Key), bool> defaults = await readDb
            .DefaultUserStoryInteractionFilterSettings
            .Where(d => ConsumedModes.Contains(d.SearchModeKey))
            .ToDictionaryAsync(d => (d.SearchModeKey, d.UserStoryInteractionFilterKey), d => d.IsEnabled);

        Dictionary<(string Mode, string Key), bool> overrides = await readDb
            .UserStoryInteractionFilterSettings
            .Where(o => o.UserId == userId && ConsumedModes.Contains(o.SearchModeKey))
            .ToDictionaryAsync(o => (o.SearchModeKey, o.UserStoryInteractionFilterKey), o => o.IsEnabled);

        List<DiscoveryFilterModeDto> result = [];
        foreach (string mode in ConsumedModes)
        {
            List<DiscoveryFilterRowDto> rows = [];
            foreach ((string filterKey, UserStoryInteractionTypeEnum type) in OrderedFilterKeys)
            {
                bool systemDefault = defaults.GetValueOrDefault((mode, filterKey), false);
                bool isOverridden = overrides.TryGetValue((mode, filterKey), out bool overrideValue);
                bool effective = isOverridden ? overrideValue : systemDefault;
                rows.Add(new DiscoveryFilterRowDto(filterKey, type, systemDefault, effective, isOverridden));
            }
            result.Add(new DiscoveryFilterModeDto(mode, modeNames.GetValueOrDefault(mode, mode), rows));
        }

        return result;
    }

    public async Task SetOverrideAsync(string searchModeKey, string filterKey, bool isEnabled)
    {
        int userId = activeUser.RequireUserId();

        // Defensive no-op on an unmappable key or an unconsumed mode — mirrors
        // ServerNotificationWriteService.SetSettingAsync's "unknown type → no-op" guard. The
        // settings form only ever echoes values GetMyMatrixAsync handed it, so this should not
        // happen in practice; a direct/malformed call is silently ignored rather than throwing.
        if (!ConsumedModes.Contains(searchModeKey) || !ServerDiscoveryDefaultsReadService.KeyToEnum.ContainsKey(filterKey))
            return;

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        DefaultUserStoryInteractionFilterSetting? systemRow = await readDb
            .DefaultUserStoryInteractionFilterSettings
            .FirstOrDefaultAsync(d => d.SearchModeKey == searchModeKey && d.UserStoryInteractionFilterKey == filterKey);
        bool systemDefault = systemRow?.IsEnabled ?? false;

        if (isEnabled == systemDefault)
        {
            // Sparse model: delete the override row so absence means "use default."
            await writeDb.UserStoryInteractionFilterSettings
                .Where(s => s.UserId == userId
                            && s.SearchModeKey == searchModeKey
                            && s.UserStoryInteractionFilterKey == filterKey)
                .ExecuteDeleteAsync();
            return;
        }

        UserStoryInteractionFilterSetting? existing = await writeDb.UserStoryInteractionFilterSettings
            .FirstOrDefaultAsync(s => s.UserId == userId
                                      && s.SearchModeKey == searchModeKey
                                      && s.UserStoryInteractionFilterKey == filterKey);

        if (existing is null)
        {
            writeDb.UserStoryInteractionFilterSettings.Add(new UserStoryInteractionFilterSetting
            {
                UserId = userId,
                SearchModeKey = searchModeKey,
                UserStoryInteractionFilterKey = filterKey,
                IsEnabled = isEnabled
            });
        }
        else
        {
            existing.IsEnabled = isEnabled;
        }

        await writeDb.SaveChangesAsync();
    }
}
