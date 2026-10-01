using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// One resolved notification target: the related entity's display <paramref name="Title"/> and
/// deep-link <paramref name="Url"/>, plus an optional <paramref name="ContextTitle"/> — a second name
/// the same anchor yields for free (the story behind a <c>GroupStory</c> or <c>Chapter</c> anchor).
/// </summary>
public readonly record struct NotificationTarget(string? Title, string? Url, string? ContextTitle);

/// <summary>
/// Resolves each notification's polymorphic <c>RelatedEntityId</c> to a display title and target
/// URL — the two-pass batch enrichment described in <c>layer2-services.md</c>
/// §"Polymorphic RelatedEntityId — Two-Pass Batch Enrichment." One query per entity kind actually
/// present in the input; kinds absent produce no query.
///
/// <para><b>Extracted from <see cref="ServerNotificationReadService"/> at WU-NotifEmail
/// (2026-07-31)</b> so the notification-email flusher can produce the same titles and links the
/// in-app panel shows. The extraction is deliberately <b>recipient-agnostic</b>: the read service's
/// <c>GetNotificationsAsync</c> is scoped to the active user via <c>IActiveUserContext</c>, but a
/// background worker has no active user. Nothing here consults the viewer.</para>
///
/// <para><b>Do not fork this switch.</b> <see cref="KindFor"/> has an arm per notification type and
/// grows every time a type is minted; a second copy would drift silently, surfacing as
/// title-less notifications in whichever consumer was not updated.</para>
///
/// <para><b>Ids are <c>long</c>, keys are <c>int</c> (WU-InertFeatures, 2026-09-30).</b>
/// <c>RelatedEntityId</c> is <c>bigint</c> (report ids), but every kind resolved here has an
/// <c>int</c> primary key, so each kind's id set is narrowed to <c>int</c> before its query — an id
/// above <c>int.MaxValue</c> simply misses — and the SQL compares <c>int = int</c>.</para>
///
/// <para><b>Plane note (unchanged by the extraction):</b> enrichment resolves <em>ground truth</em>
/// and is never rating- or audience-filtered — notifications are Personal-plane, referencing things
/// the recipient already interacted with (<c>content-safety.md</c> §"The Three-Plane Access
/// Model"). The one exception preserved verbatim below is the <c>IsTakenDown</c> filter on blog
/// posts, which stays active on purpose so moderated content degrades to a title-less,
/// non-navigating notification.</para>
/// </summary>
public static class NotificationEnricher
{
    /// <summary>
    /// Internal enum classifying what entity table a notification's <c>RelatedEntityId</c>
    /// references. Used by <see cref="KindFor"/> and <see cref="BatchLoadEntitiesAsync"/>.
    /// Two blog-post kinds exist deliberately: <c>BlogPost</c> resolves via <c>GroupBlogPosts</c>
    /// and links to the *group* (<c>/group/{GroupId}</c> — NewGroupBlogPost's chosen target);
    /// <c>BlogPostDirect</c> resolves via the TPT-root <c>BlogPosts</c> DbSet and links to the
    /// *post* (<c>/blog/{id}</c>, the unified BlogPostPage route serving both post kinds).
    /// <c>GroupStory</c> (owner ruling D16) resolves the junction row to the group (title + link) and
    /// the story (context title).
    /// </summary>
    private enum RelatedEntityKind { None, User, Story, Chapter, Group, GroupStory, BlogPost, BlogPostDirect, Tag }

    /// <summary>
    /// Batch-resolves <c>(type, relatedEntityId)</c> pairs to their targets.
    /// Pairs whose type has no navigable target, or whose entity no longer resolves (deleted,
    /// taken down), are simply absent from the returned dictionary — callers treat a miss as
    /// "no title, no link," which every consumer already renders gracefully.
    /// </summary>
    /// <param name="readDb">
    /// A no-tracking read context. Callers pass their own short-lived context and use it
    /// sequentially — see <c>layer2-services.md</c> §"Read-context concurrency: factory per method."
    /// </param>
    /// <param name="pairs">One entry per notification being enriched. Duplicates are fine.</param>
    public static async Task<Dictionary<(NotificationTypeEnum TypeId, long RelatedEntityId), NotificationTarget>>
        ResolveTargetsAsync(
            ReadOnlyApplicationDbContext readDb,
            IReadOnlyList<(NotificationTypeEnum TypeId, long RelatedEntityId)> pairs)
    {
        Dictionary<RelatedEntityKind, Dictionary<int, NotificationTarget>> kindLookups =
            await BatchLoadEntitiesAsync(readDb, pairs);

        var resolved = new Dictionary<(NotificationTypeEnum, long), NotificationTarget>();
        foreach ((NotificationTypeEnum typeId, long relatedEntityId) in pairs)
        {
            if (resolved.ContainsKey((typeId, relatedEntityId))) continue;
            if (!TryNarrow(relatedEntityId, out int id)) continue;

            if (kindLookups.TryGetValue(KindFor(typeId), out var byId) &&
                byId.TryGetValue(id, out NotificationTarget target))
            {
                resolved[(typeId, relatedEntityId)] = target;
            }
        }

        return resolved;
    }

    /// <summary>
    /// Every resolvable kind has an <c>int</c> key: an id outside the <c>int</c> range cannot name
    /// one, so it misses instead of overflowing.
    /// </summary>
    private static bool TryNarrow(long relatedEntityId, out int id)
    {
        if (relatedEntityId is >= int.MinValue and <= int.MaxValue)
        {
            id = (int)relatedEntityId;
            return true;
        }
        id = 0;
        return false;
    }

    /// <summary>
    /// Maps each <see cref="NotificationTypeEnum"/> to the kind of entity its
    /// <c>RelatedEntityId</c> references. Derived from the <c>CreateCoreAsync</c> call-sites
    /// in <see cref="ServerNotificationWriteService"/> — those are the authoritative source of
    /// what each semantic method stores in <c>RelatedEntityId</c>.
    ///
    /// <para>Types whose generating write-path is not yet implemented (tracker B24) are stubbed with
    /// the kind their future implementation is expected to store; they produce no DB rows until the
    /// triggering work-unit lands, but the branch exists for forward-compat.</para>
    /// </summary>
    private static RelatedEntityKind KindFor(NotificationTypeEnum type) => type switch
    {
        // ── Implemented WU22: RelatedEntityId = follower's / voucher's user id ──
        NotificationTypeEnum.NewFollowerOnYou => RelatedEntityKind.User,
        NotificationTypeEnum.NewVouchOnYou    => RelatedEntityKind.User,

        // ── Implemented WU29: RelatedEntityId = recommender's user id ────────────
        NotificationTypeEnum.HiddenGem        => RelatedEntityKind.User,

        // ── Implemented WU32, re-anchored WU-InertFeatures (D16): RelatedEntityId = the
        // GroupStory junction row — title = group name, link = the group (unchanged), context
        // title = the story. A removed GroupStory row misses → title-less (accepted by D16). ──
        NotificationTypeEnum.NewGroupStory          => RelatedEntityKind.GroupStory,
        NotificationTypeEnum.YourStoryAddedToGroup  => RelatedEntityKind.GroupStory,
        NotificationTypeEnum.NewGroupBlogPost        => RelatedEntityKind.BlogPost,

        // ── Implemented WU-Spotlight: RelatedEntityId = spotlighted story id ─────
        // (SpotlightSlotGranted carries no entity — falls to None below; the redemption
        // page is a fixed route, not an entity link.)
        NotificationTypeEnum.StorySpotlighted          => RelatedEntityKind.Story,
        NotificationTypeEnum.RecommendationSpotlighted => RelatedEntityKind.Story,

        // ── Implemented WU-Polls: RelatedEntityId = owning blog post id for blog-post
        // polls (navigates to the post); site polls carry 0 → no dictionary match → null
        // title/url, which the display renders as a non-navigating notification.
        // Remapped BlogPost → BlogPostDirect in WU-B2: polls attach to *profile* blog posts,
        // which the group-only BlogPost lookup could never resolve (title-less notifications). ──
        NotificationTypeEnum.PollUpdated => RelatedEntityKind.BlogPostDirect,

        // ── Implemented WU-B2 (2026-07-25): comment + profile-blog generation lives ──
        // RelatedEntityId: chapterId for NewStoryComment (deep-link to the commented chapter);
        // blogPostId for NewCommentOnBlog + the four followed-content blog types (13–16);
        // profileOwnerId for NewCommentOnYourProfile. CommentReply stays None below: it stores the
        // context entity's id, and one cross-context type cannot map to one table (re-pointing it at
        // the comment via the BaseComments TPT root is on D16's conformance list, tracker B23). ──
        NotificationTypeEnum.NewStoryComment                 => RelatedEntityKind.Chapter,
        NotificationTypeEnum.NewCommentOnBlog                => RelatedEntityKind.BlogPostDirect,
        NotificationTypeEnum.NewCommentOnYourProfile         => RelatedEntityKind.User,
        NotificationTypeEnum.NewBlogPostByFollowedUser       => RelatedEntityKind.BlogPostDirect,
        NotificationTypeEnum.NewBlogPostOnFollowedStory      => RelatedEntityKind.BlogPostDirect,
        NotificationTypeEnum.NewBlogPostOnFavoritedStory     => RelatedEntityKind.BlogPostDirect,
        NotificationTypeEnum.NewBlogPostOnReadItLaterStory   => RelatedEntityKind.BlogPostDirect,

        // ── Implemented WU-InertFeatures: RelatedEntityId = the new chapter (the Chapter kind
        // also yields the story title as context). ─────────────────────────────────────
        NotificationTypeEnum.NewChapterOnFollowedStory       => RelatedEntityKind.Chapter,

        // ── Forward-compat stubs (no rows until triggering work-units land — tracker B24) ──
        NotificationTypeEnum.NewStoryByFollowedUser          => RelatedEntityKind.Story,
        NotificationTypeEnum.NewRecommendationByFollowedUser => RelatedEntityKind.Story,
        NotificationTypeEnum.NewStoryFavorite                => RelatedEntityKind.Story,
        NotificationTypeEnum.NewStoryFollower                => RelatedEntityKind.Story,
        NotificationTypeEnum.RecommendationHighlighted       => RelatedEntityKind.Story,
        NotificationTypeEnum.SuccessfulRec                   => RelatedEntityKind.Story,

        // ── Implemented WU-RecLifecycle (submit / revise / revision request / unblock) ──
        NotificationTypeEnum.NewRecommendationOnYourStory    => RelatedEntityKind.Story,
        NotificationTypeEnum.RecommendationApproved          => RelatedEntityKind.Story,
        NotificationTypeEnum.RecommendationRevisionRequested => RelatedEntityKind.Story,
        NotificationTypeEnum.RecommendationRevised           => RelatedEntityKind.Story,

        // ── Implemented: lineage (WU42), acknowledgement (WU-StatBadgeProducers), and the
        // submission outcomes (WU34) — RelatedEntityId = the story. ─────────────────────
        NotificationTypeEnum.StoryLineageRequested            => RelatedEntityKind.Story,
        NotificationTypeEnum.StoryLineageApproved             => RelatedEntityKind.Story,
        NotificationTypeEnum.NewStoryAcknowledgement         => RelatedEntityKind.Story,
        NotificationTypeEnum.StoryRejected                   => RelatedEntityKind.Story,
        NotificationTypeEnum.StoryApproved                   => RelatedEntityKind.Story,

        // ── Implemented WU-TagFanon: RelatedEntityId = the official tag the author is invited
        // to adopt; deep-links to the per-tag adoption page. ─────────────────────────────
        NotificationTypeEnum.TagUpdateSuggestion => RelatedEntityKind.Tag,

        // ── Implemented WU39 (Feature 53): RelatedEntityId = storyId for the per-link tier;
        // the account tier carries 0 (Settings is a fixed route, not an entity) — falls to
        // None below. ─────────────────────────────────────────────────────────────────────
        NotificationTypeEnum.ExternalLinkVerified => RelatedEntityKind.Story,
        NotificationTypeEnum.ExternalLinkRejected => RelatedEntityKind.Story,

        // ── No navigable target: site announcements, account actions, and the report-anchored
        // types (70/80/81/82 carry the report id — D4 — which is a dedup anchor, not a link a
        // reporter or author may follow). ──
        _ => RelatedEntityKind.None
    };

    /// <summary>
    /// For each distinct <see cref="RelatedEntityKind"/> present in
    /// <paramref name="typeIdPairs"/>, queries the relevant table by the id-set found in the
    /// input, returning one dictionary per kind.
    ///
    /// <para><see cref="RelatedEntityKind.None"/> produces no query.</para>
    /// </summary>
    private static async Task<Dictionary<RelatedEntityKind, Dictionary<int, NotificationTarget>>>
        BatchLoadEntitiesAsync(
            ReadOnlyApplicationDbContext readDb,
            IReadOnlyList<(NotificationTypeEnum TypeId, long RelatedEntityId)> typeIdPairs)
    {
        // Classify each row's kind and group ids per kind — skip None entirely, and narrow each id
        // to the int key every kind uses (an out-of-range id cannot name an entity).
        var idsByKind = typeIdPairs
            .GroupBy(p => KindFor(p.TypeId))
            .Where(g => g.Key != RelatedEntityKind.None)
            .ToDictionary(
                g => g.Key,
                g => g.Select(p => TryNarrow(p.RelatedEntityId, out int id) ? (int?)id : null)
                      .OfType<int>()
                      .ToHashSet());

        var result = new Dictionary<RelatedEntityKind, Dictionary<int, NotificationTarget>>();

        if (idsByKind.TryGetValue(RelatedEntityKind.Story, out var storyIds) && storyIds.Count > 0)
        {
            result[RelatedEntityKind.Story] = (await readDb.StoryListings
                    .Where(s => storyIds.Contains(s.StoryId))
                    .Select(s => new { s.StoryId, s.StoryTitle })
                    .ToListAsync())
                .ToDictionary(
                    s => s.StoryId,
                    s => new NotificationTarget(s.StoryTitle, $"/story/{s.StoryId}", null));
        }

        if (idsByKind.TryGetValue(RelatedEntityKind.Chapter, out var chapterIds) && chapterIds.Count > 0)
        {
            // The story title rides along as context (type 10: "New chapter of {story}: {chapter}").
            result[RelatedEntityKind.Chapter] = (await (
                    from c in readDb.Chapters
                    where chapterIds.Contains(c.ChapterId)
                    join s in readDb.StoryListings on c.StoryId equals s.StoryId into stories
                    from s in stories.DefaultIfEmpty()
                    select new { c.ChapterId, c.Title, c.StoryId, c.ChapterNumber, StoryTitle = (string?)s.StoryTitle })
                    .ToListAsync())
                .ToDictionary(
                    c => c.ChapterId,
                    c => new NotificationTarget(c.Title, $"/story/{c.StoryId}/{c.ChapterNumber}", c.StoryTitle));
        }

        if (idsByKind.TryGetValue(RelatedEntityKind.User, out var userIds) && userIds.Count > 0)
        {
            result[RelatedEntityKind.User] = (await readDb.Users
                    .Where(u => userIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.UserName })
                    .ToListAsync())
                .ToDictionary(
                    u => u.Id,
                    u => new NotificationTarget(u.UserName ?? "Unknown User", $"/user/{u.Id}", null));
        }

        if (idsByKind.TryGetValue(RelatedEntityKind.Group, out var groupIds) && groupIds.Count > 0)
        {
            // elevated read: notifications are Personal-plane — they reference things the
            // recipient interacted with, so enrichment resolves ground truth and is never
            // rating/audience-filtered (content-safety.md §"The Three-Plane Access Model").
            // Without the bypass, an M-audience group's name dropped out of its own member's
            // notifications while the sibling post-title lookup (unfiltered GroupBlogPosts)
            // kept working — normalized toward ground truth, WU-AccessGate Phase 1.
            result[RelatedEntityKind.Group] = (await readDb.Groups
                    .IgnoreQueryFilters(["GroupAudience"])
                    .Where(g => groupIds.Contains(g.GroupId))
                    .Select(g => new { g.GroupId, g.GroupName })
                    .ToListAsync())
                .ToDictionary(
                    g => g.GroupId,
                    g => new NotificationTarget(g.GroupName, $"/group/{g.GroupId}", null));
        }

        if (idsByKind.TryGetValue(RelatedEntityKind.GroupStory, out var groupStoryIds) && groupStoryIds.Count > 0)
        {
            // D16: one id names the pairing. Explicit joins (not navigations) so the group read can
            // carry the same elevated GroupAudience bypass as the Group kind above — Personal plane.
            // The link target stays the group, as it was when these types carried the group id.
            result[RelatedEntityKind.GroupStory] = (await (
                    from gs in readDb.GroupStories
                    where groupStoryIds.Contains(gs.GroupStoryId)
                    join g in readDb.Groups.IgnoreQueryFilters(["GroupAudience"]) on gs.GroupId equals g.GroupId
                    join s in readDb.StoryListings on gs.StoryId equals s.StoryId into stories
                    from s in stories.DefaultIfEmpty()
                    select new { gs.GroupStoryId, g.GroupId, g.GroupName, StoryTitle = (string?)s.StoryTitle })
                    .ToListAsync())
                .ToDictionary(
                    x => x.GroupStoryId,
                    x => new NotificationTarget(x.GroupName, $"/group/{x.GroupId}", x.StoryTitle));
        }

        if (idsByKind.TryGetValue(RelatedEntityKind.BlogPost, out var blogPostIds) && blogPostIds.Count > 0)
        {
            // Group-scoped kind (NewGroupBlogPost only): links to the GROUP, not the post —
            // that type's chosen navigation target. Post-scoped types use BlogPostDirect below.
            result[RelatedEntityKind.BlogPost] = (await readDb.GroupBlogPosts
                    .Where(b => blogPostIds.Contains(b.BlogPostId))
                    .Select(b => new { b.BlogPostId, b.Title, b.GroupId })
                    .ToListAsync())
                .ToDictionary(
                    b => b.BlogPostId,
                    b => new NotificationTarget(b.Title, $"/group/{b.GroupId}", null));
        }

        if (idsByKind.TryGetValue(RelatedEntityKind.BlogPostDirect, out var directPostIds) && directPostIds.Count > 0)
        {
            // TPT-root lookup (WU-B2): resolves BOTH post kinds; /blog/{id} is the unified
            // BlogPostPage route. No IgnoreQueryFilters — blog posts carry no audience/rating
            // global filter (rating is an explicit .Where in the blog read service), and the
            // IsTakenDown named filter stays active deliberately: a taken-down post drops out
            // → null target → the presenter's graceful no-title fallback.
            result[RelatedEntityKind.BlogPostDirect] = (await readDb.BlogPosts
                    .Where(b => directPostIds.Contains(b.BlogPostId))
                    .Select(b => new { b.BlogPostId, b.Title })
                    .ToListAsync())
                .ToDictionary(
                    b => b.BlogPostId,
                    b => new NotificationTarget(b.Title, $"/blog/{b.BlogPostId}", null));
        }

        if (idsByKind.TryGetValue(RelatedEntityKind.Tag, out var tagIds) && tagIds.Count > 0)
        {
            // WU-TagFanon: the adoption invitation deep-links to the author's per-tag page.
            result[RelatedEntityKind.Tag] = (await readDb.Tags
                    .Where(t => tagIds.Contains(t.TagId))
                    .Select(t => new { t.TagId, t.TagName })
                    .ToListAsync())
                .ToDictionary(
                    t => t.TagId,
                    t => new NotificationTarget(t.TagName, $"/tag-adoptions/{t.TagId}", null));
        }

        return result;
    }
}
