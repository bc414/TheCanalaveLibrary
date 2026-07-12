namespace TheCanalaveLibrary.Core;

/// <summary>
/// Pivot request for a STORY anchor in Manual Tree Search (Feature 33 / WU40). Direction is
/// enforced by the type system — this request can only ever ask story→user questions; the
/// user→story questions live on <see cref="UserNeighborsRequest"/>. One pivot = one service
/// call returning every requested section (<see cref="ManualTreeNeighborsDto"/>).
///
/// <para>The three recommendation-family flags widen/narrow ONE query over <c>recommendations</c>
/// (Hidden Gem / Author Spotlight are flags on the same rows, not separate lists): a row is
/// included when (plain ∧ <see cref="IncludeRecommendations"/>) ∨ (gem ∧
/// <see cref="IncludeHiddenGems"/>) ∨ (spotlight ∧ <see cref="IncludeSpotlights"/>).</para>
/// </summary>
public sealed record StoryNeighborsRequest
{
    public required int StoryId { get; init; }

    /// <summary>story → its author (1). Toggleable — never hardcoded on (Author×Pinned would
    /// otherwise be an unavoidable identity round-trip; see `audit/Discovery.md` F33).</summary>
    public bool IncludeAuthor { get; init; } = true;

    public bool IncludeRecommendations { get; init; } = true;
    public bool IncludeHiddenGems { get; init; } = true;
    public bool IncludeSpotlights { get; init; } = true;

    /// <summary>story → users who publicly favorited it (unbounded; paged).</summary>
    public bool IncludeFavoriters { get; init; } = true;

    // Per-section paging (settled 2026-07-12): first page ≈10; "Show more" pages one section.
    public int RecommendationsPage { get; init; } = 1;
    public int FavoritersPage { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

/// <summary>
/// Pivot request for a USER anchor in Manual Tree Search. See
/// <see cref="StoryNeighborsRequest"/> for the direction-by-type and family-flag rules.
/// </summary>
public sealed record UserNeighborsRequest
{
    public required int UserId { get; init; }

    public bool IncludeRecommendations { get; init; } = true;
    public bool IncludeHiddenGems { get; init; } = true;
    public bool IncludeSpotlights { get; init; } = true;

    /// <summary>user → their public favorites (hidden favorites NEVER appear in manual,
    /// regardless of owner consent — settled privacy model).</summary>
    public bool IncludeFavorites { get; init; } = true;

    /// <summary>user → their authored catalog (pinned story badged + sorted first).</summary>
    public bool IncludeAuthored { get; init; } = true;

    /// <summary>user → published stories authored by their ≤5 vouchees (Explore only).</summary>
    public bool IncludeVouchedStories { get; init; } = true;

    public int RecommendationsPage { get; init; } = 1;
    public int FavoritesPage { get; init; } = 1;
    public int AuthoredPage { get; init; } = 1;
    public int VouchedStoriesPage { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

/// <summary>One paged section of a pivot: the page's items plus the section's full count
/// (drives the honest "Show more (N more)" affordance).</summary>
public sealed record ManualTreeSectionDto<T>(IReadOnlyList<T> Items, int TotalCount);

/// <summary>Chip-level display data (title + thumbnail) for one tree node, used to rehydrate a
/// localStorage-persisted tree (which stores IDs + edges only — settled 2026-07-12).</summary>
public sealed record ManualTreeNodeDisplayDto(int EntityId, string Title, string? ImageUrl);

/// <summary>Rehydration result: display data for every persisted id the viewer can still see.
/// Ids absent from these lists are pruned from the client tree (vanished / taken down /
/// rating-gated since the tree was saved).</summary>
public sealed record ManualTreeNodeDisplaysDto(
    IReadOnlyList<ManualTreeNodeDisplayDto> Stories,
    IReadOnlyList<ManualTreeNodeDisplayDto> Users);

/// <summary>
/// One recommendation-family result: the recommendation and its target story's listing —
/// rendered as a compound row (StoryCard + recommendation panel side by side). For a story
/// anchor, <see cref="Story"/> is the anchor itself on every row (the UI may suppress the
/// redundant story half there); for a user anchor each row's story differs.
/// </summary>
public sealed record ManualTreeRecItemDto(RecommendationDto Recommendation, StoryListingDto Story);

/// <summary>
/// Everything one Manual Tree Search pivot returns — all requested sections in one round trip.
/// Sections are null when not applicable to the anchor type or not requested (toggled off);
/// an EMPTY section means "asked, nothing there."
/// </summary>
public sealed record ManualTreeNeighborsDto
{
    // ── Story anchor sections ───────────────────────────────────────────────────
    /// <summary>The story's author (story anchor, when requested). Null also covers the
    /// anonymized-author case (`AuthorId` SET NULL).</summary>
    public UserCardDto? Author { get; init; }

    /// <summary>story → users who publicly favorited it (story anchor).</summary>
    public ManualTreeSectionDto<UserCardDto>? Favoriters { get; init; }

    // ── Either anchor ───────────────────────────────────────────────────────────
    /// <summary>The recommendation family (one query; badges stack on rows). Story anchor:
    /// recs OF the story. User anchor: recs WRITTEN BY the user.</summary>
    public ManualTreeSectionDto<ManualTreeRecItemDto>? RecommendationFamily { get; init; }

    // ── User anchor sections ────────────────────────────────────────────────────
    /// <summary>user → their public favorites.</summary>
    public ManualTreeSectionDto<StoryListingDto>? Favorites { get; init; }

    /// <summary>user → their authored catalog, pinned story first when present.</summary>
    public ManualTreeSectionDto<StoryListingDto>? Authored { get; init; }

    /// <summary>Which story in <see cref="Authored"/> is the user's Pinned Story (badge +
    /// sorted-first), if any. Also what Deep Dive's UserPinnedStory edge follows.</summary>
    public int? PinnedStoryId { get; init; }

    /// <summary>user → their vouchees' published stories (Explore only).</summary>
    public ManualTreeSectionDto<StoryListingDto>? VouchedStories { get; init; }
}
