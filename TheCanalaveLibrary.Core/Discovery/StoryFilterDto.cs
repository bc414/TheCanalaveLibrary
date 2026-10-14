namespace TheCanalaveLibrary.Core;

/// <summary>
/// Source-agnostic filter criteria emitted by <c>ResultsFilterPanel</c> (WU23, spec §5.27).
/// Each discovery surface (Search, Profiles, Bookshelves) supplies its own source as an optional
/// restrict set of story ids (null = the whole library); <c>GetListingsAsync</c> applies these
/// predicates on top of it.
///
/// <b>Excluded by design:</b>
/// <list type="bullet">
///   <item>Content-rating — handled globally by <c>ReadOnlyApplicationDbContext</c>'s named query
///         filter via <see cref="IActiveUserContext"/>; never belongs here.</item>
///   <item>Per-SearchMode default-settings matrix (§8.7 entities) — seeded externally by
///         <c>IDiscoveryDefaultsReadService</c> before the DTO is built; the DTO carries
///         explicit viewer intent (already-merged effective exclusions).</item>
///   <item>Source axis — each consumer wires its own source query;
///         <c>GetListingsAsync</c> is Source=All only.</item>
/// </list>
/// </summary>
public sealed record StoryFilterDto
{
    /// <summary>
    /// Free-text search query. Drives <c>SearchVector.Matches()</c> and enables
    /// <see cref="DefaultSortOrder.Relevance"/> as a sort option (ignored when null/empty).
    /// </summary>
    public string? TextQuery { get; init; }

    /// <summary>
    /// Tags the story must have. Boolean combination is controlled by <see cref="IncludeMode"/>:
    /// default <see cref="TagIncludeMode.And"/> requires all; <see cref="TagIncludeMode.Or"/>
    /// requires any one.
    /// </summary>
    public IReadOnlyList<int> IncludedTagIds { get; init; } = [];

    /// <summary>Tags the story must not have (any match = excluded).</summary>
    public IReadOnlyList<int> ExcludedTagIds { get; init; } = [];

    /// <summary>
    /// How <see cref="IncludedTagIds"/> are combined. Default <see cref="TagIncludeMode.And"/>
    /// (story must have all included tags) preserves existing behaviour on all surfaces.
    /// <see cref="TagIncludeMode.Or"/> (story must have at least one) is surfaced on
    /// <c>/discover</c> only via <c>TagFilter.AllowIncludeModeToggle</c>.
    /// The exclude axis always uses ANY/none and has no mode flag.
    /// </summary>
    public TagIncludeMode IncludeMode { get; init; } = TagIncludeMode.And;

    /// <summary>
    /// Interaction kinds to exclude for the current viewer. Stories where the viewer holds
    /// any of these interaction states are omitted. Applied server-side, scoped to the active
    /// user via <see cref="IActiveUserContext"/>; anonymous viewers see no exclusions.
    /// </summary>
    public IReadOnlyList<UserStoryInteractionTypeEnum> ExcludedInteractions { get; init; } = [];

    /// <summary>
    /// Result ordering. <see cref="DefaultSortOrder.Relevance"/> is only valid when
    /// <see cref="TextQuery"/> is non-empty; the service falls back to
    /// <see cref="DefaultSortOrder.DatePublished"/> otherwise (as <c>IStoryReadService.GetListingsAsync</c>
    /// documents; this comment used to say <c>LastUpdated</c>).
    /// </summary>
    public DefaultSortOrder Sort { get; init; } = DefaultSortOrder.DatePublished;

    /// <summary>1-based page number.</summary>
    public int Page { get; init; } = 1;

    /// <summary>
    /// Results per page. Seeded by the page dispatcher from the viewer's
    /// <c>ReaderSettingsDto.DefaultPaginationSize</c> (signed-in viewers; anonymous viewers keep 20);
    /// <c>ResultsFilterPanel</c> echoes it on Apply. Server-side bounds are worksheet D20's bounds
    /// half (pending).
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// Ship filters the story must satisfy — ALL of them (AND across ships, mirroring the tag
    /// axis; WU-TagFanon). Each entry matches stories containing a single pairing whose member
    /// set covers every named character (member ids inherit hierarchy roll-up, so a base-species
    /// id matches its fanon children). Transient viewer intent — never persisted in
    /// SavedTagSelection (tag-axis-only, settled F15 scope).
    /// </summary>
    public IReadOnlyList<ShipFilterDto> IncludedShips { get; init; } = [];

    /// <summary>Ship filters the story must NOT satisfy (any match = excluded) — symmetric
    /// with the tag exclude axis, including roll-up.</summary>
    public IReadOnlyList<ShipFilterDto> ExcludedShips { get; init; } = [];
}

/// <summary>
/// One ship criterion (WU-TagFanon): character tag ids that must all appear among a single
/// pairing's members, optionally constrained to romantic or platonic. One member = "any ship
/// involving this character". Bounded at <see cref="MaxMembers"/> members (enforced server-side).
/// </summary>
public sealed record ShipFilterDto
{
    public const int MaxMembers = 3;

    public IReadOnlyList<int> MemberTagIds { get; init; } = [];

    /// <summary>Null = either pairing type matches.</summary>
    public CharacterPairingType? PairingType { get; init; }

    /// <summary>
    /// Joins member display names into a ship label ("Ash ♥ Misty", "Ash &amp; Brock"). Shared by
    /// <c>ShipFilter</c> (which builds the label at pick time from the chips the user selected) and
    /// by the page dispatcher (which rebuilds it when *seeding* a ship from bare member ids — see
    /// <c>layer3.5-structure.md</c> §"Seed state vs. live fetch in filter components"). One
    /// implementation so the two paths can never render the same ship differently.
    /// </summary>
    public static string JoinMemberNames(CharacterPairingType? pairingType, IEnumerable<string> memberNames) =>
        string.Join(pairingType == CharacterPairingType.Platonic ? " & " : " ♥ ", memberNames);
}
