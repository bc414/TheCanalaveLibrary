using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

public class ServerSpriteReadService(IWebHostEnvironment env) : ISpriteReadService
{
    public string GetSpriteUrl(string theme, string spriteIdentifier, bool userPrefersAnimatedSprites)
    {
        if (!userPrefersAnimatedSprites)
        {
            return GetStaticPath(theme, spriteIdentifier);
        }
        else
        {
            string animatedRelativePath = $"/sprites/themes/{theme}/animated/{spriteIdentifier}.webp";
            if (File.Exists(Path.Combine(env.WebRootPath, "sprites", "themes", theme, "animated", $"{spriteIdentifier}.webp")))
            {
                return animatedRelativePath;
            }
            else
            {
                return GetStaticPath(theme, spriteIdentifier);
            }
        }
    }

    private string GetStaticPath(string theme, string spriteIdentifier)
    {
        string staticPath = $"/sprites/themes/{theme}/static/{spriteIdentifier}.png";
        if (File.Exists(Path.Combine(env.WebRootPath, "sprites", "themes", theme, "static", $"{spriteIdentifier}.png")))
        {
            return staticPath;
        }
        else
        {
            return $"/sprites/themes/{theme}/unknown.png";
        }
    }
}