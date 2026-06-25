namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the Theme sub-feature (Feature 3 — Sprite &amp; Theme System). Exposes the
/// list of available themes for the settings page's Appearance section; the full sprite-resolution
/// path is handled by <see cref="ISpriteReadService"/>.
/// Server impl reads the <c>Themes</c> table (small, read-heavy — suitable for a brief in-process
/// cache if desired, but not required for MVP).
/// </summary>
public interface IThemeReadService
{
    /// <summary>
    /// Returns all available themes ordered by display name. The list is small and stable
    /// (seeded, rarely changed) — callers may render it as a <c>&lt;select&gt;</c> directly.
    /// </summary>
    Task<IReadOnlyList<ThemeDto>> GetThemesAsync();
}
