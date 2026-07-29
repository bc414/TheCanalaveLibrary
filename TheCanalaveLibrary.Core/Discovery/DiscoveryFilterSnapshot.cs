namespace TheCanalaveLibrary.Core;

/// <summary>
/// The device-local persisted form of a <c>/discover</c> filter (decision row 13) — what
/// <c>DiscoveryFilterStore</c> writes to browser localStorage so filters survive navigating to a
/// story and back.
/// <para>
/// <b>Ids and scalars only</b>, never display strings and never listing payloads: chips and ship
/// labels rehydrate through the existing batch reads on load, and anything the viewer can no longer
/// see prunes silently (<see cref="ToFilter"/>). That is the same contract manual tree search uses
/// — <c>layer3.5-structure.md</c> §"The shared tree canvas" — and it is what keeps stale local data
/// from re-widening a viewer's access.
/// </para>
/// <para>
/// Arrays rather than <c>IReadOnlyList</c> for the same reason <c>SearchPage</c>'s
/// <c>[PersistentState]</c> properties are arrays: STJ round-trips concrete types most predictably.
/// Deliberately omits <c>Page</c>/<c>PageSize</c> — restoring someone onto page 7 of a result set
/// that has since changed is worse than restoring them to page 1.
/// </para>
/// <para>
/// This is <b>not</b> a <c>SavedTagSelection</c> and must never become one: per-browser, never
/// synced, never shared, invisible to every other user.
/// </para>
/// </summary>
public sealed record DiscoveryFilterSnapshot(
    string? TextQuery,
    int[] IncludedTagIds,
    int[] ExcludedTagIds,
    TagIncludeMode IncludeMode,
    UserStoryInteractionTypeEnum[] ExcludedInteractions,
    ShipSnapshot[] IncludedShips,
    ShipSnapshot[] ExcludedShips,
    DefaultSortOrder Sort)
{
    /// <summary>Captures the axes worth restoring from a live filter.</summary>
    public static DiscoveryFilterSnapshot From(StoryFilterDto filter) => new(
        filter.TextQuery,
        [.. filter.IncludedTagIds],
        [.. filter.ExcludedTagIds],
        filter.IncludeMode,
        [.. filter.ExcludedInteractions],
        [.. filter.IncludedShips.Select(ShipSnapshot.From)],
        [.. filter.ExcludedShips.Select(ShipSnapshot.From)],
        filter.Sort);

    /// <summary>
    /// Rebuilds a filter, dropping every tag and ship member id absent from
    /// <paramref name="visibleTagIds"/> — the prune step. A ship whose members are ALL pruned is
    /// dropped entirely; a ship that keeps at least one member survives in narrowed form rather
    /// than silently widening into "any ship involving anyone".
    /// </summary>
    public StoryFilterDto ToFilter(IReadOnlySet<int> visibleTagIds) => new()
    {
        TextQuery = string.IsNullOrWhiteSpace(TextQuery) ? null : TextQuery,
        IncludedTagIds = [.. IncludedTagIds.Where(visibleTagIds.Contains)],
        ExcludedTagIds = [.. ExcludedTagIds.Where(visibleTagIds.Contains)],
        IncludeMode = IncludeMode,
        ExcludedInteractions = ExcludedInteractions,
        IncludedShips = [.. IncludedShips.Select(s => s.ToFilter(visibleTagIds)).OfType<ShipFilterDto>()],
        ExcludedShips = [.. ExcludedShips.Select(s => s.ToFilter(visibleTagIds)).OfType<ShipFilterDto>()],
        Sort = Sort,
        Page = 1
    };

    /// <summary>Every tag id referenced by any axis — the single batch to resolve on rehydration.</summary>
    public int[] AllTagIds() =>
    [
        .. IncludedTagIds
            .Concat(ExcludedTagIds)
            .Concat(IncludedShips.Concat(ExcludedShips).SelectMany(s => s.MemberTagIds))
            .Distinct()
    ];
}

/// <summary>One persisted ship criterion — member ids plus the optional pairing constraint.</summary>
public sealed record ShipSnapshot(int[] MemberTagIds, CharacterPairingType? PairingType)
{
    public static ShipSnapshot From(ShipFilterDto ship) => new([.. ship.MemberTagIds], ship.PairingType);

    /// <summary>Returns null when no member survived the prune.</summary>
    public ShipFilterDto? ToFilter(IReadOnlySet<int> visibleTagIds)
    {
        int[] members = [.. MemberTagIds.Where(visibleTagIds.Contains)];
        return members.Length == 0 ? null : new ShipFilterDto { MemberTagIds = members, PairingType = PairingType };
    }
}
