using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

// ISpriteReadService is intentionally NOT injected here — sprite URL resolution moved into render
// components (CharacterEntry, TagChip) that receive a ThemeContext cascading value. TagChipDto now
// carries the raw SpriteIdentifier key. See layer2-services.md §"Sprite URLs Are Resolved At Render Time."
public class ServerStoryReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser) : IStoryReadService
{
    /// <summary>
    /// Exposed as a protected property so derived write services can access the user context
    /// without double-capturing the constructor parameter (eliminates CS9107/CS9124 warnings).
    /// </summary>
    protected IActiveUserContext ActiveUser { get; } = activeUser;

    public async Task<StoryDetailsDTO?> GetStoryByIdAsync(int storyId)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

        // Reveal-aware (WU-AccessGate, Direct-navigation plane): a per-story consent or a
        // verified crawler lifts the rating filter for THIS read only. IsTakenDown stays active.
        IQueryable<Story> stories = readDb.Stories;
        if (ActiveUser.IsVerifiedBot
            || await RevealCheck.IsRevealedAsync(readDb, ActiveUser, RevealedEntityType.Story, storyId))
        {
            // elevated read: per-story consent (or Pattern-B verified crawler) on the detail path
            stories = stories.IgnoreQueryFilters(["ContentRating"]);
        }

        // Two-step: project a lean intermediate row (EF-translatable) then build DTOs in memory.
        // SpriteIdentifier is passed through raw — no URL construction here.
        StoryDetailRow? row = await stories
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
                s.StoryDetail != null ? s.StoryDetail.Slug : null,
                s.StoryTags
                    .Select(st => new TagListingRow(
                        st.TagId, st.Tag.TagName, st.Tag.TagTypeId,
                        st.Tag.Description, st.Tag.SpriteIdentifier))
                    .ToList(),
                s.StoryCharacters
                    .Select(sc => new CharacterDetailRow(
                        sc.CharacterTagId, sc.CharacterTag.TagName, sc.CharacterTag.SpriteIdentifier,
                        sc.Priority, sc.IsOc, sc.OcName, sc.OcBio))
                    .ToList(),
                s.StoryCharacterPairings
                    .Select(scp => new PairingDetailRow(
                        scp.PairingType, scp.Priority,
                        scp.Members.Select(m => m.StoryCharacter.CharacterTag.TagName).ToList()))
                    .ToList(),
                s.ExternalLinks
                    .OrderBy(el => el.ExternalPlatformId)
                    .Select(el => new StoryExternalLinkDto(
                        el.ExternalPlatform.Name,
                        el.Url,
                        el.VerificationStatus == VerificationStatusEnum.Verified))
                    .ToList()))
            .FirstOrDefaultAsync();

        if (row is null) return null;

        List<TagChipDto> characterChips = row.Characters
            .Select(c => new TagChipDto
            {
                TagId = c.CharacterTagId, TagName = c.TagName,
                TagTypeId = TagTypeEnum.Character,
                SpriteIdentifier = c.SpriteIdentifier   // raw key; component resolves via ThemeContext
            })
            .ToList();

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
            Slug                 = row.Slug,
            Tags                 = [..row.Tags.Select(ToTagChip), ..characterChips],
            Characters           = row.Characters
                .Select((c, i) => new CharacterDisplayEntry(characterChips[i], c.Priority, c.IsOc, c.OcName, c.OcBio))
                .ToList(),
            Pairings             = row.Pairings
                .Select(p => new PairingDisplayEntry(p.PairingType, p.Priority, p.MemberNames))
                .ToList(),
            ExternalLinks        = row.ExternalLinks
        };
    }

    public async Task<StoryUpdateDTO?> GetStoryForEditAsync(int storyId)
    {
        // Author gate (endpoint-authz sweep 2026-07-18): this feeds the author-only StoryEditorPage,
        // so ownership is enforced here — the page's own AuthorId comparison is affordance, not a
        // control (identity-and-authorization.md §"Security vs affordance"). Mirrors
        // GetChapterForEditAsync: project the owner alongside the DTO, then compare after load.
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        var row = await readDb.Stories // Using a direct projection for optimal query generation
            // elevated read: an author edits their own story regardless of their personal rating
            // setting — the mature-off-author lockout was bug B5 (WU-AccessGate Phase 1). Safe:
            // the ownership comparison below is the gate; non-authors get null either way.
            .IgnoreQueryFilters(["ContentRating"])
            .Where(s => s.StoryId == storyId)
            .Select(s => new
            {
                s.AuthorId,
                Dto = new StoryUpdateDTO
                {
                StoryId = s.StoryId,
                Title = s.StoryListing != null ? s.StoryListing.StoryTitle : string.Empty,
                ShortDescription = s.StoryListing != null ? s.StoryListing.ShortDescription : null,
                Rating = s.Rating,
                StoryStatusId = s.StoryStatusId,
                CoverArtRelativeUrl = s.StoryListing != null ? s.StoryListing.CoverArtRelativeUrl : null,
                LongDescription = s.StoryDetail != null ? s.StoryDetail.LongDescription : null,
                PostApprovalStatus = s.StoryDetail != null ? s.StoryDetail.PostApprovalStatus : default,
                StoryTags = s.StoryTags.Select(st => new StoryTagDTO { TagId = st.TagId, Priority = st.Priority, TagTypeEnum = st.Tag.TagTypeId }).ToList<IStoryTag>(),
                StoryCharacters = s.StoryCharacters.Select(sc => new StoryCharacterDto
                {
                    CharacterTagId = sc.CharacterTagId,
                    Priority       = sc.Priority,
                    IsOc           = sc.IsOc,
                    OcName         = sc.OcName,
                    OcBio          = sc.OcBio
                }).ToList(),
                SettingDetails = s.SettingDetails.Select(sd => new SettingDetailDto
                {
                    BaseTagId   = sd.BaseTagId,
                    Name        = sd.Name,
                    Description = sd.Description
                }).ToList(),
                StoryCharacterPairings = s.StoryCharacterPairings.Select(scp => new StoryCharacterPairingDto
                {
                    PairingType           = scp.PairingType,
                    Priority              = scp.Priority,
                    MemberCharacterTagIds = scp.Members.Select(m => m.StoryCharacter.CharacterTagId).ToList()
                }).ToList(),
                ExternalLinks = s.ExternalLinks
                    .OrderBy(el => el.ExternalPlatformId)
                    .Select(el => new StoryExternalLinkEditDto
                    {
                        ExternalPlatformId = el.ExternalPlatformId,
                        Url                = el.Url
                    }).ToList(),
                OriginalPublishedDate   = s.OriginalPublishedDate,
                OriginalLastUpdatedDate = s.OriginalLastUpdatedDate
                }
            })
            .FirstOrDefaultAsync();

        if (row is null) return null;
        // Explicit-auth check rather than nullable comparison: an anonymous viewer (UserId null)
        // against an authorless story (AuthorId null) must not pass on null == null.
        if (ActiveUser.UserId is not int viewerId || row.AuthorId != viewerId)
            throw new UnauthorizedAccessException("You must be the author of this story.");

        return row.Dto;
    }

    public async Task<IReadOnlyList<ExternalPlatformDto>> GetExternalPlatformsAsync()
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        return await readDb.ExternalPlatforms
            .OrderBy(p => p.ExternalPlatformId)
            .Select(p => new ExternalPlatformDto(p.ExternalPlatformId, p.Name, p.DomainPattern))
            .ToListAsync();
    }

    public async Task<StoryListingDto[]> GetListingsByIdsAsync(IReadOnlyList<int> storyIds)
    {
        if (storyIds.Count == 0) return [];
        return await GetListingsByIdsCoreAsync(storyIds, personalScope: false);
    }

    // Shared hydration for GetListingsByIdsAsync (Discovery-plane, filtered) and
    // GetListingsAsync's Personal-plane path (WU-AccessGate: owner interaction-backed candidate
    // sets hydrate unfiltered so a viewer's own M favorites/history stay visible and manageable).
    private async Task<StoryListingDto[]> GetListingsByIdsCoreAsync(IReadOnlyList<int> storyIds, bool personalScope)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

        IQueryable<Story> root = readDb.Stories.Where(s => storyIds.Contains(s.StoryId));
        if (personalScope)
            root = root.IgnoreQueryFilters(["ContentRating"]); // elevated read: Personal plane (own interaction graph)

        List<StoryListingRow> rows = await ProjectListingRows(root).ToListAsync();

        // Reorder to match the caller's id order (spec §6.6 — the domain service owns "which ids and in
        // what order"; this is purely the presentation lookup). IDs the content-rating filter dropped,
        // or that simply don't exist, are silently skipped.
        Dictionary<int, StoryListingRow> byId = rows.ToDictionary(r => r.StoryId);
        return storyIds
            .Where(byId.ContainsKey)
            .Select(id => ToDto(byId[id]))
            .ToArray();
    }

    public async Task<IReadOnlyList<GatedMetadataDto>> GetGatedCardsAsync(IReadOnlyCollection<int> storyIds)
    {
        if (storyIds.Count == 0 || ActiveUser.MaxRating >= Rating.M) return [];

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

        // elevated read: mature count-line disclosure — acknowledges the M items a
        // person/collection-scoped listing hid (title/author/rating only; IsTakenDown stays
        // active). See content-safety.md §"Person/collection-scoped listings".
        Rating ceiling = ActiveUser.MaxRating;
        return await readDb.Stories
            .IgnoreQueryFilters(["ContentRating"])
            .Where(s => storyIds.Contains(s.StoryId) && s.Rating > ceiling)
            .OrderByDescending(s => s.LastUpdatedDate)
            .Select(s => new GatedMetadataDto(
                RevealedEntityType.Story,
                s.StoryId,
                s.StoryListing != null ? s.StoryListing.StoryTitle : string.Empty,
                s.AuthorId,
                s.Author != null ? s.Author.UserName : null,
                s.Rating))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<GatedMetadataDto>> GetGatedStoriesByAuthorAsync(int authorId)
    {
        if (ActiveUser.MaxRating >= Rating.M || ActiveUser.UserId == authorId) return [];

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

        // Class-A: profile-tab data — respect the author's ProfileVisibility like the visible-id
        // read does.
        if (!await ProfileVisibilityGuard.IsProfileVisibleAsync(readDb, ActiveUser, authorId))
            return [];

        // elevated read: the authored tab's disclosure half (the visible-id read deliberately
        // never leaks rating-hidden ids cross-user; this supplies them as gated metadata instead
        // — the discovery bridge: acknowledge existence, withhold content).
        Rating ceiling = ActiveUser.MaxRating;
        return await readDb.Stories
            .IgnoreQueryFilters(["ContentRating"])
            .Where(s => s.AuthorId == authorId && s.Rating > ceiling)
            .OrderByDescending(s => s.LastUpdatedDate)
            .Select(s => new GatedMetadataDto(
                RevealedEntityType.Story,
                s.StoryId,
                s.StoryListing != null ? s.StoryListing.StoryTitle : string.Empty,
                s.AuthorId,
                s.Author != null ? s.Author.UserName : null,
                s.Rating))
            .ToListAsync();
    }

    public async Task<(StoryListingDto[] Items, int TotalCount)> GetRecentListingsAsync(int page, int pageSize)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
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

    public async Task<long> GetStoryTotalViewsAsync(int storyId)
    {
        // daily_story_stats is migration-managed raw DDL with no EF model (accumulated stat
        // table, not a mart) — read via SqlQuery; SUM(int) is bigint in Postgres, hence long.
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        return await readDb.Database
            .SqlQuery<long>($"""
                SELECT COALESCE(SUM(view_count), 0) AS "Value"
                FROM daily_story_stats
                WHERE story_id = {storyId}
                """)
            .SingleAsync();
    }

    public async Task<GatedMetadataDto?> GetStoryGateAsync(int storyId)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

        // elevated read: gated-existence metadata — ContentRating bypassed so the interstitial
        // can acknowledge the story exists; "IsTakenDown" stays ACTIVE so taken-down stories
        // remain a true 404 (takedown is Class-A enforcement, not consent). Only M stories gate;
        // a null here means genuinely absent → the page 404s. Metadata is title/author/rating
        // ONLY — no cover, no description (settled 2026-07-19; both can themselves be explicit).
        return await readDb.Stories
            .IgnoreQueryFilters(["ContentRating"])
            .Where(s => s.StoryId == storyId && s.Rating == Rating.M)
            .Select(s => new GatedMetadataDto(
                RevealedEntityType.Story,
                s.StoryId,
                s.StoryListing != null ? s.StoryListing.StoryTitle : string.Empty,
                s.AuthorId,
                s.Author != null ? s.Author.UserName : null,
                s.Rating))
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<int>> GetStoryIdsByAuthorAsync(int authorId)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

        // Class-A: an author's story list is profile-tab data; respect their ProfileVisibility
        // (WU-AccessGate Phase 1 — the endpoint is now anonymous-callable for the public tab).
        if (!await ProfileVisibilityGuard.IsProfileVisibleAsync(readDb, ActiveUser, authorId))
            return [];

        // Elevated read only for the author's own list (endpoint-authz sweep 2026-07-18): the
        // ContentRating bypass must never be keyed to a client-supplied id — any other viewer gets
        // the normally-filtered set, so rating-hidden story ids don't leak cross-user.
        IQueryable<Story> stories = readDb.Stories;
        if (ActiveUser.UserId == authorId)
            stories = stories.IgnoreQueryFilters(["ContentRating"]); // elevated read: author always sees their own stories regardless of rating setting
        return await stories
            .Where(s => s.AuthorId == authorId)
            .Select(s => s.StoryId)
            .ToListAsync();
    }

    private const int MaxTitleSearchResults = 10;

    public async Task<IReadOnlyList<StoryTitleSearchDto>> SearchStoriesByTitleAsync(string term)
    {
        if (string.IsNullOrWhiteSpace(term)) return [];

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        return await readDb.Stories
            .Where(s => s.StoryListing != null && EF.Functions.ILike(s.StoryListing.StoryTitle, $"%{term}%"))
            .OrderBy(s => s.StoryListing!.StoryTitle)
            .Take(MaxTitleSearchResults)
            .Select(s => new StoryTitleSearchDto(
                s.StoryId,
                s.StoryListing!.StoryTitle,
                s.Author != null ? s.Author.UserName : null))
            .ToListAsync();
    }

    public async Task<(StoryListingDto[] Items, int TotalCount)> GetListingsAsync(
        StoryFilterDto filter, IReadOnlyCollection<int>? restrictToStoryIds = null, bool personalScope = false)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        IQueryable<Story> query = readDb.Stories;

        // Personal plane (WU-AccessGate): a viewer's own interaction-backed candidate set is
        // never rating-filtered — see the interface doc. Only meaningful with a restrict set.
        bool personal = personalScope && restrictToStoryIds is { Count: > 0 };
        if (personal)
            query = query.IgnoreQueryFilters(["ContentRating"]); // elevated read: Personal plane (own interaction graph)

        // ── Bookshelf candidate narrowing (applied first so count + all filters are scoped to it) ──
        if (restrictToStoryIds is { Count: > 0 })
            query = query.Where(s => restrictToStoryIds.Contains(s.StoryId));

        bool hasFts = !string.IsNullOrWhiteSpace(filter.TextQuery);
        query = ApplyFilters(query, filter, hasFts);

        // ── Count (before Skip/Take so it reflects the full filtered set) ─────────────────
        int totalCount = await query.CountAsync();

        // ── Sort + scalar id page (OrderBy on entity fields — Npgsql trap: keep before Select) ──
        DefaultSortOrder effectiveSort = filter.Sort switch
        {
            DefaultSortOrder.Relevance when !hasFts => DefaultSortOrder.DatePublished,
            // Viewer-relative sort needs a viewer (Bookshelves is [Authorize], so only misuse hits this).
            DefaultSortOrder.RecentlyRead when ActiveUser.UserId is null => DefaultSortOrder.DatePublished,
            _ => filter.Sort
        };
        int viewerId = ActiveUser.UserId ?? 0; // only consumed by the RecentlyRead branch (guarded above)

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

            // Most-recently-read first: viewer's MAX(UserChapterInteraction.LastInteractionDate)
            // across the story's chapters. Never-pinged stories sort last via the explicit Any()
            // first key (R5: Postgres DESC would otherwise put the NULL Max rows FIRST).
            DefaultSortOrder.RecentlyRead => await query
                .OrderByDescending(s => s.Chapters
                    .SelectMany(c => c.UserChapterInteractions)
                    .Any(u => u.UserId == viewerId))
                .ThenByDescending(s => s.Chapters
                    .SelectMany(c => c.UserChapterInteractions)
                    .Where(u => u.UserId == viewerId)
                    .Max(u => (DateTime?)u.LastInteractionDate))
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

        StoryListingDto[] items = await GetListingsByIdsCoreAsync(pageIds, personal);
        return (items, totalCount);
    }

    public async Task<IReadOnlyList<int>> FilterCandidateIdsAsync(
        IReadOnlyCollection<int> candidateIds, StoryFilterDto filter)
    {
        if (candidateIds.Count == 0) return [];

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        IQueryable<Story> query = readDb.Stories.Where(s => candidateIds.Contains(s.StoryId));
        query = ApplyFilters(query, filter, !string.IsNullOrWhiteSpace(filter.TextQuery));

        return await query.Select(s => s.StoryId).ToListAsync();
    }

    public async Task<StoryListingDto[]> GetRandomBatchAsync(StoryFilterDto filter, int batchSize)
    {
        // Plain random draw from the post-filter valid set. No Sort/Page/PageSize from the DTO is
        // consulted — batchSize is the only take-cap and EF.Functions.Random() is the only order.
        // No shown-id tracking; "give me more" is a second call that appends a fresh independent draw.
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
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
        // Character tags live in StoryCharacters; all others live in StoryTags. Since every
        // TagId belongs to exactly one entity type, the || always resolves to one side only —
        // this is correct without pre-partitioning the id list.
        if (filter.IncludedTagIds.Count > 0)
        {
            if (filter.IncludeMode == TagIncludeMode.Or)
            {
                // OR — story must match at least one included tag in either collection.
                query = query.Where(s =>
                    s.StoryCharacters.Any(sc => filter.IncludedTagIds.Contains(sc.CharacterTagId)) ||
                    s.StoryTags.Any(st => filter.IncludedTagIds.Contains(st.TagId)));
            }
            else
            {
                // AND (default) — story must match every included tag; each gets one subquery.
                foreach (int tagId in filter.IncludedTagIds)
                {
                    int tid = tagId; // local capture so the closure binds the right value
                    query = query.Where(s =>
                        s.StoryCharacters.Any(sc => sc.CharacterTagId == tid) ||
                        s.StoryTags.Any(st => st.TagId == tid));
                }
            }
        }

        // ── Tag exclude (story must have none of the excluded tags in either collection) ──
        if (filter.ExcludedTagIds.Count > 0)
        {
            query = query.Where(s =>
                !s.StoryCharacters.Any(sc => filter.ExcludedTagIds.Contains(sc.CharacterTagId)) &&
                !s.StoryTags.Any(st => filter.ExcludedTagIds.Contains(st.TagId)));
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
        if (filter.ExcludedInteractions.Count > 0 && ActiveUser.UserId.HasValue)
        {
            int viewerId = ActiveUser.UserId.Value;

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

    // Lean intermediate projection — SpriteIdentifier is passed through raw (no resolution here).
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

    private static TagChipDto ToTagChip(TagListingRow tag) => new()
    {
        TagId = tag.TagId,
        TagName = tag.TagName,
        TagTypeId = tag.TagTypeId,
        Description = tag.Description,
        SpriteIdentifier = tag.SpriteIdentifier  // raw key; component resolves via ThemeContext
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
        Rating Rating, StoryStatusEnum Status,
        string? Slug,
        List<TagListingRow> Tags,
        List<CharacterDetailRow> Characters,
        List<PairingDetailRow> Pairings,
        List<StoryExternalLinkDto> ExternalLinks);

    private sealed record TagListingRow(
        int TagId, string TagName, TagTypeEnum TagTypeId, string? Description, string? SpriteIdentifier);

    private sealed record CharacterDetailRow(
        int CharacterTagId, string TagName, string? SpriteIdentifier,
        TagPriority Priority, bool IsOc, string? OcName, string? OcBio);

    private sealed record PairingDetailRow(
        CharacterPairingType PairingType, TagPriority Priority, List<string> MemberNames);
}
