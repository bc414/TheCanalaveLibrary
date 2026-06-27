using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Convenience extensions for <see cref="ISpriteReadService"/> that read theme + animation
/// preference from an <see cref="IActiveUserContext"/>, eliminating the repeated triple
/// (activeUser.Theme, spriteIdentifier, activeUser.PrefersAnimatedSprites) at call sites.
/// </summary>
public static class SpriteReadServiceExtensions
{
    /// <summary>
    /// Resolves the best sprite URL for <paramref name="spriteIdentifier"/> using the active
    /// user's theme and animation preference.
    /// </summary>
    public static string GetSpriteUrl(
        this ISpriteReadService svc,
        IActiveUserContext user,
        string spriteIdentifier)
        => svc.GetSpriteUrl(user.Theme, spriteIdentifier, user.PrefersAnimatedSprites);
}
