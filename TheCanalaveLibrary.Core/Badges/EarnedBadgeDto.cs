namespace TheCanalaveLibrary.Core;

/// <summary>
/// Full badge descriptor for the curation settings UI — includes both visible and hidden earned
/// badges. Produced by <see cref="IBadgeReadService.GetMyBadgesForCurationAsync"/>.
/// <para>
/// Contrast with <see cref="UserCardBadgeDto"/> (display-only, icon + name only, used by the
/// UserCard leaf and ProfileBanner surfaces). This DTO carries the curation fields
/// (<see cref="DisplayOrder"/>, <see cref="BadgeKey"/>, <see cref="SortOrder"/>) needed by the
/// <see cref="IBadgeWriteService.SetDisplayOrderAsync"/> call and the settings form.
/// </para>
/// </summary>
/// <param name="BadgeKey">String PK that identifies the badge type (matches <c>SiteBadges.*</c> constants).</param>
/// <param name="DisplayName">Human-readable badge name shown in the curation UI.</param>
/// <param name="Description">Prose description of how the badge is earned; may be null for legacy rows.</param>
/// <param name="IconUrl">Base URL for the badge icon; rendered with an <img> tag.</param>
/// <param name="SortOrder">Catalogue display order — used to sequence hidden badges in the settings form.</param>
/// <param name="DisplayOrder">
/// 0 = hidden (not shown on profile or UserCards); &gt; 0 = visible, ordered ascending.
/// Newly awarded badges default to visible (<c>max existing DisplayOrder + 1</c>).
/// </param>
public record EarnedBadgeDto(
    string BadgeKey,
    string DisplayName,
    string? Description,
    string IconUrl,
    int SortOrder,
    int DisplayOrder);
