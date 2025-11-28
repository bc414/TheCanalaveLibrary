namespace TheCanalaveLibrary.Core.ServiceInterfaces;

public interface ISpriteService
{
    string GetSpriteUrl(string theme, string spriteIdentifier, bool userPrefersAnimatedSprites);
}