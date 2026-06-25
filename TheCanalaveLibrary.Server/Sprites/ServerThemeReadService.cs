using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Returns the seeded theme list for the Appearance settings control (Feature 3, WU30).
/// Themes are small and change only via migrations, so the full list is projected in one query.
/// <c>PreviewColor</c> is projected as <c>null</c> — the <c>Theme</c> entity has no color column yet;
/// add it when the swatch design is finalised.
/// </summary>
public class ServerThemeReadService(ReadOnlyApplicationDbContext readDb) : IThemeReadService
{
    public async Task<IReadOnlyList<ThemeDto>> GetThemesAsync()
    {
        return await readDb.Themes
            .OrderBy(t => t.ThemeId)
            .Select(t => new ThemeDto(t.ThemeId, t.Name, null))
            .ToListAsync();
    }
}
