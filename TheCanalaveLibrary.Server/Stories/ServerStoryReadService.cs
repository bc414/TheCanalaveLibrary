using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
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

    public async Task<(StoryListingDto[] Items, int TotalCount)> GetListingsAsync(StoryFilterDto filter)
    {
        IQueryable<Story> query = readDb.Stories;

        // ── Tag include (AND semantics: story must have all included tags) ─────────────────
        foreach (int tagId in filter.IncludedTagIds)
        {
            int tid = tagId; // local capture so the closure binds the right value
            query = query.Where(s => s.StoryTags.Any(st => st.TagId == tid));
        }

        // ── Tag exclude (story must have none of the excluded tags) ───────────────────────
        if (filter.ExcludedTagIds.Count > 0)
        {
            // EF translates IReadOnlyList.Contains(entity_field) to SQL IN (...) correctly.
            query = query.Where(s => !s.StoryTags.Any(st => filter.ExcludedTagIds.Contains(st.TagId)));
        }

        // ── FTS ───────────────────────────────────────────────────────────────────────────
        bool hasFts = !string.IsNullOrWhiteSpace(filter.TextQuery);
        if (hasFts)
        {
            string textQuery = filter.TextQuery!;
            // PlainToTsQuery is safer than ToTsQuery (no tsquery syntax knowledge required from callers).
            // SearchVector is a shadow property on StoryListing; EF.Property accesses it in a subquery.
            query = query.Where(s => s.StoryListing != null &&
                EF.Property<NpgsqlTsVector>(s.StoryListing, "SearchVector")
                    .Matches(EF.Functions.PlainToTsQuery("english", textQuery)));
        }

        // ── Interaction exclusions (authenticated viewer only) ────────────────────────────
        if (filter.ExcludedInteractions.Count > 0 && activeUser.UserId.HasValue)
        {
            int viewerId = activeUser.UserId.Value;

            bool exclFav    = filter.ExcludedInteractions.Contains(UserStoryInteractionTypeEnum.Favorite);
            bool exclHidden = filter.ExcludedInteractions.Contains(UserStoryInteractionTypeEnum.PrivateFavorite);
            bool exclFollow = filter.ExcludedInteractions.Contains(UserStoryInteractionTypeEnum.Follow);
            bool exclComp   = filter.ExcludedInteractions.Contains(UserStoryInteractionTypeEnum.Complete);
            bool exclLater  = filter.ExcludedInteractions.Contains(UserStoryInteractionTypeEnum.ReadLater);
            bool exclIgnore = filter.ExcludedInteractions.Contains(UserStoryInteractionTypeEnum.Ignore);

            // Exclude stories where the viewer's USI row has any excluded bit set.
            // The constants (exclFav, etc.) are evaluated at query-compilation time and fold into the
            // SQL as literal true/false, which Postgres optimises away. Zero SQL overhead for bits that
            // aren't excluded.
            query = query.Where(s => !s.UserStoryInteractions
                .Any(usi => usi.UserId == viewerId &&
                    (exclFav    && usi.IsFavorite      ||
                     exclHidden && usi.IsHiddenFavorite ||
                     exclFollow && usi.IsFollowed       ||
                     exclComp   && usi.IsCompleted      ||
                     exclLater  && usi.IsReadItLater    ||
                     exclIgnore && usi.IsIgnored)));
        }

        // ── Count (before Skip/Take so it reflects the full filtered set) ─────────────────
        int totalCount = await query.CountAsync();

        // ── Sort + scalar id page (OrderBy on entity fields — Npgsql trap: keep before Select) ──
        DefaultSortOrder effectiveSort = filter.Sort == DefaultSortOrder.Relevance && !hasFts
            ? DefaultSortOrder.DatePublished
            : filter.Sort;

        int[] pageIds = effectiveSort switch
        {
            DefaultSortOrder.Relevance => await query
                .OrderByDescending(s => EF.Property<NpgsqlTsVector>(s.StoryListing!, "SearchVector")
                    .Rank(EF.Functions.PlainToTsQuery("english", filter.TextQuery!)))
                .ThenByDescending(s => s.LastUpdatedDate)
                .Skip(Math.Max(0, filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(s => s.StoryId)
                .ToArrayAsync(),

            DefaultSortOrder.Random => await query
                .OrderBy(_ => EF.Functions.Random())
                .Skip(Math.Max(0, filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(s => s.StoryId)
                .ToArrayAsync(),

            _ /* DatePublished, Score-fallback */ => await query
                .OrderByDescending(s => s.PublishedDate)
                .Skip(Math.Max(0, filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(s => s.StoryId)
                .ToArrayAsync()
        };

        StoryListingDto[] items = await GetListingsByIdsAsync(pageIds);
        return (items, totalCount);
    }

    // Lean intermediate projection — ISpriteReadService.GetSpriteUrl is plain C# string-building, not
    // SQL-translatable, so tag sprites are resolved in-memory after materialization, mirroring the WU11
    // ServerTagReadService.SearchTagChipsAsync pattern (layer2-services.md "Per-keystroke typeahead...").
    private static IQueryable<StoryListingRow> ProjectListingRows(IQueryable<Story> query) =>
        query.Select(s => new StoryListingRow(
            s.StoryId,
            s.StoryListing != null ? s.StoryListing.StoryTitle : string.Empty,
            s.StoryListing != null ? s.StoryListing.ShortDescription : null,
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
        row.ShortDescription,
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
        int StoryId, string Title, string? ShortDescription, string? CoverArtRelativeUrl,
        int? AuthorId, string? AuthorName,
        int WordCount, StoryStatusEnum StoryStatusId, Rating Rating, DateTime LastUpdatedDate,
        List<TagListingRow> Tags);

    private sealed record TagListingRow(
        int TagId, string TagName, TagTypeEnum TagTypeId, string? Description, string? SpriteIdentifier);
}
