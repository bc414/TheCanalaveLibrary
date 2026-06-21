using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

public class ServerTagReadService(ReadOnlyApplicationDbContext readDb) : ITagReadService
{
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
