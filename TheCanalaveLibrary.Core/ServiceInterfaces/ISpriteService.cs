namespace TheCanalaveLibrary.Core;

public interface ISpriteService
{
    string GetSpriteUrl(string theme, string spriteIdentifier, bool userPrefersAnimatedSprites);
}