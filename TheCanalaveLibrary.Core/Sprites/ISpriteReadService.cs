namespace TheCanalaveLibrary.Core;

/// <summary>
/// Constructs sprite URLs from a (theme slug, identifier, animation preference) triple.
/// Registered as a singleton on both Server and Client; implementation is stateless.
/// The URL is optimistic — callers must handle missing assets (typically via an HTML
/// <c>onerror</c> chain). See <c>layer2-services.md</c> "Sprite URLs Are Resolved At Render Time."
/// </summary>
public interface ISpriteReadService
{
    /// <summary>
    /// Returns the primary sprite URL.
    /// </summary>
    /// <param name="themeSlug">URL-safe slug from <see cref="Theme.Slug"/> (e.g. <c>"pokemon"</c>).</param>
    /// <param name="spriteIdentifier">Semantic key stored on <see cref="Tag.SpriteIdentifier"/> (e.g. <c>"bulbasaur"</c>).</param>
    /// <param name="prefersAnimated">When <c>true</c>, targets the animated <c>.webp</c>; otherwise the static <c>.png</c>.</param>
    string GetSpriteUrl(string themeSlug, string spriteIdentifier, bool prefersAnimated);
}