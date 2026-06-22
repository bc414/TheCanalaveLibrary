namespace TheCanalaveLibrary.Core;

/// <summary>
/// Render-ready user-summary data emitted by a producing read service for the UserCard leaf
/// (spec §5.30.7). Universal atom — consumed by Following (vouch display), Profiles, Groups,
/// Comments, Recommendations, Messaging, Users search, and tree search nodes; no single feature
/// owns it (see SKILL.md "Code Organization", the Users/ cluster).
///
/// AvatarUrl is a STORED RELATIVE PATH, copied verbatim from <c>User.ProfilePictureRelativeUrl</c>
/// by the producing read service (or substituted with a service-chosen default when null) — unlike
/// TagChipDto.SpriteUrl, it is NOT resolved via ISpriteReadService.GetSpriteUrl (see
/// layer4-style.md "Avatars Are Stored URLs, Not Sprite Keys"). It is still per-user/request-scoped
/// like other resolved-URL DTO fields — never cache across users.
///
/// Badges carries only the curated subset (DisplayOrder &gt; 0, per §5.30.7); empty until the
/// Badges feature (WU36) populates it. The field is minted now so the contract doesn't change
/// shape for existing consumers once badges land.
/// </summary>
public record UserCardDto(
    int UserId,
    string Username,
    string? Tagline,
    string? AvatarUrl,
    IReadOnlyList<UserCardBadgeDto> Badges
);
