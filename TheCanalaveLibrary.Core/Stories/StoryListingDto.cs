namespace TheCanalaveLibrary.Core;

/// <summary>
/// Render-ready story summary for listing/browse surfaces (StoryCard, StoryDeck) — minted WU12, the
/// partition-anchored DTO ≈ the columns of StoryListing (§3.8) plus the hot Story scalars a card needs.
/// <see cref="CoverArtRelativeUrl"/> is copied verbatim from the entity (never resolved through a
/// service) — same discipline as avatars, see layer2-services.md "Avatars are a related but distinct
/// case" / "Cover art is the same pattern as avatars." <see cref="Tags"/> are already sprite-resolved
/// TagChipDtos (the producing read service called ISpriteReadService.GetSpriteUrl during projection).
///
/// Deliberately excludes per-viewer interaction state (IsFavorited, etc.) — that's cross-partition
/// (UserStoryInteraction), resolved by a sibling merge at the listing call site (WU15/WU16), per §3.8 +
/// §3.6 "Context-Specific Data Augmentation." The content-rating filter has already been applied by the
/// time a Story row reaches this projection (ApplicationDbContext's global query filter) — no rating
/// check belongs here.
/// </summary>
public record StoryListingDto(
    int StoryId,
    string Title,
    string? CoverArtRelativeUrl,
    int? AuthorId,
    string AuthorName,
    int WordCount,
    StoryStatusEnum StoryStatusId,
    Rating Rating,
    DateTime LastUpdatedDate,
    IReadOnlyList<TagChipDto> Tags);
