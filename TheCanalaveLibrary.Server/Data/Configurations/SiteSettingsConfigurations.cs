using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

/// <summary>
/// SiteSettings cross-cutting cluster — DB-backed mod-editable runtime knobs. Seeded from
/// <see cref="SiteSettingKeys.Seed"/> so the defaults live in exactly one place (Core), shared
/// with the read-service fallback. See <c>layer2-services.md</c> §"Site Settings".
/// </summary>
public sealed class SiteSettingConfiguration : IEntityTypeConfiguration<SiteSetting>
{
    public void Configure(EntityTypeBuilder<SiteSetting> builder)
    {
        builder.HasData(SiteSettingKeys.Seed.Select(s => new { SettingKey = s.Key, Value = s.DefaultValue }));
    }
}
