using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// This service runs in the browser, so it cannot access the file system to see if a sprite exists.
/// Just make the optimistically construct the url and hope that it works.
/// </summary>
public class OptimisticSpriteService : ISpriteService
{
    public string GetSpriteUrl(string theme, string spriteIdentifier, bool userPrefersAnimatedSprites)
    {
        if (!userPrefersAnimatedSprites)
        {
            return $"/sprites/themes/{theme}/static/{spriteIdentifier}.png";
        }
        else
        {
            return $"/sprites/themes/{theme}/animated/{spriteIdentifier}.webp";
        }
    }
}