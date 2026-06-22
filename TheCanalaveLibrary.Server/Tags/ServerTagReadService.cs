using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

public class ServerTagReadService(
    ReadOnlyApplicationDbContext readDb,
    ISpriteReadService spriteReadService,
    IActiveUserContext activeUser) : ITagReadService
{
    private const int MaxSearchResults = 10;

    public async Task<List<TagChipDto>> SearchTagChipsAsync(TagTypeEnum type, string term)
    {
        if (string.IsNullOrWhiteSpace(term)) return [];

        var rows = await readDb.Tags
            .Where(t => t.TagTypeId == type && EF.Functions.ILike(t.TagName, $"%{term}%"))
            .OrderBy(t => t.TagName)
            .Take(MaxSearchResults)
            .Select(t => new { t.TagId, t.TagName, t.TagTypeId, t.Description, t.SpriteIdentifier })
            .ToListAsync();

        // IActiveUserContext (minted WU12) replaces the WU4/WU11-era "pokemon"/non-animated literal
        // placeholder — this resolves the real signed-in viewer's theme/animation preference now.
        return rows.Select(t => new TagChipDto
        {
            TagId = t.TagId,
            TagName = t.TagName,
            TagTypeId = t.TagTypeId,
            Description = t.Description,
            SpriteUrl = t.SpriteIdentifier is null
                ? null
                : spriteReadService.GetSpriteUrl(activeUser.Theme, t.SpriteIdentifier, activeUser.PrefersAnimatedSprites)
        }).ToList();
    }

    public Task<List<TagDropDownDTO>> GetTagsByTypeAsync(TagTypeEnum type) =>
        readDb.Tags
            .Where(t => t.TagTypeId == type)
            .OrderBy(t => t.TagName)
            .Select(t => new TagDropDownDTO { TagId = t.TagId, TagName = t.TagName })
            .ToListAsync();

    public Task<List<TagDropDownDTO>> GetAllCharacterTagsAsync() =>
        GetTagsByTypeAsync(TagTypeEnum.Character);

    public Task<List<TagDropDownDTO>> GetAllSettingTagsAsync() =>
        GetTagsByTypeAsync(TagTypeEnum.Setting);

    public Task<List<TagDropDownDTO>> GetAllGenreTagsAsync() =>
        GetTagsByTypeAsync(TagTypeEnum.Genre);

    public Task<List<TagDropDownDTO>> GetAllContentWarningTagsAsync() =>
        GetTagsByTypeAsync(TagTypeEnum.ContentWarning);
}
