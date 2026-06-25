namespace TheCanalaveLibrary.Core;

/// <summary>
/// Render-ready theme record for the Appearance settings control.
/// <see cref="ThemeId"/> is the FK stored on <see cref="User.ThemeId"/>; <see cref="Name"/> is
/// the display label in the theme <c>&lt;select&gt;</c>. Preview color is optional — used for
/// a small swatch if the settings page renders one.
/// </summary>
public record ThemeDto(int ThemeId, string Name, string? PreviewColor);
