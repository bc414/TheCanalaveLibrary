namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the SiteSettings cluster. Mod-gated at the service (the mod page's
/// <c>[Authorize]</c> is affordance, this is the enforcement) — every method requires
/// Moderator/Admin per <c>IActiveUserContext</c>.
/// </summary>
public interface ISiteSettingsWriteService : ISiteSettingsReadService
{
    /// <summary>Upserts <paramref name="settingKey"/> to <paramref name="value"/>.
    /// Throws <see cref="UnauthorizedAccessException"/> for non-moderators.</summary>
    Task SetIntAsync(string settingKey, int value);
}
