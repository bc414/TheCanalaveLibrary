using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server read implementation for the SiteSettings cluster. Factory-per-method read context
/// (layer2-services.md §"Read-Context Concurrency"); no caching by design — single-row PK
/// lookups, and mod edits must take effect on the next read.
/// </summary>
public class ServerSiteSettingsReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory) : ISiteSettingsReadService
{
    /// <summary>Exposed for the derived write service (CS9107 double-capture pattern).</summary>
    protected IDbContextFactory<ReadOnlyApplicationDbContext> ReadDbFactory { get; } = readDbFactory;

    public async Task<int> GetIntAsync(string settingKey, int fallback)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        string? raw = await readDb.SiteSettings
            .Where(s => s.SettingKey == settingKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        return raw is not null && int.TryParse(raw, out int parsed) ? parsed : fallback;
    }
}
