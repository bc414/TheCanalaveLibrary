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

public sealed class SeedGraph
{
    public required List<SeedUserRow> Users { get; init; }
    public required List<SeedStoryRow> Stories { get; init; }
    public required List<SeedChapterRow> Chapters { get; init; }
    public required List<SeedChapterContentRow> ChapterContents { get; init; }
    public required List<SeedInteractionRow> Interactions { get; init; }
    public required List<SeedRecommendationRow> Recommendations { get; init; }
    public required List<SeedVouchRow> Vouches { get; init; }
    public required int HiddenGemChainCount { get; init; }
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
            SeedRecommendationRow rec = recommendations.Last(r =>
                r.RecommenderId == bases.UserId + userIdx && r.StoryId == stories[storyIdx].Id);
            if (rec.IsHiddenGem) return;
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

        return new SeedGraph
        {
            Users = users,
            Stories = stories,
            Chapters = chapters,
            ChapterContents = contents,
            Interactions = [.. interactions.Values],
            Recommendations = recommendations,
            Vouches = vouches,
            HiddenGemChainCount = chainCount,
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
    int UserId, int StoryId, int ChapterId, long ChapterContentId, int RecommendationId);
