using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server write implementation for the SiteSettings cluster. Mod-gating is enforced here with the
/// shared <c>ActiveUser.RequireModerator()</c> guard (anonymous → 401, non-moderator → 403) — mod-page
/// <c>[Authorize]</c> attributes are affordance, not the gate.
/// </summary>
public class ServerSiteSettingsWriteService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser)
    : ServerSiteSettingsReadService(readDbFactory), ISiteSettingsWriteService
{
    public async Task SetIntAsync(string settingKey, int value)
    {
        activeUser.RequireModerator();

        SiteSetting? existing = await writeDb.SiteSettings
            .FirstOrDefaultAsync(s => s.SettingKey == settingKey);

        if (existing is null)
            writeDb.SiteSettings.Add(new SiteSetting { SettingKey = settingKey, Value = value.ToString() });
        else
            existing.Value = value.ToString();

        await writeDb.SaveChangesAsync();
    }
}
