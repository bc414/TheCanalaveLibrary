namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the cross-cutting SiteSettings cluster — DB-backed, mod-editable runtime knobs
/// (see <c>layer2-services.md</c> §"Site Settings"). Deliberately uncached: reads are single-row
/// PK lookups, and the whole point is that a mod edit takes effect on the next read.
/// </summary>
public interface ISiteSettingsReadService
{
    /// <summary>
    /// The integer value of <paramref name="settingKey"/>, or <paramref name="fallback"/> when
    /// the row is missing or unparseable (callers pass the key's
    /// <see cref="SiteSettingKeys"/>-paired default). Never throws for absent keys.
    /// </summary>
    Task<int> GetIntAsync(string settingKey, int fallback);
}
