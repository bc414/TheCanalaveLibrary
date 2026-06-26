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

    public async Task<List<TagChipDto>> GetTagChipsByIdsAsync(IReadOnlyList<int> tagIds)
    {
        if (tagIds.Count == 0) return [];

        var rows = await readDb.Tags
            .Where(t => tagIds.Contains(t.TagId))
            .Select(t => new { t.TagId, t.TagName, t.TagTypeId, t.Description, t.SpriteIdentifier, t.AllowOCDetails, t.AllowSettingDetails })
            .ToListAsync();

        // Reorder to match the caller's id order (same reorder-to-input convention as GetListingsByIdsAsync).
        Dictionary<int, TagChipDto> byId = rows.ToDictionary(t => t.TagId, t => new TagChipDto
        {
            TagId = t.TagId,
            TagName = t.TagName,
            TagTypeId = t.TagTypeId,
            Description = t.Description,
            SpriteUrl = t.SpriteIdentifier is null
                ? null
                : spriteReadService.GetSpriteUrl(activeUser.Theme, t.SpriteIdentifier, activeUser.PrefersAnimatedSprites),
            AllowOCDetails = t.AllowOCDetails,
            AllowSettingDetails = t.AllowSettingDetails
        });

        return [.. tagIds.Where(byId.ContainsKey).Select(id => byId[id])];
    }

    public async Task<List<TagDirectoryGroupDto>> GetTagDirectoryAsync()
    {
        // Single projection — fetch all tags with ParentTagId so we can build the tree in memory.
        var rows = await readDb.Tags
            .OrderBy(t => t.TagName)
            .Select(t => new
            {
                t.TagId,
                t.TagName,
                t.TagTypeId,
                t.Description,
                t.SpriteIdentifier,
                t.ParentTagId,
                t.IsFanon,
                t.AllowOCDetails,
                t.AllowSettingDetails
            })
            .ToListAsync();

        // Resolve sprites post-materialization (theme/animated preference is per-viewer, not storable).
        var chips = rows.ToDictionary(t => t.TagId, t => new TagChipDto
        {
            TagId = t.TagId,
            TagName = t.TagName,
            TagTypeId = t.TagTypeId,
            Description = t.Description,
            SpriteUrl = t.SpriteIdentifier is null
                ? null
                : spriteReadService.GetSpriteUrl(activeUser.Theme, t.SpriteIdentifier, activeUser.PrefersAnimatedSprites),
            // Admin fields — set here so the mod editor can pre-populate without a second round-trip.
            IsFanon = t.IsFanon,
            AllowOCDetails = t.AllowOCDetails,
            AllowSettingDetails = t.AllowSettingDetails,
            ParentTagId = t.ParentTagId
        });

        // Build parent→children lookup (children already come out ordered by name from the query).
        var childrenByParent = rows
            .Where(t => t.ParentTagId is not null)
            .GroupBy(t => t.ParentTagId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TagChipDto>)[.. g.Select(t => chips[t.TagId])]);

        // Group top-level tags (no parent) by type, in enum declaration order.
        var topLevel = rows
            .Where(t => t.ParentTagId is null)
            .GroupBy(t => t.TagTypeId);

        List<TagDirectoryGroupDto> groups = [];
        foreach (TagTypeEnum type in Enum.GetValues<TagTypeEnum>())
        {
            var group = topLevel.FirstOrDefault(g => g.Key == type);
            var nodes = group is null
                ? (IReadOnlyList<TagDirectoryNodeDto>)[]
                : [.. group.Select(t => new TagDirectoryNodeDto
                {
                    Tag = chips[t.TagId],
                    Children = childrenByParent.GetValueOrDefault(t.TagId, [])
                })];

            groups.Add(new TagDirectoryGroupDto { TagType = type, Nodes = nodes });
        }

        return groups;
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
