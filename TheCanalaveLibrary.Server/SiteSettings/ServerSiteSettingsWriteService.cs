using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server write implementation for the SiteSettings cluster. Mod-gating is enforced here (the
/// <c>RequireModerator</c> pattern from <c>ServerModerationWriteService</c>) — mod-page
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
        RequireModerator();

        SiteSetting? existing = await writeDb.SiteSettings
            .FirstOrDefaultAsync(s => s.SettingKey == settingKey);

        if (existing is null)
            writeDb.SiteSettings.Add(new SiteSetting { SettingKey = settingKey, Value = value.ToString() });
        else
            existing.Value = value.ToString();

        await writeDb.SaveChangesAsync();
    }

    private void RequireModerator()
    {
        // IsInRole is literal — Admin does NOT inherit Moderator; accept both (IActiveUserContext doc).
        if (!activeUser.IsModerator && !activeUser.IsAdmin)
            throw new UnauthorizedAccessException("This operation requires a moderator.");
    }
}
