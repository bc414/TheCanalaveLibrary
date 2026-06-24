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
///   <item>Per-SearchMode default-settings matrix (§8.7 entities) — deferred to WU28; these
///         DTOs carry explicit user intent, not defaults.</item>
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

    /// <summary>Tags the story must have (AND semantics across included ids).</summary>
    public IReadOnlyList<int> IncludedTagIds { get; init; } = [];

    /// <summary>Tags the story must not have (any match = excluded).</summary>
    public IReadOnlyList<int> ExcludedTagIds { get; init; } = [];

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
