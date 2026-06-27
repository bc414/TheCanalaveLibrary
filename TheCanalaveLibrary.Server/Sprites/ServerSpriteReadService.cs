using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Singleton sprite resolver. Builds a per-(theme, kind) existence cache at startup by scanning
/// <c>wwwroot/sprites/themes/</c> once; all subsequent <see cref="GetSpriteUrl"/> calls are O(1)
/// in-memory lookups with no filesystem I/O per render.
///
/// <para>Sprite sets are static, git-committed assets — their existence does not change within a
/// process's lifetime (a deploy restarts the process). The former approach called
/// <c>File.Exists</c> synchronously on every chip resolution, producing ~100–200 blocking
/// syscalls per listing page under concurrency. This eliminates that entirely.</para>
///
/// <para>Registered as <b>singleton</b> in DI. The three-arg <see cref="GetSpriteUrl(string,string,bool)"/>
/// primitive is kept for unit-testability. The <see cref="SpriteReadServiceExtensions"/> extension
/// provides a convenience overload taking <see cref="IActiveUserContext"/>.</para>
/// </summary>
public class ServerSpriteReadService : ISpriteReadService
{
    // theme → set of identifiers that have an animated .webp
    private readonly IReadOnlyDictionary<string, HashSet<string>> _animatedByTheme;
    // theme → set of identifiers that have a static .png
    private readonly IReadOnlyDictionary<string, HashSet<string>> _staticByTheme;

    private readonly IWebHostEnvironment _env;

    public ServerSpriteReadService(IWebHostEnvironment env)
    {
        _env = env;

        var animated = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var staticSet = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        string themesRoot = Path.Combine(env.WebRootPath, "sprites", "themes");
        if (Directory.Exists(themesRoot))
        {
            foreach (string themeDir in Directory.EnumerateDirectories(themesRoot))
            {
                string theme = Path.GetFileName(themeDir);

                // Animated .webp identifiers
                string animDir = Path.Combine(themeDir, "animated");
                if (Directory.Exists(animDir))
                {
                    animated[theme] = Directory.EnumerateFiles(animDir, "*.webp")
                        .Select(f => Path.GetFileNameWithoutExtension(f)!)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    animated[theme] = [];
                }

                // Static .png identifiers
                string staticDir = Path.Combine(themeDir, "static");
                if (Directory.Exists(staticDir))
                {
                    staticSet[theme] = Directory.EnumerateFiles(staticDir, "*.png")
                        .Select(f => Path.GetFileNameWithoutExtension(f)!)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    staticSet[theme] = [];
                }
            }
        }

        _animatedByTheme = animated;
        _staticByTheme = staticSet;
    }

    /// <summary>
    /// Resolves the best available sprite URL for the given theme and identifier.
    /// Resolution order: animated .webp → static .png → theme's <c>unknown.png</c> fallback.
    /// All lookups are in-memory (O(1) HashSet); no filesystem I/O per call.
    /// </summary>
    public string GetSpriteUrl(string theme, string spriteIdentifier, bool userPrefersAnimatedSprites)
    {
        if (userPrefersAnimatedSprites
            && _animatedByTheme.TryGetValue(theme, out var animSet)
            && animSet.Contains(spriteIdentifier))
        {
            return $"/sprites/themes/{theme}/animated/{spriteIdentifier}.webp";
        }

        if (_staticByTheme.TryGetValue(theme, out var staticSet2)
            && staticSet2.Contains(spriteIdentifier))
        {
            return $"/sprites/themes/{theme}/static/{spriteIdentifier}.png";
        }

        return $"/sprites/themes/{theme}/unknown.png";
    }
}
