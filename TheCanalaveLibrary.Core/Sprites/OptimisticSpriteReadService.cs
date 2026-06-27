namespace TheCanalaveLibrary.Core;

/// <summary>
/// Stateless, optimistic URL builder — the single shared implementation of
/// <see cref="ISpriteReadService"/> on both Server and Client. Constructs sprite
/// URLs purely from string concatenation; never touches the filesystem or network.
///
/// <para>Registered as a <b>singleton</b> (no mutable state). Missing assets are handled
/// client-side via an <c>onerror</c> fallback chain in the render component
/// (<c>webp → png → unknown.png</c>).</para>
///
/// <para>The base URL comes from configuration (<c>Sprites:BaseUrl</c>, defaulting to
/// <c>/sprites/themes</c>). R2/CDN cutover = change that one value + Rclone sync, zero
/// code change needed here.</para>
/// </summary>
public sealed class OptimisticSpriteReadService(string baseUrl) : ISpriteReadService
{
    public string GetSpriteUrl(string themeSlug, string spriteIdentifier, bool prefersAnimated)
    {
        return prefersAnimated
            ? $"{baseUrl}/{themeSlug}/animated/{spriteIdentifier}.webp"
            : $"{baseUrl}/{themeSlug}/static/{spriteIdentifier}.png";
    }
}
