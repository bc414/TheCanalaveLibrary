namespace TheCanalaveLibrary.Core;

/// <summary>
/// One badge in a UserCardDto's curated subset (DisplayOrder &gt; 0, per spec §5.30.7). Minimal by
/// design — only what the UserCard leaf renders today (a small icon with a tooltip name). The
/// Badges feature (WU36) owns the full badge model and may extend this shape when it lands.
/// </summary>
public record UserCardBadgeDto(
    string IconUrl,
    string Name
);
