using TheCanalaveLibrary.Core.ServiceInterfaces;

namespace TheCanalaveLibrary.Server.Services;

public class FileSystemSpriteService : ISpriteService
{
    private readonly IWebHostEnvironment _env;

    public FileSystemSpriteService(IWebHostEnvironment env)
    {
        _env = env;
    }
    
    public string GetSpriteUrl(string theme, string spriteIdentifier, bool userPrefersAnimatedSprites)
    {
        if (!userPrefersAnimatedSprites)
        {
            return GetStaticPath(theme, spriteIdentifier);
        }
        else
        {
            string animatedRelativePath = $"/sprites/themes/{theme}/animated/{spriteIdentifier}.webp";
            if (File.Exists(Path.Combine(_env.WebRootPath, "sprites", "themes", theme, "animated", $"{spriteIdentifier}.webp")))
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
        if (File.Exists(Path.Combine(_env.WebRootPath, "sprites", "themes", theme, "static", $"{spriteIdentifier}.png")))
        {
            return staticPath;
        }
        else
        {
            return $"/sprites/themes/{theme}/unknown.png";
        }
    }
}