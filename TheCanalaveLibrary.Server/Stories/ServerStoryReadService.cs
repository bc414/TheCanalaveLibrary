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
        // Two-step: project a lean intermediate row (EF-translatable) then resolve sprites in memory.
        // ISpriteReadService.GetSpriteUrl is plain C# string-building (not SQL-translatable) — same
        // pattern as ProjectListingRows / ToDto used for listing queries (layer2-services.md
        // §"Sprite URLs Are Resolved Server-Side, At Projection Time").
        StoryDetailRow? row = await readDb.Stories
            .Where(s => s.StoryId == storyId)
            .Select(s => new StoryDetailRow(
                s.StoryId,
                s.StoryListing != null ? s.StoryListing.StoryTitle : string.Empty,
                s.StoryListing != null ? s.StoryListing.ShortDescription ?? string.Empty : string.Empty,
                s.StoryDetail != null ? s.StoryDetail.LongDescription ?? string.Empty : string.Empty,
                s.WordCount,
                s.PublishedDate,
                s.LastUpdatedDate,
                s.OriginalPublishedDate,
                s.OriginalLastUpdatedDate,
                s.AuthorId,
                s.Author != null ? s.Author.UserName : null,
                s.StoryListing != null ? s.StoryListing.CoverArtRelativeUrl : null,
                s.Rating,
                s.StoryStatusId,
                s.Chapters.Select(c => c.Title).ToList(),
                s.StoryTags
                    .Select(st => new TagListingRow(
                        st.TagId, st.Tag.TagName, st.Tag.TagTypeId,
                        st.Tag.Description, st.Tag.SpriteIdentifier))
                    .ToList()))
            .FirstOrDefaultAsync();

        if (row is null) return null;

        return new StoryDetailsDTO
        {
            StoryId              = row.StoryId,
            StoryTitle           = row.Title,
            ShortDescription     = row.ShortDescription,
            LongDescription      = row.LongDescription,
            WordCount            = row.WordCount,
            PublishDate          = row.PublishDate,
            LastUpdatedDate      = row.LastUpdatedDate,
            OriginalPublishDate  = row.OriginalPublishDate,
            OriginalLastUpdatedDate = row.OriginalLastUpdatedDate,
            AuthorId             = row.AuthorId,
            AuthorName           = row.AuthorName ?? "Unknown",
            CoverArtRelativeUrl  = row.CoverArtRelativeUrl,
            Rating               = row.Rating,
            Status               = row.Status,
            ChapterNames         = row.ChapterNames,
            Tags                 = row.Tags.Select(ToTagChip).ToList()
        };
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

    public async Task<IReadOnlyList<int>> GetStoryIdsByAuthorAsync(int authorId) =>
        await readDb.Stories
            .IgnoreQueryFilters(["ContentRating"])
            .Where(s => s.AuthorId == authorId)
            .Select(s => s.StoryId)
            .ToListAsync();

    public async Task<(StoryListingDto[] Items, int TotalCount)> GetListingsAsync(
        StoryFilterDto filter, IReadOnlyCollection<int>? restrictToStoryIds = null)
    {
        IQueryable<Story> query = readDb.Stories;

        // ── Bookshelf candidate narrowing (applied first so count + all filters are scoped to it) ──
        if (restrictToStoryIds is { Count: > 0 })
            query = query.Where(s => restrictToStoryIds.Contains(s.StoryId));

        bool hasFts = !string.IsNullOrWhiteSpace(filter.TextQuery);
        query = ApplyFilters(query, filter, hasFts);

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

    public async Task<StoryListingDto[]> GetRandomBatchAsync(StoryFilterDto filter, int batchSize)
    {
        // Plain random draw from the post-filter valid set. No Sort/Page/PageSize from the DTO is
        // consulted — batchSize is the only take-cap and EF.Functions.Random() is the only order.
        // No shown-id tracking; "give me more" is a second call that appends a fresh independent draw.
        IQueryable<Story> query = ApplyFilters(readDb.Stories, filter, !string.IsNullOrWhiteSpace(filter.TextQuery));

        int[] ids = await query
            .OrderBy(_ => EF.Functions.Random())
            .Take(batchSize)
            .Select(s => s.StoryId)
            .ToArrayAsync();

        return await GetListingsByIdsAsync(ids);
    }

    /// <summary>
    /// Shared filter-building helper used by both <see cref="GetListingsAsync"/> and
    /// <see cref="GetRandomBatchAsync"/>. Applies tag include (AND or OR per <c>filter.IncludeMode</c>),
    /// tag exclude (ANY/none), FTS, and viewer-relative interaction exclusions. Does NOT add OrderBy
    /// or pagination — those are the caller's responsibility.
    /// </summary>
    private IQueryable<Story> ApplyFilters(IQueryable<Story> query, StoryFilterDto filter, bool hasFts)
    {
        // ── Tag include ────────────────────────────────────────────────────────────────────
        if (filter.IncludedTagIds.Count > 0)
        {
            if (filter.IncludeMode == TagIncludeMode.Or)
            {
                // OR — story must have at least one of the included tags.
                // Single WHERE EXISTS IN (...) translates cleanly to SQL.
                query = query.Where(s => s.StoryTags.Any(st => filter.IncludedTagIds.Contains(st.TagId)));
            }
            else
            {
                // AND (default) — story must have all of the included tags.
                // One subquery per tag; Postgres folds the constants away efficiently.
                foreach (int tagId in filter.IncludedTagIds)
                {
                    int tid = tagId; // local capture so the closure binds the right value
                    query = query.Where(s => s.StoryTags.Any(st => st.TagId == tid));
                }
            }
        }

        // ── Tag exclude (story must have none of the excluded tags) ───────────────────────
        if (filter.ExcludedTagIds.Count > 0)
        {
            // EF translates IReadOnlyList.Contains(entity_field) to SQL IN (...) correctly.
            query = query.Where(s => !s.StoryTags.Any(st => filter.ExcludedTagIds.Contains(st.TagId)));
        }

        // ── FTS ───────────────────────────────────────────────────────────────────────────
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

        return query;
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

    /// <summary>
    /// Intermediate row for <see cref="GetStoryByIdAsync"/> — holds raw DB scalars so sprite
    /// resolution can happen in memory after materialization (ISpriteReadService is not EF-translatable).
    /// </summary>
    private sealed record StoryDetailRow(
        int StoryId, string Title, string ShortDescription, string LongDescription,
        int WordCount, DateTime PublishDate, DateTime LastUpdatedDate,
        DateOnly? OriginalPublishDate, DateOnly? OriginalLastUpdatedDate,
        int? AuthorId, string? AuthorName, string? CoverArtRelativeUrl,
        Rating Rating, StoryStatusEnum Status, List<string> ChapterNames, List<TagListingRow> Tags);

    private sealed record TagListingRow(
        int TagId, string TagName, TagTypeEnum TagTypeId, string? Description, string? SpriteIdentifier);
}
