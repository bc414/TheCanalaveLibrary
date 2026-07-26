using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Read side of the fanonization pipeline (WU-TagFanon). Grouping key everywhere:
/// (lower(trim(CustomName)), base tag) — the same normalization the editor nudge and
/// <see cref="FanonLink.NormalizedName"/> use. Character axis reads <c>StoryCharacters</c>;
/// every other axis reads <c>StoryTags</c> of that type.
///
/// <para><b>Access-gating:</b> reach counts are COMPLETE for every viewer (an OC name and a
/// count are not mature content) — group queries bypass the ContentRating filter; the expanded
/// story list keeps it active and pairs the visible rows with the complete count for the
/// count-line disclosure. <c>IsTakenDown</c> stays active everywhere. Public reach counts draw
/// on published stories only (drafts never leak); the author-facing adoption pages include the
/// author's own drafts (personal plane).</para>
/// </summary>
public class ServerFanonReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser,
    ISiteSettingsReadService siteSettings) : IFanonReadService
{
    protected IActiveUserContext ActiveUser { get; } = activeUser;

    /// <summary>Normalization shared by grouping, links, and the editor nudge (6.3).</summary>
    public static string Normalize(string name) => name.Trim().ToLowerInvariant();

    public async Task<IReadOnlyList<FanonGroupDto>> GetGroupsAsync(TagTypeEnum axis, string? search, int page, int pageSize)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        int threshold = await siteSettings.GetIntAsync(SiteSettingKeys.FanonMinAuthorReach, SiteSettingKeys.FanonMinAuthorReachDefault);

        var rows = await GroupQuery(readDb, axis, search)
            .Where(g => g.AuthorCount >= threshold)
            .OrderByDescending(g => g.AuthorCount)
            .ThenByDescending(g => g.StoryCount)
            .ThenBy(g => g.Name)
            .Skip(Math.Max(0, page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        if (rows.Count == 0) return [];

        // Enrich in memory: base-tag chips, links, and per-link adoption state.
        List<int> baseTagIds = rows.Select(r => r.BaseTagId).Distinct().ToList();
        Dictionary<int, TagChipDto> baseChips = await ChipsByIdAsync(readDb, baseTagIds);

        List<string> names = rows.Select(r => r.Name).Distinct().ToList();
        var links = await readDb.FanonLinks
            .Where(l => names.Contains(l.NormalizedName) && baseTagIds.Contains(l.BaseTagId))
            .Select(l => new { l.NormalizedName, l.BaseTagId, l.TargetTagId })
            .ToListAsync();
        Dictionary<(string, int), int> linkTargets = links.ToDictionary(l => (l.NormalizedName, l.BaseTagId), l => l.TargetTagId);
        Dictionary<int, TagChipDto> targetChips = await ChipsByIdAsync(readDb, links.Select(l => l.TargetTagId).Distinct().ToList());

        // ── Two batched queries for the whole page's linked groups (never per-group: the
        //    "Two-Pass Batch Enrichment" rule in layer2-services.md, and the defect shape
        //    tracker MA-408 exists for). ──
        List<int> targetIds = links.Select(l => l.TargetTagId).Distinct().ToList();

        // (1) notified (target, user) pairs — serves BOTH the notified count and the
        //     unnotified-author computation.
        List<(int TargetTagId, int UserId)> notifiedPairs = targetIds.Count == 0
            ? []
            : (await readDb.TagAdoptionStates
                    .Where(s => targetIds.Contains(s.TargetTagId) && s.DateNotified != null)
                    .Select(s => new { s.TargetTagId, s.UserId })
                    .ToListAsync())
                .Select(x => (x.TargetTagId, x.UserId))
                .ToList();
        Dictionary<int, int> notifiedByTarget = notifiedPairs
            .GroupBy(p => p.TargetTagId)
            .ToDictionary(g => g.Key, g => g.Count());
        Dictionary<int, HashSet<int>> notifiedUsersByTarget = notifiedPairs
            .GroupBy(p => p.TargetTagId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.UserId).ToHashSet());

        // (2) (group key → author ids) for every linked group on this page, in ONE query.
        //     The IN×IN predicate over-matches (name × base cross product); the exact
        //     (name, base) pairing is restored in memory below.
        List<(string Name, int BaseTagId)> linkedKeys = rows
            .Where(r => linkTargets.ContainsKey((r.Name, r.BaseTagId)))
            .Select(r => (r.Name, r.BaseTagId))
            .ToList();
        Dictionary<(string, int), HashSet<int>> authorsByGroup =
            await GroupAuthorsBatchedAsync(readDb, axis, linkedKeys);

        List<FanonGroupDto> result = new(rows.Count);
        foreach (var r in rows)
        {
            int? targetId = linkTargets.TryGetValue((r.Name, r.BaseTagId), out int t) ? t : null;
            int unnotified = 0;
            if (targetId is int tid)
            {
                HashSet<int> groupAuthors = authorsByGroup.GetValueOrDefault((r.Name, r.BaseTagId)) ?? [];
                HashSet<int> notified = notifiedUsersByTarget.GetValueOrDefault(tid) ?? [];
                unnotified = groupAuthors.Count(a => !notified.Contains(a));
            }

            result.Add(new FanonGroupDto(
                r.DisplayName,
                baseChips.GetValueOrDefault(r.BaseTagId) ?? new TagChipDto { TagId = r.BaseTagId, TagName = "?", TagTypeId = axis },
                r.StoryCount,
                r.AuthorCount,
                targetId is int id ? targetChips.GetValueOrDefault(id) : null,
                targetId is int id2 ? notifiedByTarget.GetValueOrDefault(id2) : 0,
                unnotified));
        }
        return result;
    }

    public async Task<int> GetGroupCountAsync(TagTypeEnum axis, string? search)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        int threshold = await siteSettings.GetIntAsync(SiteSettingKeys.FanonMinAuthorReach, SiteSettingKeys.FanonMinAuthorReachDefault);
        return await GroupQuery(readDb, axis, search).Where(g => g.AuthorCount >= threshold).CountAsync();
    }

    public async Task<FanonGroupStoriesDto> GetGroupStoriesAsync(TagTypeEnum axis, int baseTagId, string name)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        string normalized = Normalize(name);

        // Complete count (rating-bypassed) for the count-line disclosure…
        int total = await GroupStoryIdsQuery(readDb, axis, baseTagId, normalized, bypassRating: true).CountAsync();
        // …visible rows under the viewer's own consent (ContentRating filter ACTIVE).
        List<int> visibleIds = await GroupStoryIdsQuery(readDb, axis, baseTagId, normalized, bypassRating: false)
            .ToListAsync();

        List<FanonGroupStoryDto> visible = await readDb.Stories
            .Where(s => visibleIds.Contains(s.StoryId))
            .OrderByDescending(s => s.LastUpdatedDate)
            .Select(s => new FanonGroupStoryDto(
                s.StoryId,
                s.StoryListing != null ? s.StoryListing.StoryTitle : string.Empty,
                s.AuthorId,
                s.Author != null ? s.Author.UserName : null,
                s.Rating))
            .ToListAsync();

        return new FanonGroupStoriesDto(visible, total);
    }

    public async Task<IReadOnlyList<FanonTagDto>> GetEstablishedFanonTagsAsync()
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        var rows = await readDb.Tags
            .Where(t => t.IsFanon)
            .Select(t => new
            {
                Chip = new TagChipDto
                {
                    TagId = t.TagId, TagName = t.TagName, TagTypeId = t.TagTypeId,
                    Description = t.Description,
                    SpriteIdentifier = t.SpriteIdentifier ?? (t.ParentTag != null ? t.ParentTag.SpriteIdentifier : null),
                    IsFanon = t.IsFanon, AllowCustomName = t.AllowCustomName,
                    ParentTagId = t.ParentTagId,
                    ParentTagName = t.ParentTag != null ? t.ParentTag.TagName : null
                },
                Reach = t.StoryTags.Count + t.StoryCharacters.Count
            })
            .OrderByDescending(x => x.Reach)
            .ThenBy(x => x.Chip.TagName)
            .ToListAsync();
        return rows.Select(x => new FanonTagDto(x.Chip, x.Reach)).ToList();
    }

    public async Task<TagAdoptionPageDto?> GetMyAdoptionPageAsync(int targetTagId)
    {
        if (ActiveUser.UserId is not int viewerId)
            throw new UnauthorizedAccessException("Sign in to view tag adoptions.");

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        TagChipDto? target = (await ChipsByIdAsync(readDb, [targetTagId])).GetValueOrDefault(targetTagId);
        if (target is null) return null;

        var links = await readDb.FanonLinks
            .Where(l => l.TargetTagId == targetTagId)
            .Select(l => new { l.NormalizedName, l.BaseTagId, BaseTypeId = l.BaseTag.TagTypeId, BaseName = l.BaseTag.TagName })
            .ToListAsync();
        if (links.Count == 0) return null;

        bool dismissed = await readDb.TagAdoptionStates
            .AnyAsync(s => s.UserId == viewerId && s.TargetTagId == targetTagId && s.IsDismissed);

        List<TagAdoptionRowDto> rows = [];
        foreach (var link in links)
        {
            if (link.BaseTypeId == TagTypeEnum.Character)
            {
                // Personal plane: the author's own stories, drafts included, rating irrelevant.
                var mine = await readDb.StoryCharacters
                    .IgnoreQueryFilters(["ContentRating"])
                    .Where(sc => sc.Story.AuthorId == viewerId
                        && sc.CharacterTagId == link.BaseTagId
                        && sc.CustomName != null
                        && sc.CustomName!.ToLower().Trim() == link.NormalizedName)
                    .Select(sc => new
                    {
                        sc.StoryId,
                        Title = sc.Story.StoryListing != null ? sc.Story.StoryListing.StoryTitle : string.Empty,
                        sc.CustomName,
                        sc.Nuance,
                        Collides = sc.Story.StoryCharacters.Any(other => other.CharacterTagId == targetTagId)
                    })
                    .ToListAsync();
                rows.AddRange(mine.Select(m => new TagAdoptionRowDto(
                    m.StoryId, m.Title, TagTypeEnum.Character, link.BaseName, m.CustomName!, m.Nuance, m.Collides)));
            }
            else
            {
                var mine = await readDb.StoryTags
                    .IgnoreQueryFilters(["ContentRating"])
                    .Where(st => st.Story.AuthorId == viewerId
                        && st.TagId == link.BaseTagId
                        && st.CustomName != null
                        && st.CustomName!.ToLower().Trim() == link.NormalizedName)
                    .Select(st => new
                    {
                        st.StoryId,
                        Title = st.Story.StoryListing != null ? st.Story.StoryListing.StoryTitle : string.Empty,
                        st.CustomName,
                        st.Nuance,
                        Collides = st.Story.StoryTags.Any(other => other.TagId == targetTagId)
                    })
                    .ToListAsync();
                rows.AddRange(mine.Select(m => new TagAdoptionRowDto(
                    m.StoryId, m.Title, link.BaseTypeId, link.BaseName, m.CustomName!, m.Nuance, m.Collides)));
            }
        }

        return new TagAdoptionPageDto(target, dismissed, rows.OrderBy(r => r.StoryTitle).ToList());
    }

    public async Task<IReadOnlyList<MyTagAdoptionSummaryDto>> GetMyAdoptionIndexAsync()
    {
        if (ActiveUser.UserId is not int viewerId)
            throw new UnauthorizedAccessException("Sign in to view tag adoptions.");

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

        var links = await readDb.FanonLinks
            .Select(l => new { l.NormalizedName, l.BaseTagId, BaseTypeId = l.BaseTag.TagTypeId, l.TargetTagId })
            .ToListAsync();
        if (links.Count == 0) return [];

        Dictionary<int, bool> dismissedByTarget = (await readDb.TagAdoptionStates
                .Where(s => s.UserId == viewerId)
                .Select(s => new { s.TargetTagId, s.IsDismissed })
                .ToListAsync())
            .ToDictionary(s => s.TargetTagId, s => s.IsDismissed);

        // TWO batched queries for the viewer's whole matching set — never one per link. The old
        // per-link loop was unbounded by paging: it cost one query per fanon link SITE-WIDE on
        // every load of /tag-adoptions.
        List<string> allNames = links.Select(l => l.NormalizedName).Distinct().ToList();
        List<int> charBaseIds = links.Where(l => l.BaseTypeId == TagTypeEnum.Character)
            .Select(l => l.BaseTagId).Distinct().ToList();
        List<int> flatBaseIds = links.Where(l => l.BaseTypeId != TagTypeEnum.Character)
            .Select(l => l.BaseTagId).Distinct().ToList();

        List<(string Name, int BaseTagId)> charRows = charBaseIds.Count == 0
            ? []
            : (await readDb.StoryCharacters
                    .IgnoreQueryFilters(["ContentRating"])
                    .Where(sc => sc.Story.AuthorId == viewerId
                        && sc.CustomName != null
                        && charBaseIds.Contains(sc.CharacterTagId)
                        && allNames.Contains(sc.CustomName!.ToLower().Trim()))
                    .Select(sc => new { Name = sc.CustomName!.ToLower().Trim(), BaseTagId = sc.CharacterTagId })
                    .ToListAsync())
                .Select(x => (x.Name, x.BaseTagId)).ToList();

        List<(string Name, int BaseTagId)> flatRows = flatBaseIds.Count == 0
            ? []
            : (await readDb.StoryTags
                    .IgnoreQueryFilters(["ContentRating"])
                    .Where(st => st.Story.AuthorId == viewerId
                        && st.CustomName != null
                        && flatBaseIds.Contains(st.TagId)
                        && allNames.Contains(st.CustomName!.ToLower().Trim()))
                    .Select(st => new { Name = st.CustomName!.ToLower().Trim(), BaseTagId = st.TagId })
                    .ToListAsync())
                .Select(x => (x.Name, x.BaseTagId)).ToList();

        // Tally the over-matched rows back onto exact (name, base) link keys.
        Dictionary<(string, int), int> countByKey = charRows.Concat(flatRows)
            .GroupBy(r => (r.Name, r.BaseTagId))
            .ToDictionary(g => g.Key, g => g.Count());

        Dictionary<int, int> pendingByTarget = [];
        foreach (var link in links)
        {
            int pending = countByKey.GetValueOrDefault((link.NormalizedName, link.BaseTagId));
            if (pending > 0)
                pendingByTarget[link.TargetTagId] = pendingByTarget.GetValueOrDefault(link.TargetTagId) + pending;
        }

        // Every target the viewer holds pending rows OR state for (a dismissed/emptied target
        // stays listed so dismissal is reversible and history has a home).
        HashSet<int> targetIds = [.. pendingByTarget.Keys, .. dismissedByTarget.Keys];
        if (targetIds.Count == 0) return [];
        Dictionary<int, TagChipDto> chips = await ChipsByIdAsync(readDb, targetIds.ToList());

        return targetIds
            .Where(chips.ContainsKey)
            .Select(id => new MyTagAdoptionSummaryDto(
                chips[id],
                pendingByTarget.GetValueOrDefault(id),
                dismissedByTarget.GetValueOrDefault(id)))
            .OrderByDescending(s => s.PendingRowCount)
            .ThenBy(s => s.TargetTag.TagName)
            .ToList();
    }

    public async Task<TagChipDto?> FindOfficialTagByNameAsync(TagTypeEnum axis, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        string normalized = Normalize(name);

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

        // A fanon link whose group name matches wins (the tag's own name may carry a
        // disambiguator — "Saura (Silver Resistance)" never string-matches "Saura").
        int? linkedTarget = await readDb.FanonLinks
            .Where(l => l.NormalizedName == normalized && l.BaseTag.TagTypeId == axis)
            .Select(l => (int?)l.TargetTagId)
            .FirstOrDefaultAsync();
        if (linkedTarget is int targetId)
            return (await ChipsByIdAsync(readDb, [targetId])).GetValueOrDefault(targetId);

        // Otherwise an exact (normalized) official tag name of the axis type.
        var direct = await readDb.Tags
            .Where(t => t.TagTypeId == axis && t.TagName.ToLower() == normalized)
            .Select(t => t.TagId)
            .FirstOrDefaultAsync();
        return direct == 0 ? null : (await ChipsByIdAsync(readDb, [direct])).GetValueOrDefault(direct);
    }

    // ── Shared query builders ─────────────────────────────────────────────────────

    // Member-init class, NOT a positional record: the dashboard query composes Where/OrderBy/
    // Count on top of this projection, and EF can only bind post-Select member access when the
    // projection is member-initialized (constructor projections aren't composable).
    private sealed class GroupRow
    {
        public string Name { get; init; } = null!;
        public string DisplayName { get; init; } = null!;
        public int BaseTagId { get; init; }
        public int StoryCount { get; init; }
        public int AuthorCount { get; init; }
    }

    /// <summary>
    /// The grouped, thresholdable dashboard query. Published stories only (drafts never feed
    /// public reach), IsTakenDown active, ContentRating bypassed (counts are complete for every
    /// viewer — 6.5).
    /// </summary>
    private static IQueryable<GroupRow> GroupQuery(ReadOnlyApplicationDbContext readDb, TagTypeEnum axis, string? search)
    {
        if (axis == TagTypeEnum.Character)
        {
            IQueryable<StoryCharacter> q = readDb.StoryCharacters
                .IgnoreQueryFilters(["ContentRating"])
                .Where(sc => sc.CustomName != null
                    && sc.Story.StoryStatusId >= StoryStatusEnum.InProgress
                    && sc.Story.StoryStatusId <= StoryStatusEnum.OpenBeta);
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(sc => EF.Functions.ILike(sc.CustomName!, $"%{search}%"));
            return q
                .GroupBy(sc => new { Name = sc.CustomName!.ToLower().Trim(), sc.CharacterTagId })
                .Select(g => new GroupRow
                {
                    Name = g.Key.Name,
                    DisplayName = g.Min(x => x.CustomName)!,
                    BaseTagId = g.Key.CharacterTagId,
                    StoryCount = g.Select(x => x.StoryId).Distinct().Count(),
                    AuthorCount = g.Where(x => x.Story.AuthorId != null).Select(x => x.Story.AuthorId).Distinct().Count()
                });
        }

        IQueryable<StoryTag> fq = readDb.StoryTags
            .IgnoreQueryFilters(["ContentRating"])
            .Where(st => st.CustomName != null
                && st.Tag.TagTypeId == axis
                && st.Story.StoryStatusId >= StoryStatusEnum.InProgress
                && st.Story.StoryStatusId <= StoryStatusEnum.OpenBeta);
        if (!string.IsNullOrWhiteSpace(search))
            fq = fq.Where(st => EF.Functions.ILike(st.CustomName!, $"%{search}%"));
        return fq
            .GroupBy(st => new { Name = st.CustomName!.ToLower().Trim(), st.TagId })
            .Select(g => new GroupRow
            {
                Name = g.Key.Name,
                DisplayName = g.Min(x => x.CustomName)!,
                BaseTagId = g.Key.TagId,
                StoryCount = g.Select(x => x.StoryId).Distinct().Count(),
                AuthorCount = g.Where(x => x.Story.AuthorId != null).Select(x => x.Story.AuthorId).Distinct().Count()
            });
    }

    private static IQueryable<int> GroupStoryIdsQuery(
        ReadOnlyApplicationDbContext readDb, TagTypeEnum axis, int baseTagId, string normalizedName, bool bypassRating)
    {
        if (axis == TagTypeEnum.Character)
        {
            IQueryable<StoryCharacter> q = readDb.StoryCharacters;
            if (bypassRating) q = q.IgnoreQueryFilters(["ContentRating"]);
            return q
                .Where(sc => sc.CharacterTagId == baseTagId
                    && sc.CustomName != null
                    && sc.CustomName!.ToLower().Trim() == normalizedName
                    && sc.Story.StoryStatusId >= StoryStatusEnum.InProgress
                    && sc.Story.StoryStatusId <= StoryStatusEnum.OpenBeta)
                .Select(sc => sc.StoryId)
                .Distinct();
        }

        IQueryable<StoryTag> fq = readDb.StoryTags;
        if (bypassRating) fq = fq.IgnoreQueryFilters(["ContentRating"]);
        return fq
            .Where(st => st.TagId == baseTagId
                && st.CustomName != null
                && st.CustomName!.ToLower().Trim() == normalizedName
                && st.Story.StoryStatusId >= StoryStatusEnum.InProgress
                && st.Story.StoryStatusId <= StoryStatusEnum.OpenBeta)
            .Select(st => st.StoryId)
            .Distinct();
    }

    /// <summary>
    /// Distinct author ids per group key, for MANY groups in ONE query (the batch counterpart of
    /// the old per-group lookup). The SQL predicate is an over-matching IN×IN cross product; the
    /// exact (normalized name, base tag) pairing is restored in memory, which is cheap and keeps
    /// the round-trip count at one regardless of page size.
    /// </summary>
    protected static async Task<Dictionary<(string, int), HashSet<int>>> GroupAuthorsBatchedAsync(
        ReadOnlyApplicationDbContext readDb, TagTypeEnum axis, List<(string Name, int BaseTagId)> keys)
    {
        if (keys.Count == 0) return [];

        List<string> names = keys.Select(k => k.Name).Distinct().ToList();
        List<int> baseIds = keys.Select(k => k.BaseTagId).Distinct().ToList();
        HashSet<(string, int)> wanted = keys.ToHashSet();

        List<(string Name, int BaseTagId, int AuthorId)> pairs;
        if (axis == TagTypeEnum.Character)
        {
            pairs = (await readDb.StoryCharacters
                    .IgnoreQueryFilters(["ContentRating"])
                    .Where(sc => sc.CustomName != null
                        && baseIds.Contains(sc.CharacterTagId)
                        && names.Contains(sc.CustomName!.ToLower().Trim())
                        && sc.Story.AuthorId != null
                        && sc.Story.StoryStatusId >= StoryStatusEnum.InProgress
                        && sc.Story.StoryStatusId <= StoryStatusEnum.OpenBeta)
                    .Select(sc => new
                    {
                        Name = sc.CustomName!.ToLower().Trim(),
                        BaseTagId = sc.CharacterTagId,
                        AuthorId = sc.Story.AuthorId!.Value
                    })
                    .Distinct()
                    .ToListAsync())
                .Select(x => (x.Name, x.BaseTagId, x.AuthorId))
                .ToList();
        }
        else
        {
            pairs = (await readDb.StoryTags
                    .IgnoreQueryFilters(["ContentRating"])
                    .Where(st => st.CustomName != null
                        && baseIds.Contains(st.TagId)
                        && names.Contains(st.CustomName!.ToLower().Trim())
                        && st.Story.AuthorId != null
                        && st.Story.StoryStatusId >= StoryStatusEnum.InProgress
                        && st.Story.StoryStatusId <= StoryStatusEnum.OpenBeta)
                    .Select(st => new
                    {
                        Name = st.CustomName!.ToLower().Trim(),
                        BaseTagId = st.TagId,
                        AuthorId = st.Story.AuthorId!.Value
                    })
                    .Distinct()
                    .ToListAsync())
                .Select(x => (x.Name, x.BaseTagId, x.AuthorId))
                .ToList();
        }

        return pairs
            .Where(p => wanted.Contains((p.Name, p.BaseTagId)))
            .GroupBy(p => (p.Name, p.BaseTagId))
            .ToDictionary(g => g.Key, g => g.Select(p => p.AuthorId).ToHashSet());
    }

    /// <summary>Full-field chip lookup by id (parent-inherited sprite, parent name, fanon flag).</summary>
    protected static async Task<Dictionary<int, TagChipDto>> ChipsByIdAsync(
        ReadOnlyApplicationDbContext readDb, List<int> tagIds)
    {
        if (tagIds.Count == 0) return [];
        List<TagChipDto> chips = await readDb.Tags
            .Where(t => tagIds.Contains(t.TagId))
            .Select(t => new TagChipDto
            {
                TagId = t.TagId, TagName = t.TagName, TagTypeId = t.TagTypeId,
                Description = t.Description,
                SpriteIdentifier = t.SpriteIdentifier ?? (t.ParentTag != null ? t.ParentTag.SpriteIdentifier : null),
                IsFanon = t.IsFanon, AllowCustomName = t.AllowCustomName,
                ParentTagId = t.ParentTagId,
                ParentTagName = t.ParentTag != null ? t.ParentTag.TagName : null
            })
            .ToListAsync();
        return chips.ToDictionary(c => c.TagId);
    }
}
