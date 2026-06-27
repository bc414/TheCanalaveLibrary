namespace TheCanalaveLibrary.Core;

/// <summary>
/// Server-only write-time probe: checks whether a sprite asset physically exists for a given
/// (theme slug, sprite identifier) pair. Used exclusively at mod-write time in
/// <c>ServerTagWriteService</c> — never called at render time (rendering is optimistic + browser
/// <c>onerror</c> fallback). See <c>audit/Sprites.md</c> L2 and <c>layer2-services.md</c>
/// §"Sprite URLs Are Resolved At Render Time."
///
/// <para>MVP implementation: <see cref="LocalSpriteAssetProbe"/> — <c>File.Exists</c> against
/// <c>wwwroot/sprites/themes/{slug}/static/{id}.png</c>. The static <c>.png</c> is the
/// authoritative form (every sprite must have a static version; animated <c>.webp</c> is optional).
/// </para>
///
/// <para>Post-MVP implementation: R2 <c>HeadObject</c> (when <c>S3SpriteAssetProbe</c> is wired
/// up behind the same interface). No code change here at cutover.</para>
///
/// <para><b>Not registered client-side</b> — this interface exists only in Core so the contract
/// is testable; implementations live in Server.</para>
/// </summary>
public interface ISpriteAssetProbe
{
    /// <summary>
    /// Returns <c>true</c> if the static <c>.png</c> asset for <paramref name="spriteIdentifier"/>
    /// exists in the given theme's folder; <c>false</c> if absent.
    /// </summary>
    /// <param name="themeSlug">URL-safe theme slug (e.g. <c>"pokemon"</c>).</param>
    /// <param name="spriteIdentifier">Semantic key (e.g. <c>"bulbasaur"</c>).</param>
    Task<bool> ExistsAsync(string themeSlug, string spriteIdentifier, CancellationToken ct = default);
}
