using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// One DB-backed, mod-editable runtime tuning value (string-key lookup pattern —
/// <c>layer1-data-model.md</c> enum/lookup framework). Cross-cutting SiteSettings cluster: these
/// are values moderators change from a mod surface <em>without a deploy</em> — distinct from
/// <c>appsettings</c> (deploy-time operator config) and from per-user settings (Profiles).
/// Values are stored as strings; typing lives in <see cref="ISiteSettingsReadService"/>'s
/// accessors, which fall back to the key's default (<see cref="SiteSettingKeys"/>) when a row is
/// missing or unparseable. See <c>layer2-services.md</c> §"Site Settings".
/// </summary>
public class SiteSetting
{
    [Key]
    [MaxLength(128)]
    public string SettingKey { get; set; } = null!;

    [Required]
    [MaxLength(256)]
    public string Value { get; set; } = null!;
}
