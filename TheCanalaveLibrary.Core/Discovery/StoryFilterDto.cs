namespace TheCanalaveLibrary.Core;

/// <summary>
/// Source-agnostic filter criteria emitted by <c>ResultsFilterPanel</c> (WU23, spec §5.27).
/// Each discovery surface (Search, Profiles, Bookshelves) supplies its own source IQueryable;
/// <c>GetListingsAsync</c> applies these predicates on top of whatever the caller passes in.
///
/// <b>Excluded by design:</b>
/// <list type="bullet">
///   <item>Content-rating — handled globally by <c>ApplicationDbContext</c>'s query filter
///         via <see cref="IActiveUserContext"/>; never belongs here.</item>
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
    /// <see cref="DefaultSortOrder.LastUpdated"/> otherwise.
    /// </summary>
    public DefaultSortOrder Sort { get; init; } = DefaultSortOrder.DatePublished;

    /// <summary>1-based page number.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Results per page. Callers should cap at a reasonable max (e.g. 20).</summary>
    public int PageSize { get; init; } = 20;
}
