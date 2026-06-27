using Microsoft.AspNetCore.Hosting;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// <see cref="ISpriteAssetProbe"/> implementation for local wwwroot sprite storage (dev / MVP).
/// Checks <c>File.Exists</c> against <c>wwwroot/sprites/themes/{slug}/static/{id}.png</c>.
///
/// <para>The static <c>.png</c> is the authoritative form: every complete sprite set must have
/// one. Animated <c>.webp</c> is optional. Probing the static file is therefore the correct
/// correctness gate at mod-write time.</para>
///
/// <para>Post-MVP: replace with <c>R2SpriteAssetProbe</c> (<c>HeadObject</c>) — same interface,
/// zero code change in the consuming tag write service.</para>
/// </summary>
public sealed class LocalSpriteAssetProbe(IWebHostEnvironment env) : ISpriteAssetProbe
{
    public Task<bool> ExistsAsync(string themeSlug, string spriteIdentifier, CancellationToken ct = default)
    {
        string path = Path.Combine(env.WebRootPath, "sprites", "themes", themeSlug, "static", $"{spriteIdentifier}.png");
        return Task.FromResult(File.Exists(path));
    }
}
