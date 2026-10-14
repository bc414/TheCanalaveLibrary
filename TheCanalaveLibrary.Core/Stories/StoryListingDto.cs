namespace TheCanalaveLibrary.Core;

/// <summary>
/// Render-ready story summary for listing/browse surfaces (StoryCard, StoryDeck) — minted WU12, the
/// partition-anchored DTO ≈ the columns of StoryListing (§3.8) plus the hot Story scalars a card needs.
/// <see cref="CoverArtRelativeUrl"/> is copied verbatim from the entity (never resolved through a
/// service) — same discipline as avatars, see layer2-services.md "Avatars are a related but distinct
/// case" / "Cover art is the same pattern as avatars." <see cref="Tags"/> are TagChipDtos carrying the
/// raw <c>SpriteIdentifier</c>; <c>TagChip</c> resolves the URL at render time through
/// ISpriteReadService (layer2-services.md "Sprite URLs Are Resolved At Render Time, In the Component").
/// <see cref="ShortDescription"/> is the warm-partition synopsis (nullable, MaxLength 500) shown
/// on the card as a truncated preview with a full-text tooltip — added WU13 (additive, no migration).
///
/// Deliberately excludes per-viewer interaction state (IsFavorited, etc.) — that's cross-partition
/// (UserStoryInteraction), resolved by a sibling merge at the listing call site (WU15/WU16), per §3.8 +
/// §3.6 "Context-Specific Data Augmentation." No rating check belongs here: the producing query decides.
/// Discovery-plane reads run under ReadOnlyApplicationDbContext's ContentRating filter (the write
/// context carries none), but the Personal-plane path (GetListingsAsync with personalScope and a
/// restrict set, hydrating through the same projection) lifts it on purpose so a viewer's own M
/// favorites stay visible. So a StoryListingDto is not proof that its story is within the viewer's
/// rating ceiling.
/// </summary>
public record StoryListingDto(
    int StoryId,
    string Title,
    string? ShortDescription,
    string? CoverArtRelativeUrl,
    int? AuthorId,
    string AuthorName,
    int WordCount,
    StoryStatusEnum StoryStatusId,
    Rating Rating,
    DateTime LastUpdatedDate,
    IReadOnlyList<TagChipDto> Tags);
