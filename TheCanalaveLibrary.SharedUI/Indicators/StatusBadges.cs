using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// The single source for story status / rating badge classes (Indicator role, unified tint
/// recipe: bg-X/15 · text-X · ring-X/30 — layer4-style.md "Element Roles"). Replaces the
/// switch tables formerly triplicated across StoryCard/StoryDesktop/StoryMobile, which were
/// pinned to raw Tailwind palette colors and could never follow token changes.
///
/// Every arm is a FULL LITERAL class string — Tailwind's JIT scans source for literals, so
/// interpolated class construction would silently generate nothing (the bare-name-trap family).
/// </summary>
public static class StatusBadges
{
    /// <summary>Shared badge shell (shape + typography); combine with a recipe below.</summary>
    public const string Shell = "rounded-full px-2 py-0.5 text-xs font-medium";

    public static string ForStatus(StoryStatusEnum status) => status switch
    {
        StoryStatusEnum.InProgress => "bg-(--color-progress)/15 text-(--color-progress) ring-1 ring-(--color-progress)/30",
        StoryStatusEnum.Completed => "bg-(--color-success)/15 text-(--color-success) ring-1 ring-(--color-success)/30",
        StoryStatusEnum.OnHiatus => "bg-(--color-warning)/15 text-(--color-warning) ring-1 ring-(--color-warning)/30",
        StoryStatusEnum.Cancelled => "bg-(--color-danger)/15 text-(--color-danger) ring-1 ring-(--color-danger)/30",
        StoryStatusEnum.Rejected => "bg-(--color-danger)/15 text-(--color-danger) ring-1 ring-(--color-danger)/30",
        // Draft, PendingApproval, OpenBeta, Rewriting — neutral
        _ => "bg-(--color-text-muted)/15 text-(--color-text-muted) ring-1 ring-(--color-text-muted)/30",
    };

    public static string ForRating(Rating rating) => rating switch
    {
        Rating.E => "bg-(--color-success)/15 text-(--color-success) ring-1 ring-(--color-success)/30",
        Rating.T => "bg-(--color-warning)/15 text-(--color-warning) ring-1 ring-(--color-warning)/30",
        Rating.M => "bg-(--color-danger)/15 text-(--color-danger) ring-1 ring-(--color-danger)/30",
        _ => "bg-(--color-text-muted)/15 text-(--color-text-muted) ring-1 ring-(--color-text-muted)/30",
    };
}
