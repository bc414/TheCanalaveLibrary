namespace TheCanalaveLibrary.Core;

public interface ISpriteReadService
{
    string GetSpriteUrl(string theme, string spriteIdentifier, bool userPrefersAnimatedSprites);
}