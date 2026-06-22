using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

public class ServerStoryReadService(
    ReadOnlyApplicationDbContext readDb,
    IActiveUserContext activeUser,
    ISpriteReadService spriteReadService) : IStoryReadService
{
    public async Task<StoryDetailsDTO?> GetStoryByIdAsync(int storyId)
    {
        return await readDb.Stories.Where(s => s.StoryId == storyId)
            .Select(s => new StoryDetailsDTO
            {
                StoryId = s.StoryId,
                StoryTitle = s.StoryListing != null ? s.StoryListing.StoryTitle : string.Empty,
                ShortDescription = s.StoryListing != null ? s.StoryListing.ShortDescription ?? string.Empty : string.Empty,
                LongDescription = s.StoryDetail.LongDescription ?? string.Empty,
                WordCount = s.WordCount,
                PublishDate = s.PublishedDate,
                LastUpdatedDate = s.LastUpdatedDate,
                OriginalPublishDate = s.OriginalPublishedDate,
                OriginalLastUpdatedDate = s.OriginalLastUpdatedDate,
                ChapterNames = s.Chapters.Select(c => c.Title).ToList(),
                AuthorName = s.Author != null ? s.Author.UserName : "Unknown"
            })
            .FirstOrDefaultAsync();
    }

    public async Task<StoryUpdateDTO?> GetStoryForEditAsync(int storyId)
    {
        return await readDb.Stories // Using a direct projection for optimal query generation
            .Where(s => s.StoryId == storyId)
            .Select(s => new StoryUpdateDTO
            {
                StoryId = s.StoryId,
                Title = s.StoryListing != null ? s.StoryListing.StoryTitle : string.Empty,
                ShortDescription = s.StoryListing != null ? s.StoryListing.ShortDescription : null,
                Rating = s.Rating,
                StoryStatusId = s.StoryStatusId,
                CoverArtRelativeUrl = s.StoryListing != null ? s.StoryListing.CoverArtRelativeUrl : null,
                LongDescription = s.StoryDetail != null ? s.StoryDetail.LongDescription : null,
                PostApprovalStatus = s.StoryDetail != null ? s.StoryDetail.PostApprovalStatus : default,
                StoryTags = s.StoryTags.Select(st => new StoryTagDTO { TagId = st.TagId, Priority = st.Priority, TagTypeEnum = st.Tag.TagTypeId }).ToList<IStoryTag>()
            })
            .FirstOrDefaultAsync();
    }

    public async Task<StoryListingDto[]> GetListingsByIdsAsync(IReadOnlyList<int> storyIds)
    {
        if (storyIds.Count == 0) return [];

        List<StoryListingRow> rows = await ProjectListingRows(readDb.Stories.Where(s => storyIds.Contains(s.StoryId)))
            .ToListAsync();

        // Reorder to match the caller's id order (spec §6.6 — the domain service owns "which ids and in
        // what order"; this is purely the presentation lookup). IDs the content-rating filter dropped,
        // or that simply don't exist, are silently skipped.
        Dictionary<int, StoryListingRow> byId = rows.ToDictionary(r => r.StoryId);
        return storyIds
            .Where(byId.ContainsKey)
            .Select(id => ToDto(byId[id]))
            .ToArray();
    }

    public async Task<(StoryListingDto[] Items, int TotalCount)> GetRecentListingsAsync(int page, int pageSize)
    {
        int totalCount = await readDb.Stories.CountAsync();

        // Page on scalar StoryId first — keeps Skip/Take scoped to story-level rows, not a join cartesian
        // product, then hands off to GetListingsByIdsAsync for the actual presentation projection (the
        // same domain-ids-then-presentation-DTOs composition as the spec §6.6 building-block pattern).
        int[] pageStoryIds = await readDb.Stories
            .OrderByDescending(s => s.LastUpdatedDate)
            .Skip(Math.Max(0, page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => s.StoryId)
            .ToArrayAsync();

        StoryListingDto[] items = await GetListingsByIdsAsync(pageStoryIds);
        return (items, totalCount);
    }

    // Lean intermediate projection — ISpriteReadService.GetSpriteUrl is plain C# string-building, not
    // SQL-translatable, so tag sprites are resolved in-memory after materialization, mirroring the WU11
    // ServerTagReadService.SearchTagChipsAsync pattern (layer2-services.md "Per-keystroke typeahead...").
    private static IQueryable<StoryListingRow> ProjectListingRows(IQueryable<Story> query) =>
        query.Select(s => new StoryListingRow(
            s.StoryId,
            s.StoryListing != null ? s.StoryListing.StoryTitle : string.Empty,
            s.StoryListing != null ? s.StoryListing.CoverArtRelativeUrl : null,
            s.AuthorId,
            s.Author != null ? s.Author.UserName : null,
            s.WordCount,
            s.StoryStatusId,
            s.Rating,
            s.LastUpdatedDate,
            s.StoryTags.Select(st => new TagListingRow(
                st.TagId, st.Tag.TagName, st.Tag.TagTypeId, st.Tag.Description, st.Tag.SpriteIdentifier)).ToList()));

    private StoryListingDto ToDto(StoryListingRow row) => new(
        row.StoryId,
        row.Title,
        row.CoverArtRelativeUrl,
        row.AuthorId,
        row.AuthorName ?? "Unknown",
        row.WordCount,
        row.StoryStatusId,
        row.Rating,
        row.LastUpdatedDate,
        row.Tags.Select(ToTagChip).ToList());

    private TagChipDto ToTagChip(TagListingRow tag) => new()
    {
        TagId = tag.TagId,
        TagName = tag.TagName,
        TagTypeId = tag.TagTypeId,
        Description = tag.Description,
        SpriteUrl = tag.SpriteIdentifier is null
            ? null
            : spriteReadService.GetSpriteUrl(activeUser.Theme, tag.SpriteIdentifier, activeUser.PrefersAnimatedSprites)
    };

    private sealed record StoryListingRow(
        int StoryId, string Title, string? CoverArtRelativeUrl, int? AuthorId, string? AuthorName,
        int WordCount, StoryStatusEnum StoryStatusId, Rating Rating, DateTime LastUpdatedDate,
        List<TagListingRow> Tags);

    private sealed record TagListingRow(
        int TagId, string TagName, TagTypeEnum TagTypeId, string? Description, string? SpriteIdentifier);
}
