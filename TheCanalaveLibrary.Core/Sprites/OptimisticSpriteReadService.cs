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
/// <see cref="ClientConfigDto.DefaultSpriteBaseUrl"/>). The server reads it once into its
/// <see cref="ClientConfigDto"/> singleton; the WASM client receives the same value from
/// <c>GET /api/client-config</c> at boot (<c>ClientRuntimeConfig</c>; WU-ParityAndRemaining,
/// 2026-10-13). So an R2/CDN cutover is that one value + an Rclone sync on both render phases, with
/// zero code change here.</para>
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
