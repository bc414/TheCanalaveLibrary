using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// The single source for group audience-badge label + classes (Indicator role — mirrors
/// <see cref="StatusBadges"/>). Replaces the switch tables formerly triplicated across
/// GroupCard/GroupDesktop/GroupMobile (MA-509).
///
/// Every arm is a FULL LITERAL class string — Tailwind's JIT scans source for literals, so
/// interpolated class construction would silently generate nothing (the bare-name-trap family).
/// </summary>
public static class GroupDisplayFormat
{
    public static string AudienceBadgeLabel(GroupAudienceType type) => type switch
    {
        GroupAudienceType.SfwOnly => "SFW Only",
        GroupAudienceType.Mature  => "Mature",
        _                         => "Standard"
    };

    public static string AudienceBadgeClasses(GroupAudienceType type) => type switch
    {
        GroupAudienceType.Mature  => "bg-(--color-danger)/10 text-(--color-danger) ring-(--color-danger)/30",
        GroupAudienceType.SfwOnly => "bg-(--color-warning)/10 text-(--color-warning) ring-(--color-warning)/30",
        _                         => "bg-(--color-surface-raised) text-(--color-text-muted) ring-(--color-border)"
    };
}
