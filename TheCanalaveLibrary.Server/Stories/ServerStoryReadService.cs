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
                        st.Tag.Description,
                        st.Tag.SpriteIdentifier ?? (st.Tag.ParentTag != null ? st.Tag.ParentTag.SpriteIdentifier : null),
                        st.Tag.IsFanon, st.Tag.AllowCustomName,
                        st.Tag.ParentTagId,
                        st.Tag.ParentTag != null ? st.Tag.ParentTag.TagName : null,
                        st.CustomName, st.Nuance))
                    .ToList(),
                s.StoryCharacters
                    .Select(sc => new CharacterDetailRow(
                        sc.CharacterTagId, sc.CharacterTag.TagName,
                        sc.CharacterTag.SpriteIdentifier ?? (sc.CharacterTag.ParentTag != null ? sc.CharacterTag.ParentTag.SpriteIdentifier : null),
                        sc.CharacterTag.IsFanon,
                        sc.CharacterTag.ParentTagId,
                        sc.CharacterTag.ParentTag != null ? sc.CharacterTag.ParentTag.TagName : null,
                        sc.Priority, sc.IsOc, sc.CustomName, sc.Nuance))
                    .ToList(),
                s.StoryCharacterPairings
                    .Select(scp => new PairingDetailRow(
                        scp.PairingType, scp.Priority,
                        // Custom-named characters display under their per-story name; after a
                        // fanonize adoption the canonical tag name takes over (WU-TagFanon 8.6).
                        scp.Members.Select(m => m.StoryCharacter.CustomName ?? m.StoryCharacter.CharacterTag.TagName).ToList()))
                    .ToList(),
                s.ExternalLinks
                    .OrderBy(el => el.ExternalPlatformId)
                    .Select(el => new StoryExternalLinkDto(
                        el.ExternalPlatform.Name,
                        el.Url,
                        el.VerificationStatus == VerificationStatusEnum.Verified,
                        // WU39: the companion sub-line only appears once the per-link tier is
                        // reviewed AND the story's author still holds a Verified account-tier
                        // identity for that platform — a correlated lookup, not a stored copy, so
                        // an account later un-verified stops showing the handle automatically.
                        el.VerificationStatus == VerificationStatusEnum.Verified && s.Author != null
                            ? s.Author.UserExternalIdentities
                                .Where(i => i.ExternalPlatformId == el.ExternalPlatformId && i.VerificationStatus == VerificationStatusEnum.Verified)
                                .Select(i => i.Handle).FirstOrDefault()
                            : null,
                        el.VerificationStatus == VerificationStatusEnum.Verified && s.Author != null
                            ? s.Author.UserExternalIdentities
                                .Where(i => i.ExternalPlatformId == el.ExternalPlatformId && i.VerificationStatus == VerificationStatusEnum.Verified)
                                .Select(i => i.ProfileUrl).FirstOrDefault()
                            : null))
                    .ToList()))
            .FirstOrDefaultAsync();

        if (row is null) return null;

        List<TagChipDto> characterChips = row.Characters
            .Select(c => new TagChipDto
            {
                TagId = c.CharacterTagId, TagName = c.TagName,
                TagTypeId = TagTypeEnum.Character,
                SpriteIdentifier = c.SpriteIdentifier,  // raw key; component resolves via ThemeContext
                IsFanon = c.IsFanon,
                ParentTagId = c.ParentTagId,
                ParentTagName = c.ParentTagName,
                // Per-story overlay rides the chip so cards/story chips carry the indicator (WU-TagFanon 4.1).
                CustomName = c.CustomName,
                Nuance = c.Nuance
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
                .Select((c, i) => new CharacterDisplayEntry(characterChips[i], c.Priority, c.IsOc, c.CustomName, c.Nuance))
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
                // Same ordering as the Dto.StoryCharacters subquery — positional index source.
                CharacterRowIds = s.StoryCharacters.OrderBy(sc => sc.StoryCharacterId)
                    .Select(sc => sc.StoryCharacterId).ToList(),
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
                StoryTags = s.StoryTags.Select(st => new StoryTagDTO
                {
                    TagId = st.TagId, Priority = st.Priority, TagTypeEnum = st.Tag.TagTypeId,
                    CustomName = st.CustomName, Nuance = st.Nuance
                }).ToList<IStoryTag>(),
                // OrderBy keeps this list and CharacterRowIds below positionally aligned — the
                // index-based pairing translation depends on identical subquery ordering.
                StoryCharacters = s.StoryCharacters.OrderBy(sc => sc.StoryCharacterId).Select(sc => new StoryCharacterDto
                {
                    CharacterTagId = sc.CharacterTagId,
                    Priority       = sc.Priority,
                    IsOc           = sc.IsOc,
                    CustomName     = sc.CustomName,
                    Nuance         = sc.Nuance
                }).ToList(),
                StoryCharacterPairings = s.StoryCharacterPairings.Select(scp => new StoryCharacterPairingDto
                {
                    PairingType   = scp.PairingType,
                    Priority      = scp.Priority,
                    // Temporarily carries StoryCharacterIds; translated to list indexes below
                    // (the DTO contract is index-based — WU-TagFanon).
                    MemberIndexes = scp.Members.Select(m => m.StoryCharacterId).ToList()
                }).ToList(),
                ExternalLinks = s.ExternalLinks
                    .OrderBy(el => el.ExternalPlatformId)
                    .Select(el => new StoryExternalLinkEditDto
                    {
                        StoryExternalLinkId   = el.StoryExternalLinkId,
                        ExternalPlatformId    = el.ExternalPlatformId,
                        Url                   = el.Url,
                        VerificationStatus    = el.VerificationStatus,
                        VerificationRequested = el.DateVerificationRequested != null,
                        RejectionReason       = el.RejectionReason
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

        // Translate pairing member StoryCharacterIds (projected above) into indexes into the
        // DTO's StoryCharacters list — the wire contract is index-based (WU-TagFanon).
        Dictionary<int, int> indexByRowId = row.CharacterRowIds
            .Select((id, i) => (id, i))
            .ToDictionary(x => x.id, x => x.i);
        row.Dto.StoryCharacterPairings = row.Dto.StoryCharacterPairings
            .Select(p => new StoryCharacterPairingDto
            {
                PairingType   = p.PairingType,
                Priority      = p.Priority,
                MemberIndexes = p.MemberIndexes
                    .Where(indexByRowId.ContainsKey)
                    .Select(id => indexByRowId[id])
                    .ToList()
            })
            .ToList();

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

    public async Task<long> GetStoryTotalViewsAsync(int storyId)
    {
        // daily_story_stats is migration-managed raw DDL with no EF model (accumulated stat
        // table, not a mart) — read via SqlQuery; SUM(int) is bigint in Postgres, hence long.
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

        // Kind (g): raw SQL has no EF model and therefore no query filter of any kind, so this
        // confirmed the existence and popularity of hidden, draft and taken-down stories. Zero is
        // what a story with no recorded views returns, so the hidden case is indistinguishable.
        if (!await StoryVisibilityGuard.IsStoryVisibleAsync(readDb, ActiveUser, storyId))
            return 0;

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
        bool personal = personalScope && restrictToStoryIds is not null;
        if (personal)
            query = query.IgnoreQueryFilters(["ContentRating"]); // elevated read: Personal plane (own interaction graph)

        // ── Bookshelf candidate narrowing (applied first so count + all filters are scoped to it) ──
        // null = no narrowing (the /discover path); EMPTY = narrow to nothing. The empty case must
        // NOT fall through to an unnarrowed query — an empty shelf/profile tab renders empty, never
        // the whole library (browser-caught 2026-07-25; every bookshelf tab with zero candidates
        // was listing every story, and it silently undid WU-RecLifecycle's D1 filter on the profile
        // Recommendations tab).
        if (restrictToStoryIds is not null)
            query = query.Where(s => restrictToStoryIds.Contains(s.StoryId));

        bool hasFts = !string.IsNullOrWhiteSpace(filter.TextQuery);
        query = await ApplyFiltersAsync(readDb, query, filter, hasFts);

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
        query = await ApplyFiltersAsync(readDb, query, filter, !string.IsNullOrWhiteSpace(filter.TextQuery));

        return await query.Select(s => s.StoryId).ToListAsync();
    }

    public async Task<StoryListingDto[]> GetRandomBatchAsync(StoryFilterDto filter, int batchSize)
    {
        // Plain random draw from the post-filter valid set. No Sort/Page/PageSize from the DTO is
        // consulted — batchSize is the only take-cap and EF.Functions.Random() is the only order.
        // No shown-id tracking; "give me more" is a second call that appends a fresh independent draw.
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        IQueryable<Story> query = await ApplyFiltersAsync(readDb, readDb.Stories, filter, !string.IsNullOrWhiteSpace(filter.TextQuery));

        int[] ids = await query
            .OrderBy(_ => EF.Functions.Random())
            .Take(batchSize)
            .Select(s => s.StoryId)
            .ToArrayAsync();

        return await GetListingsByIdsAsync(ids);
    }

    /// <summary>
    /// Shared filter-building helper used by <see cref="GetListingsAsync"/>,
    /// <see cref="FilterCandidateIdsAsync"/> and <see cref="GetRandomBatchAsync"/>. Applies tag
    /// include (AND or OR per <c>filter.IncludeMode</c>), tag exclude (ANY/none), ship filters,
    /// FTS, and viewer-relative interaction exclusions. Does NOT add OrderBy or pagination —
    /// those are the caller's responsibility.
    ///
    /// <para><b>Hierarchy roll-up (WU-TagFanon):</b> every tag id — include, exclude, and ship
    /// member — expands to {self} ∪ children before the predicate builds (one lookup; hierarchy
    /// is one level deep). Symmetric: excluding a parent excludes its children. AND terms are
    /// independent: a story tagged only with a child satisfies a filter naming parent AND child.
    /// See layer2-services.md §"Tag Hierarchy Roll-Up".</para>
    /// </summary>
    private async Task<IQueryable<Story>> ApplyFiltersAsync(
        ReadOnlyApplicationDbContext readDb, IQueryable<Story> query, StoryFilterDto filter, bool hasFts)
    {
        // Shape validation up front, as a user-facing domain exception — NOT an ArgumentException
        // from deep inside predicate assembly, which would surface as a 500 for what is ordinary
        // bad input.
        ValidateShipShape(filter);

        // ── One child-expansion lookup covering every axis that names tag ids ─────────────
        Dictionary<int, int[]> expansion = await ExpandWithChildrenAsync(readDb,
        [
            ..filter.IncludedTagIds,
            ..filter.ExcludedTagIds,
            ..filter.IncludedShips.SelectMany(sh => sh.MemberTagIds),
            ..filter.ExcludedShips.SelectMany(sh => sh.MemberTagIds),
        ]);

        // ── Tag include ────────────────────────────────────────────────────────────────────
        // Character tags live in StoryCharacters; all others live in StoryTags. Since every
        // TagId belongs to exactly one entity type, the || always resolves to one side only —
        // this is correct without pre-partitioning the id list.
        if (filter.IncludedTagIds.Count > 0)
        {
            if (filter.IncludeMode == TagIncludeMode.Or)
            {
                // OR — story must match at least one included tag (or child) in either collection.
                int[] anyOf = [.. filter.IncludedTagIds.SelectMany(id => expansion[id]).Distinct()];
                query = query.Where(s =>
                    s.StoryCharacters.Any(sc => anyOf.Contains(sc.CharacterTagId)) ||
                    s.StoryTags.Any(st => anyOf.Contains(st.TagId)));
            }
            else
            {
                // AND (default) — story must match every included term; each term is its own
                // {self ∪ children} set with its own subquery, evaluated independently.
                foreach (int tagId in filter.IncludedTagIds)
                {
                    int[] set = expansion[tagId];
                    query = query.Where(s =>
                        s.StoryCharacters.Any(sc => set.Contains(sc.CharacterTagId)) ||
                        s.StoryTags.Any(st => set.Contains(st.TagId)));
                }
            }
        }

        // ── Tag exclude (story must have none of the excluded tags OR their children) ──
        if (filter.ExcludedTagIds.Count > 0)
        {
            int[] noneOf = [.. filter.ExcludedTagIds.SelectMany(id => expansion[id]).Distinct()];
            query = query.Where(s =>
                !s.StoryCharacters.Any(sc => noneOf.Contains(sc.CharacterTagId)) &&
                !s.StoryTags.Any(st => noneOf.Contains(st.TagId)));
        }

        // ── Ship filters (WU-TagFanon) — AND across ships; each ship needs ONE pairing whose
        //    member set covers every named character (roll-up applied per member). ──
        foreach (ShipFilterDto ship in filter.IncludedShips)
            query = ApplyShipTerm(query, ship, expansion, negate: false);
        foreach (ShipFilterDto ship in filter.ExcludedShips)
            query = ApplyShipTerm(query, ship, expansion, negate: true);

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

    /// <summary>
    /// Rejects malformed ship criteria before any query work. A ship names at most
    /// <see cref="ShipFilterDto.MaxMembers"/> characters (the predicate builders below are
    /// explicit per arity so the expression stays EF-translatable). Throws the user-facing
    /// <see cref="StoryValidationException"/> so callers translate it to a 400 rather than
    /// logging an unexpected error.
    /// </summary>
    private static void ValidateShipShape(StoryFilterDto filter)
    {
        List<string> errors = [];
        foreach (ShipFilterDto ship in filter.IncludedShips.Concat(filter.ExcludedShips))
        {
            if (ship.MemberTagIds.Count > ShipFilterDto.MaxMembers)
                errors.Add($"A ship filter supports at most {ShipFilterDto.MaxMembers} characters.");
            if (ship.MemberTagIds.Distinct().Count() != ship.MemberTagIds.Count)
                errors.Add("A ship filter cannot name the same character twice.");
        }
        if (errors.Count > 0) throw new StoryValidationException(errors.Distinct().ToList());
    }

    /// <summary>
    /// Expands each distinct input tag id to <c>{self} ∪ children</c> (hierarchy is one level
    /// deep — a single lookup, no CTE; the query the rejected Cache_TagHierarchy presumed).
    /// </summary>
    private static async Task<Dictionary<int, int[]>> ExpandWithChildrenAsync(
        ReadOnlyApplicationDbContext readDb, IEnumerable<int> tagIds)
    {
        List<int> ids = tagIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        var childRows = await readDb.Tags
            .Where(t => t.ParentTagId != null && ids.Contains(t.ParentTagId.Value))
            .Select(t => new { Parent = t.ParentTagId!.Value, t.TagId })
            .ToListAsync();

        ILookup<int, int> childrenByParent = childRows.ToLookup(r => r.Parent, r => r.TagId);
        return ids.ToDictionary(id => id, id => (int[])[id, .. childrenByParent[id]]);
    }

    /// <summary>
    /// One ship term: the story must (or, negated, must not) contain a single pairing whose
    /// member set covers every named character — each member id already roll-up-expanded.
    /// Members beyond <see cref="ShipFilterDto.MaxMembers"/> are rejected, not silently capped.
    /// Explicit 1/2/3-member branches keep the predicate EF-translatable without expression-tree
    /// assembly.
    /// </summary>
    private static IQueryable<Story> ApplyShipTerm(
        IQueryable<Story> query, ShipFilterDto ship, Dictionary<int, int[]> expansion, bool negate)
    {
        // Arity is already validated by ValidateShipShape at the entry point.
        List<int[]> sets = ship.MemberTagIds.Select(id => expansion[id]).ToList();
        if (sets.Count == 0) return query;

        CharacterPairingType? type = ship.PairingType;
        System.Linq.Expressions.Expression<Func<Story, bool>> predicate = sets.Count switch
        {
            1 => Ship1(sets[0], type),
            2 => Ship2(sets[0], sets[1], type),
            _ => Ship3(sets[0], sets[1], sets[2], type),
        };

        return negate
            ? query.Where(Not(predicate))
            : query.Where(predicate);
    }

    /// <summary>Logical negation of a predicate expression (EF translates NOT(EXISTS…) fine).</summary>
    private static System.Linq.Expressions.Expression<Func<T, bool>> Not<T>(
        System.Linq.Expressions.Expression<Func<T, bool>> expr) =>
        System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(
            System.Linq.Expressions.Expression.Not(expr.Body), expr.Parameters);

    private static System.Linq.Expressions.Expression<Func<Story, bool>> Ship1(int[] a, CharacterPairingType? type) =>
        s => s.StoryCharacterPairings.Any(p =>
            (type == null || p.PairingType == type) &&
            p.Members.Any(m => a.Contains(m.StoryCharacter.CharacterTagId)));

    private static System.Linq.Expressions.Expression<Func<Story, bool>> Ship2(int[] a, int[] b, CharacterPairingType? type) =>
        s => s.StoryCharacterPairings.Any(p =>
            (type == null || p.PairingType == type) &&
            p.Members.Any(m => a.Contains(m.StoryCharacter.CharacterTagId)) &&
            p.Members.Any(m => b.Contains(m.StoryCharacter.CharacterTagId)));

    private static System.Linq.Expressions.Expression<Func<Story, bool>> Ship3(int[] a, int[] b, int[] c, CharacterPairingType? type) =>
        s => s.StoryCharacterPairings.Any(p =>
            (type == null || p.PairingType == type) &&
            p.Members.Any(m => a.Contains(m.StoryCharacter.CharacterTagId)) &&
            p.Members.Any(m => b.Contains(m.StoryCharacter.CharacterTagId)) &&
            p.Members.Any(m => c.Contains(m.StoryCharacter.CharacterTagId)));

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
                st.TagId, st.Tag.TagName, st.Tag.TagTypeId, st.Tag.Description,
                st.Tag.SpriteIdentifier ?? (st.Tag.ParentTag != null ? st.Tag.ParentTag.SpriteIdentifier : null),
                st.Tag.IsFanon, st.Tag.AllowCustomName,
                st.Tag.ParentTagId,
                st.Tag.ParentTag != null ? st.Tag.ParentTag.TagName : null,
                st.CustomName, st.Nuance)).ToList()));

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
        SpriteIdentifier = tag.SpriteIdentifier,  // raw key (parent-inherited); component resolves via ThemeContext
        IsFanon = tag.IsFanon,
        AllowCustomName = tag.AllowCustomName,
        ParentTagId = tag.ParentTagId,
        ParentTagName = tag.ParentTagName,
        CustomName = tag.CustomName,
        Nuance = tag.Nuance
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
        int TagId, string TagName, TagTypeEnum TagTypeId, string? Description, string? SpriteIdentifier,
        bool IsFanon, bool AllowCustomName, int? ParentTagId, string? ParentTagName,
        string? CustomName, string? Nuance);

    private sealed record CharacterDetailRow(
        int CharacterTagId, string TagName, string? SpriteIdentifier,
        bool IsFanon, int? ParentTagId, string? ParentTagName,
        TagPriority Priority, bool IsOc, string? CustomName, string? Nuance);

    private sealed record PairingDetailRow(
        CharacterPairingType PairingType, TagPriority Priority, List<string> MemberNames);
}
