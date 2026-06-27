using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

public class Theme
{
    public int ThemeId { get; set; }

    /// <summary>Display name shown to users (e.g. "Pokémon"). Never used in URLs or paths.</summary>
    [Required] [MaxLength(100)] public string Name { get; set; } = null!;

    /// <summary>
    /// URL-safe slug used as the path segment in sprite URLs (e.g. "pokemon").
    /// This is the value baked into the <c>canalave:theme</c> claim and used by
    /// <c>ISpriteReadService.GetSpriteUrl</c>. Must be lowercase, ASCII, no spaces.
    /// </summary>
    [Required] [MaxLength(64)] public string Slug { get; set; } = null!;

    [Required] [MaxLength(512)]
    public string Description { get; set; } = null!;
}