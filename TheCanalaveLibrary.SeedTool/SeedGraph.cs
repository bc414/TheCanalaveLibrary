using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.SeedTool;

// ── In-memory row shapes (mirror the COPY column lists in SeedBulkWriter) ──────────────────────

public sealed record SeedUserRow(
    int Id, string UserName, bool ShowMatureContent, bool AllowDiscoveryFromHiddenFavorites);

public sealed record SeedStoryRow(
    int Id, int AuthorId, Rating Rating, StoryStatusEnum Status, DateTime PublishedUtc,
    string Title, string ShortDescription, string Slug, double Popularity)
{
    public int WordCount { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = PublishedUtc;

    /// <summary>The discovery-mart visibility predicate (approved statuses, never taken down here).</summary>
    public bool IsVisible => Status is >= StoryStatusEnum.InProgress and <= StoryStatusEnum.OpenBeta;
}

public sealed record SeedChapterRow(int Id, int StoryId, int Number, string Title, bool IsPublished)
{
    public long ContentId { get; set; }
}

public sealed record SeedChapterContentRow(
    long Id, int ChapterId, int AuthorId, string Html, int WordCount, DateTime PublishUtc);

public sealed class SeedInteractionRow(int userId, int storyId)
{
    public int UserId { get; } = userId;
    public int StoryId { get; } = storyId;
    public bool HasStarted { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsHiddenFavorite { get; set; }
    public bool IsFollowed { get; set; }
    public bool IsReadItLater { get; set; }
    public bool IsIgnored { get; set; }
    public DateTime? FavoriteDateUtc { get; set; }
    public DateTime? HiddenFavoriteDateUtc { get; set; }
}

public sealed class SeedRecommendationRow(int id, int storyId, int? recommenderId, DateTime datePostedUtc, string text)
{
    public int Id { get; } = id;
    public int StoryId { get; } = storyId;
    public int? RecommenderId { get; } = recommenderId;
    public DateTime DatePostedUtc { get; } = datePostedUtc;
    public string Text { get; } = text;
    public bool IsHiddenGem { get; set; }
    public bool IsHighlightedByAuthor { get; set; }
}

public sealed record SeedVouchRow(int VouchingUserId, int VouchedUserId, DateTime DateUtc);

/// <summary>WU-StatBadgeProducers — a story-author credit to another user. Composite PK
/// (StoryId, AcknowledgedUserId, AcknowledgmentRoleId); no surrogate id.</summary>
public sealed record SeedAcknowledgmentRow(
    int StoryId, int AcknowledgedUserId, short RoleId, short StatusId,
    DateTime DateAcknowledgedUtc, DateTime? DateRespondedUtc);

/// <summary>WU-StatBadgeProducers — an "Inspired By" (RelationshipTypeId 1) link between two
/// different authors' stories, the source for `UserStat.AcknowledgedAsInspirationCount`.
/// Composite PK (SourceStoryId, TargetStoryId, RelationshipTypeId); no surrogate id. Only type 1
/// is generated here — the other three lineage types have no counter to make measurable.</summary>
public sealed record SeedLineageRow(
    int SourceStoryId, int TargetStoryId, short StatusId, DateTime DateCreatedUtc);

/// <summary>TPT: one base_comments row + one chapter_comments row per instance.</summary>
public sealed record SeedChapterCommentRow(
    long Id, int ChapterId, int UserId, long? ParentCommentId, string Text, DateTime DatePostedUtc, bool IsSpoiler);

public sealed record SeedNotificationRow(
    long Id, int RecipientUserId, int? SourceUserId, short TypeId, int RelatedEntityId,
    bool IsRead, DateTime DateCreatedUtc);

// ── Tag world (WU-TagFanon) ────────────────────────────────────────────────────────────────────

public sealed record SeedTagRow(
    int Id, string Name, TagTypeEnum TypeId, bool IsFanon, bool AllowCustomName,
    int? ParentTagId, string? SpriteIdentifier, string? Description);

public sealed record SeedStoryTagRow(
    int StoryId, int TagId, TagPriority Priority, string? CustomName, string? Nuance);

public sealed record SeedStoryCharacterRow(
    int Id, int StoryId, int CharacterTagId, TagPriority Priority, bool IsOc,
    string? CustomName, string? Nuance);

public sealed record SeedPairingRow(int Id, int StoryId, CharacterPairingType PairingType, TagPriority Priority);

public sealed record SeedPairingMemberRow(int PairingId, int StoryCharacterId);

public sealed record SeedSavedSelectionRow(
    int Id, int UserId, string Nickname, bool IsPublic, string? Description, DateTime DateCreatedUtc);

public sealed record SeedSavedSelectionEntryRow(int Id, int SelectionId, int TagId, bool IsExcluded);

public sealed record SeedNotificationSettingRow(int UserId, short NotificationTypeId, bool EmailEnabled, bool Collapsed);

public sealed record SeedFanonLinkRow(
    int Id, string NormalizedName, int BaseTagId, int TargetTagId, int? LinkedByUserId, DateTime DateLinkedUtc);

public sealed record SeedAdoptionStateRow(int UserId, int TargetTagId, DateTime? DateNotifiedUtc, bool IsDismissed);

public sealed class SeedGraph
{
    public required List<SeedUserRow> Users { get; init; }
    public required List<SeedStoryRow> Stories { get; init; }
    public required List<SeedChapterRow> Chapters { get; init; }
    public required List<SeedChapterContentRow> ChapterContents { get; init; }
    public required List<SeedInteractionRow> Interactions { get; init; }
    public required List<SeedRecommendationRow> Recommendations { get; init; }
    public required List<SeedVouchRow> Vouches { get; init; }
    public required List<SeedAcknowledgmentRow> Acknowledgments { get; init; }
    public required List<SeedLineageRow> Lineages { get; init; }
    public required List<SeedChapterCommentRow> ChapterComments { get; init; }
    public required List<SeedNotificationRow> Notifications { get; init; }
    public required int HiddenGemChainCount { get; init; }

    // Tag world (WU-TagFanon): vocabulary with parent/child hierarchy + fanon population,
    // per-story overlays, pairings, saved selections (F15 — tracker C4 half), notification
    // settings, and one pre-linked fanon cluster (link + adoption states + type-26 rows).
    public required List<SeedTagRow> Tags { get; init; }
    public required List<SeedStoryTagRow> StoryTags { get; init; }
    public required List<SeedStoryCharacterRow> StoryCharacters { get; init; }
    public required List<SeedPairingRow> Pairings { get; init; }
    public required List<SeedPairingMemberRow> PairingMembers { get; init; }
    public required List<SeedSavedSelectionRow> SavedSelections { get; init; }
    public required List<SeedSavedSelectionEntryRow> SavedSelectionEntries { get; init; }
    public required List<SeedNotificationSettingRow> NotificationSettings { get; init; }
    public required List<SeedFanonLinkRow> FanonLinks { get; init; }
    public required List<SeedAdoptionStateRow> AdoptionStates { get; init; }
}

/// <summary>
/// Deterministic generator of the clustered discovery graph (D3, layer8-data-marts.md
/// "horizontal line crossed"). Everything derives from one <see cref="Random"/> seeded by
/// <c>--seed</c> and a FIXED time anchor, so the same arguments over the same starting database
/// produce the same dataset. Uniform-random volume is deliberately avoided — the point of this
/// tool is the STRUCTURE:
///
/// <list type="bullet">
/// <item>taste-communities: users and stories cluster; favorites overlap inside a community →
///   non-uniform, rankable co-occurrence scores;</item>
/// <item>power-law story popularity + supernode recommenders → wide-mode flooding is visible
///   and the traversal fan-out cap matters;</item>
/// <item>wired hidden-gem chains over niche stories → deep-mode chain-of-trust searches reach
///   degree 5–6 (curator→curator hops), not just theoretical;</item>
/// <item>author spotlights (≤5/story) — the other chain-of-trust edge;</item>
/// <item>vouches biased toward low-volume authors — the vouch projection's whole purpose;</item>
/// <item>consent-split hidden favorites — the edge-owner consent rule is observable both ways;</item>
/// <item>a sprinkle of drafts / pending stories and anonymized recommendations — negative-test
///   rows the mart build predicates must exclude.</item>
/// </list>
/// </summary>
public sealed class SeedGraphGenerator(SeedToolOptions options, SeedIdBases bases)
{
    // Fixed anchor (NOT wall clock) so the dataset is fully reproducible for a given seed.
    private static readonly DateTime Anchor = new(2026, 07, 01, 0, 0, 0, DateTimeKind.Utc);

    private readonly Random _rng = new(options.Seed);

    private static readonly string[] WordBank =
        ("canal harbor lantern tide sailor compass beacon quay drift anchor gale mast breeze current " +
         "library shelf archive quill ledger chronicle margin chapter draft stanza glossary preface " +
         "torterra sapling grove bramble meadow thicket fern moss cedar willow acorn tundra summit").Split(' ');

    public SeedGraph Generate()
    {
        int communityCount = options.Communities;

        // ── Users ────────────────────────────────────────────────────────────────────────────
        List<SeedUserRow> users = new(options.Users);
        int[] userPrimaryCommunity = new int[options.Users];
        int[] userSecondaryCommunity = new int[options.Users]; // -1 = none
        bool[] userIsAuthor = new bool[options.Users];
        bool[] userIsPowerRecommender = new bool[options.Users];

        for (int i = 0; i < options.Users; i++)
        {
            int id = bases.UserId + i;
            bool consent = _rng.NextDouble() < 0.20;
            users.Add(new SeedUserRow(id, $"seed-user-{i + 1:00000}", _rng.NextDouble() < 0.40, consent));
            userPrimaryCommunity[i] = SampleZipf(communityCount);
            userSecondaryCommunity[i] = _rng.NextDouble() < 0.30 ? _rng.Next(communityCount) : -1;
            userIsAuthor[i] = _rng.NextDouble() < 0.15;
            userIsPowerRecommender[i] = _rng.NextDouble() < 0.01;
        }
        // Guarantee minimum interesting populations regardless of the volume arguments.
        for (int i = 0; i < Math.Min(5, options.Users); i++) userIsAuthor[i] = true;
        for (int i = 0; i < Math.Min(3, options.Users); i++) userIsPowerRecommender[options.Users - 1 - i] = true;

        int[] authorIndexes = Enumerable.Range(0, options.Users).Where(i => userIsAuthor[i]).ToArray();

        // ── Stories (power-law author output + power-law popularity) ────────────────────────
        List<SeedStoryRow> stories = new(options.Stories);
        List<List<int>> storiesByCommunity = [.. Enumerable.Range(0, communityCount).Select(_ => new List<int>())];
        Dictionary<int, List<int>> storyIndexesByAuthor = [];

        for (int i = 0; i < options.Stories; i++)
        {
            int id = bases.StoryId + i;
            int authorIdx = authorIndexes[SampleZipf(authorIndexes.Length)];
            int community = _rng.NextDouble() < 0.85
                ? userPrimaryCommunity[authorIdx]
                : _rng.Next(communityCount);

            StoryStatusEnum status = _rng.NextDouble() switch
            {
                < 0.55 => StoryStatusEnum.InProgress,
                < 0.85 => StoryStatusEnum.Completed,
                < 0.93 => StoryStatusEnum.OnHiatus,
                < 0.96 => StoryStatusEnum.Draft,          // excluded by the mart build predicate
                < 0.98 => StoryStatusEnum.PendingApproval, // excluded by the mart build predicate
                _ => StoryStatusEnum.Cancelled,
            };
            Rating rating = _rng.NextDouble() switch { < 0.30 => Rating.E, < 0.75 => Rating.T, _ => Rating.M };
            DateTime published = Anchor.AddDays(-_rng.Next(30, 1000)).AddMinutes(_rng.Next(1440));

            SeedStoryRow story = new(
                id, bases.UserId + authorIdx, rating, status, published,
                Title: $"Seed Story {i + 1:00000}: {Phrase(3)}",
                ShortDescription: $"Seed short description — {Phrase(10)}.",
                Slug: $"seed-story-{i + 1:00000}",
                Popularity: 1.0 / (1 + SampleZipf(200)));
            stories.Add(story);

            if (story.IsVisible) storiesByCommunity[community].Add(i);
            (storyIndexesByAuthor.TryGetValue(authorIdx, out List<int>? list)
                ? list
                : storyIndexesByAuthor[authorIdx] = []).Add(i);
        }

        // ── Chapters + contents (word counts kept by construction, DataSeeder-style) ────────
        List<SeedChapterRow> chapters = [];
        List<SeedChapterContentRow> contents = [];
        int chapterId = bases.ChapterId;
        long contentId = bases.ChapterContentId;
        foreach (SeedStoryRow story in stories)
        {
            int chapterCount = 1 + Math.Min(19, (int)(-4.0 * Math.Log(1 - _rng.NextDouble()))); // geometric, mean ≈ 5
            int storyWords = 0;
            for (int n = 1; n <= chapterCount; n++)
            {
                SeedChapterRow chapter = new(chapterId++, story.Id, n, $"Chapter {n}: {Phrase(2)}",
                    IsPublished: story.Status != StoryStatusEnum.Draft);
                string html = ChapterHtml(story.Id, n);
                int words = ChapterText.CountWords(html);
                storyWords += words;
                SeedChapterContentRow content = new(contentId++, chapter.Id, story.AuthorId, html, words,
                    story.PublishedUtc.AddDays(n - 1));
                chapter.ContentId = content.Id;
                chapters.Add(chapter);
                contents.Add(content);
            }
            story.WordCount = storyWords;
            story.LastUpdatedUtc = story.PublishedUtc.AddDays(chapterCount - 1);
        }

        // ── Interactions: community-clustered favorites + exclusion-filter noise ────────────
        Dictionary<(int UserId, int StoryId), SeedInteractionRow> interactions = [];
        SeedInteractionRow GetInteraction(int userId, int storyIdx)
        {
            (int, int) key = (userId, stories[storyIdx].Id);
            return interactions.TryGetValue(key, out SeedInteractionRow? row)
                ? row
                : interactions[key] = new SeedInteractionRow(userId, stories[storyIdx].Id);
        }

        for (int u = 0; u < options.Users; u++)
        {
            int userId = bases.UserId + u;
            int favoriteCount = 5 + SampleZipf(options.FavoritesPerUserSpread);
            HashSet<int> picked = [];
            for (int k = 0; k < favoriteCount; k++)
            {
                int storyIdx = PickClusteredStory(u, userPrimaryCommunity, userSecondaryCommunity, storiesByCommunity, stories);
                if (storyIdx < 0 || !picked.Add(storyIdx)) continue;

                SeedInteractionRow row = GetInteraction(userId, storyIdx);
                bool hidden = _rng.NextDouble() < 0.15;
                if (hidden)
                {
                    row.IsHiddenFavorite = true;
                    row.HiddenFavoriteDateUtc = AfterPublish(stories[storyIdx]);
                }
                else
                {
                    row.IsFavorite = true;
                    row.FavoriteDateUtc = AfterPublish(stories[storyIdx]);
                }
                if (_rng.NextDouble() < 0.60) row.HasStarted = true;
                if (row.HasStarted && _rng.NextDouble() < 0.40) row.IsCompleted = true;
                if (_rng.NextDouble() < 0.20) row.IsFollowed = true;
            }

            // Non-favorite noise so exclusion filters have something real to exclude.
            for (int k = 0; k < 4; k++)
            {
                int storyIdx = PickClusteredStory(u, userPrimaryCommunity, userSecondaryCommunity, storiesByCommunity, stories);
                if (storyIdx < 0 || picked.Contains(storyIdx)) continue;
                SeedInteractionRow row = GetInteraction(userId, storyIdx);
                switch (_rng.Next(3))
                {
                    case 0: row.IsIgnored = true; break;
                    case 1: row.IsReadItLater = true; break;
                    default: row.HasStarted = true; break;
                }
            }
        }

        // ── Recommendations (readers + supernode power recommenders + anonymized sprinkle) ──
        List<SeedRecommendationRow> recommendations = [];
        Dictionary<int, HashSet<int>> recStoriesByUser = []; // userIdx → story indexes recommended
        int recId = bases.RecommendationId;

        void AddRecommendation(int userIdx, int storyIdx)
        {
            // Self-rec block (WU-RecLifecycle): seed data mirrors the production invariant —
            // a story's author never recommends their own story.
            if (stories[storyIdx].AuthorId == bases.UserId + userIdx) return;
            HashSet<int> set = recStoriesByUser.TryGetValue(userIdx, out HashSet<int>? s)
                ? s
                : recStoriesByUser[userIdx] = [];
            if (!set.Add(storyIdx)) return; // unique (recommender, story)
            recommendations.Add(new SeedRecommendationRow(
                recId++, stories[storyIdx].Id, bases.UserId + userIdx,
                AfterPublish(stories[storyIdx]),
                $"<p>Seed recommendation — {Phrase(24)}.</p>"));
        }

        foreach (SeedInteractionRow row in interactions.Values.Where(r => r.IsFavorite).ToList())
        {
            if (_rng.NextDouble() >= 0.12) continue;
            int userIdx = row.UserId - bases.UserId;
            int storyIdx = row.StoryId - bases.StoryId; // stories are generated id-contiguous
            if (stories[storyIdx].IsVisible) AddRecommendation(userIdx, storyIdx);
        }
        for (int u = 0; u < options.Users; u++)
        {
            if (!userIsPowerRecommender[u]) continue;
            int recCount = 150 + _rng.Next(150); // the supernode: floods wide-mode traversal
            for (int k = 0; k < recCount; k++)
            {
                int storyIdx = PickClusteredStory(u, userPrimaryCommunity, userSecondaryCommunity, storiesByCommunity, stories);
                if (storyIdx >= 0) AddRecommendation(u, storyIdx);
            }
        }
        // Anonymized recommendations (recommender NULL) — must contribute NO edge (AD4).
        for (int k = 0; k < Math.Max(10, options.Stories / 100); k++)
        {
            int storyIdx = _rng.Next(stories.Count);
            if (!stories[storyIdx].IsVisible) continue;
            recommendations.Add(new SeedRecommendationRow(
                recId++, stories[storyIdx].Id, recommenderId: null,
                AfterPublish(stories[storyIdx]),
                $"<p>Seed anonymized recommendation — {Phrase(18)}.</p>"));
        }

        // ── Hidden-gem chains (the deep-mode deliverable) ────────────────────────────────────
        // Chain shape: curator u_i holds hidden gems on BOTH s_i and s_(i+1) — so a deep search
        // rooted at s_1 walks s_1 →(gem) u_1 →(gem) s_2 →(gem) u_2 → … reaching s_(k) at degree
        // 2(k-1). Six stories per chain puts the tail at degree 10; degree-5/6 searches surface
        // the middle — exactly the "niche story via curator hops" experience.
        List<int> nicheVisible = [.. Enumerable.Range(0, stories.Count)
            .Where(i => stories[i].IsVisible && stories[i].Popularity < 0.05)];
        List<int> curatorPool = [.. Enumerable.Range(0, options.Users)
            .Where(u => !userIsPowerRecommender[u])];
        Shuffle(nicheVisible);
        Shuffle(curatorPool);

        int chainStories = 6;
        int chainCount = Math.Min(options.HiddenGemChains,
            Math.Min(nicheVisible.Count / chainStories, curatorPool.Count / (chainStories - 1)));
        Dictionary<int, int> gemCountByUser = [];
        int nicheCursor = 0, curatorCursor = 0;
        for (int c = 0; c < chainCount; c++)
        {
            int[] chain = [.. nicheVisible.Skip(nicheCursor).Take(chainStories)];
            nicheCursor += chainStories;
            for (int i = 0; i < chainStories - 1; i++)
            {
                int curator = curatorPool[curatorCursor++];
                MarkGem(curator, chain[i]);
                MarkGem(curator, chain[i + 1]);
            }
        }

        void MarkGem(int userIdx, int storyIdx)
        {
            AddRecommendation(userIdx, storyIdx);
            // AddRecommendation skips self-recs (WU-RecLifecycle invariant) — no row may exist.
            SeedRecommendationRow? rec = recommendations.LastOrDefault(r =>
                r.RecommenderId == bases.UserId + userIdx && r.StoryId == stories[storyIdx].Id);
            if (rec is null || rec.IsHiddenGem) return;
            int count = gemCountByUser.GetValueOrDefault(userIdx);
            if (count >= 5) return; // the ≤5 cap the write services enforce in production
            rec.IsHiddenGem = true;
            gemCountByUser[userIdx] = count + 1;
        }

        // General gem sprinkle for non-chain users (still ≤5 each).
        foreach (IGrouping<int?, SeedRecommendationRow> byUser in recommendations
                     .Where(r => r.RecommenderId is not null).GroupBy(r => r.RecommenderId))
        {
            int userIdx = byUser.Key!.Value - bases.UserId;
            if (gemCountByUser.ContainsKey(userIdx)) continue; // chain curators keep their curated 2
            foreach (SeedRecommendationRow rec in byUser.Take(5))
            {
                if (_rng.NextDouble() < 0.25)
                {
                    rec.IsHiddenGem = true;
                    gemCountByUser[userIdx] = gemCountByUser.GetValueOrDefault(userIdx) + 1;
                }
            }
        }

        // ── Author spotlights (≤5 per story — hidden gem in reverse) ─────────────────────────
        foreach (IGrouping<int, SeedRecommendationRow> byStory in recommendations
                     .Where(r => r.RecommenderId is not null).GroupBy(r => r.StoryId))
        {
            if (_rng.NextDouble() >= 0.40) continue;
            foreach (SeedRecommendationRow rec in byStory.Take(3))
                rec.IsHighlightedByAuthor = true;
        }

        // ── Vouches, biased toward low-volume authors (≤5 per voucher, no self-vouch) ────────
        List<int> lowVolumeAuthors = [.. authorIndexes.Where(a =>
            storyIndexesByAuthor.TryGetValue(a, out List<int>? own) &&
            own.Count(i => stories[i].IsVisible) is 1 or 2)];
        List<SeedVouchRow> vouches = [];
        HashSet<(int, int)> vouchPairs = [];
        for (int u = 0; u < options.Users; u++)
        {
            if (_rng.NextDouble() >= 0.25) continue;
            int vouchCount = 1 + _rng.Next(5);
            for (int k = 0; k < vouchCount; k++)
            {
                int targetIdx = lowVolumeAuthors.Count > 0 && _rng.NextDouble() < 0.70
                    ? lowVolumeAuthors[_rng.Next(lowVolumeAuthors.Count)]
                    : authorIndexes[_rng.Next(authorIndexes.Length)];
                if (targetIdx == u) continue;
                if (!vouchPairs.Add((u, targetIdx))) continue;
                vouches.Add(new SeedVouchRow(bases.UserId + u, bases.UserId + targetIdx,
                    Anchor.AddDays(-_rng.Next(1, 400))));
            }
        }

        // ── Story Acknowledgments (WU-StatBadgeProducers) — author credits on ~15% of stories ──
        // Feeds AcknowledgedAsBetaReaderCount (Accepted, role 1) and the BetaReader badge at
        // volume — per the C4/WU-TagFanon lesson, a counter with no seed generator stays an
        // assertion, never a measurement (index/planner behavior, curation-UI states at scale).
        List<SeedAcknowledgmentRow> acknowledgments = [];
        HashSet<(int Story, int User, short Role)> ackKeys = [];
        foreach (SeedStoryRow story in stories)
        {
            if (_rng.NextDouble() >= 0.15) continue;
            int creditCount = 1 + _rng.Next(3);
            for (int k = 0; k < creditCount; k++)
            {
                int recipientId = bases.UserId + _rng.Next(options.Users);
                if (recipientId == story.AuthorId) continue; // self-credit forbidden (write-service rule)
                short roleId = (short)(1 + _rng.Next(4)); // 1=Beta Reader, 2=Planner, 3=Cover Artist, 4=Editor
                if (!ackKeys.Add((story.Id, recipientId, roleId))) continue;

                DateTime credited = AfterPublish(story);
                double roll = _rng.NextDouble();
                (short statusId, DateTime? responded) = roll switch
                {
                    < 0.60 => ((short)1, (DateTime?)credited.AddDays(1 + _rng.Next(14))), // Accepted
                    < 0.85 => ((short)0, null),                                          // Pending
                    _ => ((short)2, (DateTime?)credited.AddDays(1 + _rng.Next(14))),      // Declined
                };
                acknowledgments.Add(new SeedAcknowledgmentRow(
                    story.Id, recipientId, roleId, statusId, credited, responded));
            }
        }

        // ── Story Lineage "Inspired By" (WU-StatBadgeProducers) — cross-author, ~8% of stories ──
        // Feeds AcknowledgedAsInspirationCount (Approved, type 1) at volume. Cross-author only —
        // a same-author link is real in production but never counts (anti-self-farm guard), so it
        // would add volume without adding anything measurable.
        List<SeedLineageRow> lineages = [];
        HashSet<(int Source, int Target)> lineageKeys = [];
        foreach (SeedStoryRow source in stories)
        {
            if (_rng.NextDouble() >= 0.08) continue;
            SeedStoryRow? target = null;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                SeedStoryRow candidate = stories[_rng.Next(stories.Count)];
                if (candidate.Id == source.Id || candidate.AuthorId == source.AuthorId) continue;
                target = candidate;
                break;
            }
            if (target is null) continue;
            if (!lineageKeys.Add((source.Id, target.Id))) continue;

            double roll = _rng.NextDouble();
            short statusId = roll switch { < 0.55 => 1, < 0.85 => 0, _ => 2 }; // Approved/Pending/Rejected
            lineages.Add(new SeedLineageRow(source.Id, target.Id, statusId, AfterPublish(source)));
        }

        // ── Chapter comments (threaded, popularity-weighted — L6 comment-paging measurability) ──
        // Hub stories accumulate hundreds of comments, the tail a handful — the same power law
        // that makes co-occurrence rankable makes the (chapter_id, date_posted) paging index earn
        // its keep on hubs.
        List<SeedChapterCommentRow> comments = [];
        long commentId = bases.CommentId;
        Dictionary<int, List<SeedChapterRow>> chaptersByStory = chapters
            .GroupBy(c => c.StoryId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (SeedStoryRow story in stories)
        {
            if (!story.IsVisible) continue;
            int commentCount = (int)(story.Popularity * 400) + _rng.Next(5);
            if (commentCount == 0) continue;
            List<SeedChapterRow> storyChapters = chaptersByStory[story.Id];
            List<long> rootsOnChapter = [];
            int lastChapterId = -1;
            for (int k = 0; k < commentCount; k++)
            {
                SeedChapterRow chapter = storyChapters[_rng.Next(storyChapters.Count)];
                if (chapter.Id != lastChapterId) { rootsOnChapter.Clear(); lastChapterId = chapter.Id; }
                int commenter = bases.UserId + _rng.Next(options.Users);
                long? parent = rootsOnChapter.Count > 0 && _rng.NextDouble() < 0.30
                    ? rootsOnChapter[_rng.Next(rootsOnChapter.Count)]
                    : null;
                SeedChapterCommentRow comment = new(
                    commentId++, chapter.Id, commenter, parent,
                    $"<p>Seed comment — {Phrase(8 + _rng.Next(20))}.</p>",
                    story.PublishedUtc.AddDays(_rng.Next(1, 60)).AddMinutes(_rng.Next(1440)),
                    IsSpoiler: _rng.NextDouble() < 0.05);
                comments.Add(comment);
                if (parent is null) rootsOnChapter.Add(comment.Id);
            }
        }

        // ── Tag world (WU-TagFanon): vocabulary, hierarchy, fanon population, overlays ─────────
        // Everything roll-up, the /fanon dashboard, ship filtering, and the adoption flow need:
        // parent/child trees (no seed row EVER set a parent before this — the hierarchy had
        // never run), fanon tags, cross-author OC-name clusters at reach above AND below the
        // dashboard threshold, per-story overlays, pairings, saved selections (F15), per-user
        // notification settings, and one pre-linked cluster exercising the adoption pipeline.
        int tagId = bases.TagId;
        List<SeedTagRow> tagRows = [];
        SeedTagRow AddTag(string name, TagTypeEnum type, bool allowCustom = false, bool isFanon = false,
            int? parentId = null, string? sprite = null)
        {
            SeedTagRow row = new(tagId++, name, type, isFanon, allowCustom, parentId, sprite,
                $"Seed {type} tag: {name}.");
            tagRows.Add(row);
            return row;
        }

        string[] speciesPre = ["Ember", "Tide", "Gale", "Moss", "Frost", "Dusk", "Iron", "Sky",
            "Thorn", "Ridge", "Cinder", "Fen", "Bright", "Hollow", "Pine", "Storm", "Loam",
            "Vale", "Crag", "Reed"];
        string[] speciesSuf = ["fox", "drake", "moth", "finch", "newt", "boar", "lynx", "carp", "wren", "toad"];
        List<SeedTagRow> species = [];
        for (int i = 0; i < 40; i++)
            // The i/20 term shifts the suffix on the second prefix cycle — without it, i and
            // i+20 produce the same (prefix, suffix) pair and violate the tag-name unique index.
            species.Add(AddTag(speciesPre[i % 20] + speciesSuf[(i * 7 + i / 20 + 3) % 10], TagTypeEnum.Character,
                allowCustom: i % 4 != 3)); // 75% are OC bases
        // Specific-canon children (tier 2 of the three-tier model — Entry #1316).
        List<SeedTagRow> canonChildren = [];
        for (int i = 0; i < 10; i++)
            canonChildren.Add(AddTag($"Elder {species[i].Name}", TagTypeEnum.Character,
                parentId: species[i].Id));
        // Fanon children (tier 3): community-established, specific entities — no custom names.
        string[] fanonNames = ["Saurel", "Voltra", "Nyxis", "Runeel", "Sablette", "Talonis",
            "Wispera", "Junofel", "Echolin", "Onyxa", "Fablest", "Skyewin"];
        List<SeedTagRow> fanonChildren = [];
        for (int i = 0; i < 12; i++)
            fanonChildren.Add(AddTag($"{fanonNames[i]} (Seed Saga)", TagTypeEnum.Character,
                isFanon: true, parentId: species[i * 3 % 40].Id));

        string[] settingSuf = ["Reach", "Expanse", "Isles", "Coast", "Basin", "Steppe"];
        List<SeedTagRow> settingTags = [];
        for (int i = 0; i < 12; i++)
            settingTags.Add(AddTag($"{speciesPre[(i * 3 + 1) % 20]} {settingSuf[i % 6]}", TagTypeEnum.Setting,
                allowCustom: i % 3 == 0)); // a third accept custom setting names
        List<SeedTagRow> fanonSettings = [];
        for (int i = 0; i < 2; i++)
            fanonSettings.Add(AddTag($"The {fanonNames[i]} Wilds", TagTypeEnum.Setting,
                isFanon: true, parentId: settingTags[i * 3].Id));

        string[] genreNames = ["Voyage", "Lorekeeping", "Skybound", "Harborlore", "Tidebound",
            "Wandering", "Homefront", "Starcrossed"];
        List<SeedTagRow> genreTags = genreNames.Select(n => AddTag(n, TagTypeEnum.Genre)).ToList();
        string[] cwNames = ["Storm Peril", "Deep Grief", "Old Scars", "Sharp Teeth"];
        List<SeedTagRow> cwTags = cwNames.Select(n => AddTag(n, TagTypeEnum.ContentWarning)).ToList();

        // ── Per-story assignments ──
        int storyCharacterId = bases.StoryCharacterId;
        int pairingId = bases.PairingId;
        List<SeedStoryTagRow> storyTagRows = [];
        List<SeedStoryCharacterRow> characterRows = [];
        List<SeedPairingRow> pairingRows = [];
        List<SeedPairingMemberRow> pairingMemberRows = [];
        string[] regionNames = ["Aethon Region", "Vermeil Coast", "Hollow Vale", "Cindral Basin",
            "The Split Isles", "Old Quay Province"];
        string[] uniqueOcBank = ["Bram", "Lark", "Vex", "Ida", "Corvin", "Mira", "Tolly", "Ferrin",
            "Oswin", "Petra", "Quill", "Sorrel"];

        // OC-name clusters: 14 names on fixed base species, reach deliberately spanning both
        // sides of the dashboard threshold (2 large, 4 medium, 8 small/single-author).
        string[] clusterNames = ["Saura", "Volt", "Nyx", "Rune", "Sable", "Talon", "Wisp", "Juno",
            "Echo", "Onyx", "Fable", "Skye", "Kira", "Pixel"];
        int[] clusterStoryCounts = [18, 12, 6, 6, 5, 5, 2, 2, 1, 1, 1, 1, 2, 1];
        SeedTagRow[] clusterBase = new SeedTagRow[clusterNames.Length];
        for (int c = 0; c < clusterNames.Length; c++)
        {
            // Only AllowCustomName species can host an OC.
            SeedTagRow baseTag;
            do { baseTag = species[SampleZipf(species.Count)]; } while (!baseTag.AllowCustomName);
            clusterBase[c] = baseTag;
        }

        HashSet<(int Story, int Tag, string? Name)> usedCharacterKeys = [];
        void AddCharacterRow(int storyId2, SeedTagRow tag, bool isOc, string? customName, string? nuance)
        {
            (int, int, string?) key = (storyId2, tag.Id, customName?.Trim().ToLowerInvariant());
            if (!usedCharacterKeys.Add(key)) return; // unique (story, tag, name), nulls colliding
            characterRows.Add(new SeedStoryCharacterRow(
                storyCharacterId++, storyId2, tag.Id,
                _rng.NextDouble() < 0.6 ? TagPriority.Primary : TagPriority.Supporting,
                isOc, customName, nuance));
        }
        string? MaybeNuance(double p) => _rng.NextDouble() < p ? Phrase(6 + _rng.Next(10)) : null;

        foreach (SeedStoryRow story in stories)
        {
            // Genres ×2 + setting ×1 (+ overlays), CW 30%, on every story (drafts author-tag too).
            SeedTagRow g1 = genreTags[SampleZipf(genreTags.Count)];
            SeedTagRow g2 = genreTags[(genreTags.IndexOf(g1) + 1 + _rng.Next(genreTags.Count - 1)) % genreTags.Count];
            storyTagRows.Add(new SeedStoryTagRow(story.Id, g1.Id, TagPriority.Primary, null, MaybeNuance(0.10)));
            storyTagRows.Add(new SeedStoryTagRow(story.Id, g2.Id, TagPriority.Supporting, null, null));

            SeedTagRow setting = _rng.NextDouble() < 0.06 && fanonSettings.Count > 0
                ? fanonSettings[_rng.Next(fanonSettings.Count)]
                : settingTags[SampleZipf(settingTags.Count)];
            string? settingCustom = setting.AllowCustomName && _rng.NextDouble() < 0.30
                ? regionNames[_rng.Next(regionNames.Length)]
                : null;
            storyTagRows.Add(new SeedStoryTagRow(story.Id, setting.Id, TagPriority.Primary,
                settingCustom, settingCustom is not null ? MaybeNuance(0.5) : MaybeNuance(0.08)));

            if (_rng.NextDouble() < 0.30)
                storyTagRows.Add(new SeedStoryTagRow(story.Id, cwTags[_rng.Next(cwTags.Count)].Id,
                    TagPriority.Primary, null, MaybeNuance(0.10)));

            // Characters: 1–3 non-cluster rows per story (clusters add theirs below).
            int rowCount = 1 + _rng.Next(3);
            for (int r = 0; r < rowCount; r++)
            {
                double roll = _rng.NextDouble();
                if (roll < 0.62)
                    AddCharacterRow(story.Id, species[SampleZipf(species.Count)], isOc: false, null, MaybeNuance(0.15));
                else if (roll < 0.74)
                    AddCharacterRow(story.Id, canonChildren[_rng.Next(canonChildren.Count)], isOc: false, null, MaybeNuance(0.15));
                else if (roll < 0.86)
                    AddCharacterRow(story.Id, fanonChildren[_rng.Next(fanonChildren.Count)], isOc: false, null, MaybeNuance(0.20));
                else
                {
                    SeedTagRow ocBase = species[SampleZipf(species.Count)];
                    if (!ocBase.AllowCustomName) { AddCharacterRow(story.Id, ocBase, false, null, null); continue; }
                    string ocName = $"{uniqueOcBank[_rng.Next(uniqueOcBank.Length)]}-{_rng.Next(1000):D3}";
                    AddCharacterRow(story.Id, ocBase, isOc: true, ocName, MaybeNuance(0.40));
                }
            }
        }

        // Cluster rows: visible stories only feed public reach; casing varies on ~20% of rows so
        // the case-insensitive normalization is genuinely exercised.
        List<SeedStoryRow> visibleStories = stories.Where(s => s.IsVisible).ToList();
        for (int c = 0; c < clusterNames.Length; c++)
        {
            HashSet<int> chosen = [];
            int want = Math.Min(clusterStoryCounts[c], visibleStories.Count);
            int guard = 0;
            while (chosen.Count < want && guard++ < want * 30)
            {
                SeedStoryRow s = visibleStories[_rng.Next(visibleStories.Count)];
                if (!chosen.Add(s.Id)) continue;
                string name = _rng.NextDouble() < 0.20 ? clusterNames[c].ToLowerInvariant() : clusterNames[c];
                AddCharacterRow(s.Id, clusterBase[c], isOc: true, name, MaybeNuance(0.40));
            }
        }

        // Pairings: ~35% of stories with ≥2 character rows get one (some between same-species
        // custom-named rows — the case tag-id member references could never express).
        foreach (IGrouping<int, SeedStoryCharacterRow> byStory in characterRows.GroupBy(r => r.StoryId))
        {
            List<SeedStoryCharacterRow> rows = [.. byStory];
            if (rows.Count < 2 || _rng.NextDouble() >= 0.35) continue;
            int a = _rng.Next(rows.Count);
            int b = (a + 1 + _rng.Next(rows.Count - 1)) % rows.Count;
            SeedPairingRow pairing = new(pairingId++, byStory.Key,
                _rng.NextDouble() < 0.6 ? CharacterPairingType.Romantic : CharacterPairingType.Platonic,
                TagPriority.Primary);
            pairingRows.Add(pairing);
            pairingMemberRows.Add(new SeedPairingMemberRow(pairing.Id, rows[a].Id));
            pairingMemberRows.Add(new SeedPairingMemberRow(pairing.Id, rows[b].Id));
        }

        // Saved tag selections (F15 — the C4 "no generator, flipped without measurement" half).
        int savedSelectionId = bases.SavedSelectionId;
        int savedSelectionEntryId = bases.SavedSelectionEntryId;
        List<SeedSavedSelectionRow> savedSelections = [];
        List<SeedSavedSelectionEntryRow> savedSelectionEntries = [];
        foreach (SeedUserRow user in users)
        {
            if (_rng.NextDouble() >= 0.25) continue;
            int count = 1 + _rng.Next(2);
            for (int n = 0; n < count; n++)
            {
                SeedSavedSelectionRow sel = new(savedSelectionId++, user.Id, $"Seed selection {n + 1}",
                    _rng.NextDouble() < 0.20,
                    _rng.NextDouble() < 0.30 ? Phrase(8) : null,
                    Anchor.AddDays(-_rng.Next(60)));
                savedSelections.Add(sel);
                HashSet<int> chosenTags = [];
                int entries = 2 + _rng.Next(5);
                for (int e = 0; e < entries; e++)
                {
                    SeedTagRow t = tagRows[_rng.Next(tagRows.Count)];
                    if (!chosenTags.Add(t.Id)) continue;
                    savedSelectionEntries.Add(new SeedSavedSelectionEntryRow(
                        savedSelectionEntryId++, sel.Id, t.Id, _rng.NextDouble() < 0.25));
                }
            }
        }

        // Per-user notification settings (sparse overrides — never seeded anywhere before).
        List<SeedNotificationSettingRow> notificationSettings = [];
        short[] settableTypes = [0, 10, 20, 22, 26, 32];
        foreach (SeedUserRow user in users)
        {
            if (_rng.NextDouble() >= 0.20) continue;
            HashSet<short> chosenTypes = [];
            int count = 1 + _rng.Next(3);
            for (int n = 0; n < count; n++)
            {
                short t = settableTypes[_rng.Next(settableTypes.Length)];
                if (!chosenTypes.Add(t)) continue;
                notificationSettings.Add(new SeedNotificationSettingRow(
                    user.Id, t, _rng.NextDouble() < 0.30, _rng.NextDouble() < 0.30));
            }
        }

        // Pre-linked fanon cluster (cluster[1], "Volt"): a moderator already linked it to a new
        // fanon tag and notified the then-current authors — the adoption pipeline is exercisable
        // against seed data end to end (index page, per-tag page, dismiss, adopt).
        SeedTagRow adoptionTarget = AddTag($"{clusterNames[1]} (Silver Saga)", TagTypeEnum.Character,
            isFanon: true, parentId: clusterBase[1].Id);
        string linkedNormalized = clusterNames[1].ToLowerInvariant();
        List<SeedFanonLinkRow> fanonLinks =
        [
            new(bases.FanonLinkId, linkedNormalized, clusterBase[1].Id, adoptionTarget.Id,
                LinkedByUserId: null, Anchor.AddDays(-5))
        ];
        Dictionary<int, int> authorByStory = stories.ToDictionary(s => s.Id, s => s.AuthorId);
        List<int> linkedAuthors = characterRows
            .Where(r => r.CharacterTagId == clusterBase[1].Id
                && r.CustomName is not null
                && r.CustomName.Trim().ToLowerInvariant() == linkedNormalized)
            .Select(r => authorByStory[r.StoryId])
            .Distinct()
            .ToList();
        List<SeedAdoptionStateRow> adoptionStates = linkedAuthors
            .Select(a => new SeedAdoptionStateRow(a, adoptionTarget.Id, Anchor.AddDays(-5),
                IsDismissed: _rng.NextDouble() < 0.10))
            .ToList();

        // ── Notifications (derived from real actions, so recipients/types are semantically sane) ─
        List<SeedNotificationRow> notifications = [];
        long notificationId = bases.NotificationId;
        void Notify(int recipient, int? source, short typeId, int relatedEntityId, DateTime when)
        {
            if (source == recipient) return;
            notifications.Add(new SeedNotificationRow(
                notificationId++, recipient, source, typeId, relatedEntityId,
                IsRead: _rng.NextDouble() < 0.70, when));
        }
        const short NewStoryFavorite = 20, NewRecommendationOnYourStory = 22, NewVouchOnYou = 32;
        foreach (SeedInteractionRow row in interactions.Values)
        {
            if (!row.IsFavorite || row.FavoriteDateUtc is not DateTime favoriteDate) continue;
            if (_rng.NextDouble() >= 0.40) continue; // sample — not every favorite notifies
            Notify(stories[row.StoryId - bases.StoryId].AuthorId, row.UserId, NewStoryFavorite, row.StoryId, favoriteDate);
        }
        foreach (SeedRecommendationRow rec in recommendations.Where(r => r.RecommenderId is not null))
            Notify(stories[rec.StoryId - bases.StoryId].AuthorId, rec.RecommenderId, NewRecommendationOnYourStory,
                rec.StoryId, rec.DatePostedUtc);
        foreach (SeedVouchRow vouch in vouches)
            Notify(vouch.VouchedUserId, vouch.VouchingUserId, NewVouchOnYou, vouch.VouchingUserId, vouch.DateUtc);

        // WU-StatBadgeProducers: NewStoryAcknowledgement (52) fires for every credit (mirrors
        // RequestAcknowledgmentAsync, which always notifies on request regardless of eventual
        // Accept/Decline outcome).
        const short NewStoryAcknowledgement = 52;
        foreach (SeedAcknowledgmentRow ack in acknowledgments)
            Notify(ack.AcknowledgedUserId, authorByStory[ack.StoryId], NewStoryAcknowledgement,
                ack.StoryId, ack.DateAcknowledgedUtc);

        // WU-StatBadgeProducers: StoryLineageRequested (50) fires for every link (a request always
        // happens first); StoryLineageApproved (51) additionally fires for the Approved subset —
        // mirrors ApproveLineageAsync notifying the source author back.
        const short StoryLineageRequested = 50, StoryLineageApproved = 51;
        foreach (SeedLineageRow link in lineages)
        {
            Notify(authorByStory[link.TargetStoryId], authorByStory[link.SourceStoryId],
                StoryLineageRequested, link.SourceStoryId, link.DateCreatedUtc);
            if (link.StatusId == 1) // Approved
                Notify(authorByStory[link.SourceStoryId], authorByStory[link.TargetStoryId],
                    StoryLineageApproved, link.TargetStoryId, link.DateCreatedUtc.AddDays(1 + _rng.Next(5)));
        }

        // Type-26 adoption invitations for the pre-linked fanon cluster (WU-TagFanon) — one per
        // notified author, RelatedEntityId = the target tag, matching NotifyTagAdoptionSuggestedAsync.
        const short TagUpdateSuggestion = 26;
        foreach (SeedAdoptionStateRow state in adoptionStates.Where(s => s.DateNotifiedUtc is not null))
            notifications.Add(new SeedNotificationRow(
                notificationId++, state.UserId, SourceUserId: null, TagUpdateSuggestion,
                state.TargetTagId, IsRead: _rng.NextDouble() < 0.5, state.DateNotifiedUtc!.Value));

        return new SeedGraph
        {
            Users = users,
            Stories = stories,
            Chapters = chapters,
            ChapterContents = contents,
            Interactions = [.. interactions.Values],
            Recommendations = recommendations,
            Vouches = vouches,
            Acknowledgments = acknowledgments,
            Lineages = lineages,
            ChapterComments = comments,
            Notifications = notifications,
            HiddenGemChainCount = chainCount,
            Tags = tagRows,
            StoryTags = storyTagRows,
            StoryCharacters = characterRows,
            Pairings = pairingRows,
            PairingMembers = pairingMemberRows,
            SavedSelections = savedSelections,
            SavedSelectionEntries = savedSelectionEntries,
            NotificationSettings = notificationSettings,
            FanonLinks = fanonLinks,
            AdoptionStates = adoptionStates,
        };
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Zipf-ish rank sample in [0, n): rank r with weight 1/(r+1) — the power-law knob
    /// behind community sizes, author output, and story popularity.</summary>
    private int SampleZipf(int n)
    {
        if (n <= 1) return 0;
        double total = 0;
        for (int i = 1; i <= n; i++) total += 1.0 / i;
        double roll = _rng.NextDouble() * total, cumulative = 0;
        for (int i = 1; i <= n; i++)
        {
            cumulative += 1.0 / i;
            if (roll <= cumulative) return i - 1;
        }
        return n - 1;
    }

    /// <summary>85% a popularity-weighted draw from the user's own communities, 15% anywhere —
    /// the taste-community clustering that makes co-occurrence rankable.</summary>
    private int PickClusteredStory(
        int userIdx, int[] primary, int[] secondary, List<List<int>> byCommunity, List<SeedStoryRow> stories)
    {
        List<int> pool;
        if (_rng.NextDouble() < 0.85)
        {
            int community = secondary[userIdx] >= 0 && _rng.NextDouble() < 0.30
                ? secondary[userIdx]
                : primary[userIdx];
            pool = byCommunity[community];
        }
        else
        {
            pool = byCommunity[_rng.Next(byCommunity.Count)];
        }
        if (pool.Count == 0) return -1;

        // Popularity-weighted pick: a few probes, keep the most popular — cheap hub bias.
        int best = pool[_rng.Next(pool.Count)];
        for (int probe = 0; probe < 2; probe++)
        {
            int candidate = pool[_rng.Next(pool.Count)];
            if (stories[candidate].Popularity > stories[best].Popularity) best = candidate;
        }
        return best;
    }

    private DateTime AfterPublish(SeedStoryRow story) =>
        story.PublishedUtc.AddDays(_rng.Next(1, 30)).AddMinutes(_rng.Next(1440));

    private string Phrase(int words) =>
        string.Join(' ', Enumerable.Range(0, words).Select(_ => WordBank[_rng.Next(WordBank.Length)]));

    private string ChapterHtml(int storyId, int chapterNumber)
    {
        int paragraphs = 2 + _rng.Next(3);
        IEnumerable<string> body = Enumerable.Range(0, paragraphs)
            .Select(p => $"<p>Seed chapter body (story {storyId}, chapter {chapterNumber}, paragraph {p + 1}): {Phrase(40 + _rng.Next(40))}.</p>");
        return string.Concat(body);
    }

    private void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

/// <summary>Volume/shape knobs (all deterministic given <see cref="Seed"/>).</summary>
public sealed record SeedToolOptions
{
    public required string ConnectionString { get; init; }
    public int Seed { get; init; } = 1337;
    public int Users { get; init; } = 2000;
    public int Stories { get; init; } = 3000;
    public int Communities { get; init; } = 8;
    public int HiddenGemChains { get; init; } = 12;

    /// <summary>Upper spread of the per-user favorite-count power law (5 + zipf(spread)).</summary>
    public int FavoritesPerUserSpread { get; init; } = 55;
}

/// <summary>Starting IDs (MAX+1 read from the target database) so the tool composes with an
/// existing Full/Minimal dev seed instead of colliding with it.</summary>
public sealed record SeedIdBases(
    int UserId, int StoryId, int ChapterId, long ChapterContentId, int RecommendationId,
    long CommentId, long NotificationId,
    int TagId, int StoryCharacterId, int PairingId, int SavedSelectionId, int SavedSelectionEntryId,
    int FanonLinkId);
