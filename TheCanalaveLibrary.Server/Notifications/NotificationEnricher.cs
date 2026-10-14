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
/// <para><b>Plane note:</b> enrichment is never rating- or audience-filtered — notifications are
/// Personal-plane, referencing things the recipient already interacted with
/// (<c>content-safety.md</c> §"The Three-Plane Access Model"). Class-A confidentiality is different:
/// it binds every recipient who is not the content's author. So the blog-post kinds (tracker D8) and
/// the story-family kinds (tracker D21) split by recipient: an <c>Own*</c> kind's recipient is always
/// the author and resolves the content as stored (a story-family one keeps a taken-down story's title
/// but not its link, which would 404 for the author too), and its sibling drops content its
/// recipients can no longer open (an unpublished post or story, a Private author's profile post, a
/// taken-down post or story, an unpublished chapter). A dropped target is a miss, so the
/// notification degrades to its title-less, non-navigating copy. The split is by notification type,
/// never by viewer, which is what keeps this class recipient-agnostic (<c>layer2-services.md</c>
/// §"Polymorphic RelatedEntityId").</para>
/// </summary>
public static class NotificationEnricher
{
    /// <summary>
    /// Internal enum classifying what entity table a notification's <c>RelatedEntityId</c>
    /// references. Used by <see cref="KindFor"/> and <see cref="BatchLoadEntitiesAsync"/>.
    /// Three blog-post kinds exist deliberately: <c>BlogPost</c> resolves via <c>GroupBlogPosts</c>
    /// and links to the *group* (<c>/group/{GroupId}</c> — NewGroupBlogPost's chosen target);
    /// <c>BlogPostDirect</c> and <c>OwnBlogPost</c> resolve via the TPT-root <c>BlogPosts</c> DbSet
    /// and link to the *post* (<c>/blog/{id}</c>, the unified BlogPostPage route serving every post
    /// kind). They split by recipient (tracker D8): <c>OwnBlogPost</c>'s recipient is the post's
    /// author and resolves the post as stored; <c>BlogPostDirect</c>'s recipients are never the
    /// author, so it drops a post they can no longer open.
    /// <c>GroupStory</c> (owner ruling D16) resolves the junction row to the group (title + link) and
    /// the story (context title).
    /// The story-family kinds split the same way (tracker D21): <c>OwnStory</c>, <c>OwnChapter</c> and
    /// <c>OwnGroupStory</c> serve types whose recipient is always the story's author and resolve the
    /// story as stored; <c>Story</c>, <c>Chapter</c> and <c>GroupStory</c> serve everyone else and drop
    /// a story outside the public window or taken down (and, for <c>Chapter</c>, an unpublished
    /// chapter). A type belongs in an <c>Own*</c> kind only when its recipient is always the author;
    /// when in doubt, the filtered kind — a wrong <c>Own*</c> arm leaks, a wrong filtered arm only costs
    /// an author their own title.
    /// </summary>
    private enum RelatedEntityKind
    {
        None, User, Story, OwnStory, Chapter, OwnChapter, Group, GroupStory, OwnGroupStory,
        BlogPost, BlogPostDirect, OwnBlogPost, Tag
    }

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
        // title = the story. A removed GroupStory row misses → title-less (accepted by D16).
        // Split by recipient (tracker D21): 60 goes to members, never the story's author (they get
        // 25), so its story title drops while the story is unpublished or taken down. ──
        NotificationTypeEnum.NewGroupStory          => RelatedEntityKind.GroupStory,
        NotificationTypeEnum.YourStoryAddedToGroup  => RelatedEntityKind.OwnGroupStory,
        NotificationTypeEnum.NewGroupBlogPost        => RelatedEntityKind.BlogPost,

        // ── Implemented WU-UserDeletion (D13 edge (b)): RelatedEntityId = the group the recipient was
        // made an admin of — title = group name, link = the group (the audience-bypassing Group kind). ──
        NotificationTypeEnum.GroupAdminAssigned      => RelatedEntityKind.Group,

        // ── Implemented WU-Spotlight: RelatedEntityId = spotlighted story id ─────
        // (SpotlightSlotGranted carries no entity — falls to None below; the redemption
        // page is a fixed route, not an entity link.) 91 goes to the story's author; 92 to the
        // recommender, who is never the author (self-recommendation is refused). ──
        NotificationTypeEnum.StorySpotlighted          => RelatedEntityKind.OwnStory,
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
        // the comment via the BaseComments TPT root is on D16's conformance list, tracker B23).
        // 24's recipient is always the story's author (tracker D21's split): OwnChapter. ──
        NotificationTypeEnum.NewStoryComment                 => RelatedEntityKind.OwnChapter,
        // 33's recipient is always the post's author (tracker D8's type split): OwnBlogPost.
        NotificationTypeEnum.NewCommentOnBlog                => RelatedEntityKind.OwnBlogPost,
        NotificationTypeEnum.NewCommentOnYourProfile         => RelatedEntityKind.User,
        NotificationTypeEnum.NewBlogPostByFollowedUser       => RelatedEntityKind.BlogPostDirect,
        NotificationTypeEnum.NewBlogPostOnFollowedStory      => RelatedEntityKind.BlogPostDirect,
        NotificationTypeEnum.NewBlogPostOnFavoritedStory     => RelatedEntityKind.BlogPostDirect,
        NotificationTypeEnum.NewBlogPostOnReadItLaterStory   => RelatedEntityKind.BlogPostDirect,

        // ── Implemented WU-InertFeatures: RelatedEntityId = the new chapter (the Chapter kind
        // also yields the story title as context). Followers only — the author is the source and
        // drops out — so the filtered Chapter kind (tracker D21). ─────────────────────────
        NotificationTypeEnum.NewChapterOnFollowedStory       => RelatedEntityKind.Chapter,

        // ── Forward-compat stubs (no rows until triggering work-units land — tracker B24).
        // Classified by the recipient each type names: 20/21 tell the story's author; 11/12 tell
        // followers and 41/42 the recommender, never the author. ──
        NotificationTypeEnum.NewStoryByFollowedUser          => RelatedEntityKind.Story,
        NotificationTypeEnum.NewRecommendationByFollowedUser => RelatedEntityKind.Story,
        NotificationTypeEnum.NewStoryFavorite                => RelatedEntityKind.OwnStory,
        NotificationTypeEnum.NewStoryFollower                => RelatedEntityKind.OwnStory,
        NotificationTypeEnum.RecommendationHighlighted       => RelatedEntityKind.Story,
        NotificationTypeEnum.SuccessfulRec                   => RelatedEntityKind.Story,

        // ── Implemented WU-RecLifecycle (submit / revise / revision request / unblock):
        // 22/27 tell the story's author; 40/43 tell the recommender. ──
        NotificationTypeEnum.NewRecommendationOnYourStory    => RelatedEntityKind.OwnStory,
        NotificationTypeEnum.RecommendationApproved          => RelatedEntityKind.Story,
        NotificationTypeEnum.RecommendationRevisionRequested => RelatedEntityKind.Story,
        NotificationTypeEnum.RecommendationRevised           => RelatedEntityKind.OwnStory,

        // ── Implemented: lineage (WU42), acknowledgement (WU-StatBadgeProducers), and the
        // submission outcomes (WU34) — RelatedEntityId = the story. 50 names the requester's
        // source story to the target's author, 51 the approver's target story to the source's
        // author, 52 the crediting author's story to the credited user: none is the story's
        // author, and 50/52 can be minted while that story is still a draft. 71/75 tell the author. ──
        NotificationTypeEnum.StoryLineageRequested            => RelatedEntityKind.Story,
        NotificationTypeEnum.StoryLineageApproved             => RelatedEntityKind.Story,
        NotificationTypeEnum.NewStoryAcknowledgement         => RelatedEntityKind.Story,
        NotificationTypeEnum.StoryRejected                   => RelatedEntityKind.OwnStory,
        NotificationTypeEnum.StoryApproved                   => RelatedEntityKind.OwnStory,

        // ── Implemented WU-TagFanon: RelatedEntityId = the official tag the author is invited
        // to adopt; deep-links to the per-tag adoption page. ─────────────────────────────
        NotificationTypeEnum.TagUpdateSuggestion => RelatedEntityKind.Tag,

        // ── Implemented WU39 (Feature 53): RelatedEntityId = storyId for the per-link tier;
        // the account tier carries 0 (Settings is a fixed route, not an entity) — falls to
        // None below. Both tell the story's author. ──────────────────────────────────────────
        NotificationTypeEnum.ExternalLinkVerified => RelatedEntityKind.OwnStory,
        NotificationTypeEnum.ExternalLinkRejected => RelatedEntityKind.OwnStory,

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

        // ── Story-family kinds, split by recipient (tracker D21; layer2-services.md §"Polymorphic
        // RelatedEntityId" → "Story-family kinds by recipient"). Each pair runs the same loader over a
        // different story source, so the two halves cannot drift apart in shape. ──

        if (idsByKind.TryGetValue(RelatedEntityKind.Story, out var storyIds) && storyIds.Count > 0)
            result[RelatedEntityKind.Story] = await LoadStoriesAsync(PublicWindowStories(readDb), storyIds);

        if (idsByKind.TryGetValue(RelatedEntityKind.OwnStory, out var ownStoryIds) && ownStoryIds.Count > 0)
            result[RelatedEntityKind.OwnStory] = await LoadStoriesAsync(StoriesAsStored(readDb), ownStoryIds);

        if (idsByKind.TryGetValue(RelatedEntityKind.Chapter, out var chapterIds) && chapterIds.Count > 0)
        {
            // Followers (type 10): an unpublished chapter is its author's alone, like its story
            // (StoryVisibilityGuard.IsChapterVisibleAsync's rule).
            result[RelatedEntityKind.Chapter] =
                await LoadChaptersAsync(readDb, PublicWindowStories(readDb), chapterIds, publishedChaptersOnly: true);
        }

        if (idsByKind.TryGetValue(RelatedEntityKind.OwnChapter, out var ownChapterIds) && ownChapterIds.Count > 0)
        {
            result[RelatedEntityKind.OwnChapter] =
                await LoadChaptersAsync(readDb, StoriesAsStored(readDb), ownChapterIds, publishedChaptersOnly: false);
        }

        if (idsByKind.TryGetValue(RelatedEntityKind.GroupStory, out var groupStoryIds) && groupStoryIds.Count > 0)
            result[RelatedEntityKind.GroupStory] = await LoadGroupStoriesAsync(readDb, PublicWindowStories(readDb), groupStoryIds);

        if (idsByKind.TryGetValue(RelatedEntityKind.OwnGroupStory, out var ownGroupStoryIds) && ownGroupStoryIds.Count > 0)
            result[RelatedEntityKind.OwnGroupStory] = await LoadGroupStoriesAsync(readDb, StoriesAsStored(readDb), ownGroupStoryIds);

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
            // TPT-root lookup (WU-B2): resolves every post kind; /blog/{id} is the unified
            // BlogPostPage route. These recipients (13–16, 100) are never the post's author, so the
            // lookup drops a post they can no longer open (tracker D8): an unpublished post, or a
            // profile post whose author's profile is Private. That is BlogPostVisibilityGuard's
            // Class-A half; UsersOnly needs no check (every recipient is signed in), and rating,
            // audience and reveals are Class B, which Personal-plane notifications never filter.
            // The check reads only the post, never the recipient, so the email composer (no active
            // user) shares it. No IgnoreQueryFilters: the IsTakenDown named filter stays active
            // deliberately — a taken-down post drops out → null target → the graceful fallback.
            result[RelatedEntityKind.BlogPostDirect] = (await readDb.BlogPosts
                    .Where(b => directPostIds.Contains(b.BlogPostId))
                    .Where(b => b is ProfileBlogPost
                        ? ((ProfileBlogPost)b).IsPublished
                          && (b.Author == null
                              || b.Author.PrivacySettings.ProfileVisibility != ProfileVisibility.Private)
                        : b is GroupBlogPost
                            ? ((GroupBlogPost)b).IsPublished
                            : ((SiteBlogPost)b).IsPublished)
                    .Select(b => new { b.BlogPostId, b.Title })
                    .ToListAsync())
                .ToDictionary(
                    b => b.BlogPostId,
                    b => new NotificationTarget(b.Title, $"/blog/{b.BlogPostId}", null));
        }

        if (idsByKind.TryGetValue(RelatedEntityKind.OwnBlogPost, out var ownPostIds) && ownPostIds.Count > 0)
        {
            // The author's own post (33): resolved as stored — publish state and the author's own
            // privacy never hide a post from its author. Same TPT root, same link, same active
            // IsTakenDown filter as BlogPostDirect above.
            result[RelatedEntityKind.OwnBlogPost] = (await readDb.BlogPosts
                    .Where(b => ownPostIds.Contains(b.BlogPostId))
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

    // ── Story sources for the story-family kinds (tracker D21) ─────────────────────────────────

    /// <summary>
    /// The stories a recipient who is <b>not</b> the story's author may still see named: the public
    /// window, not taken down. This is <c>ReadOnlyApplicationDbContext</c>'s <c>StoryStatus</c> filter
    /// with its viewer clause removed, plus its <c>IsTakenDown</c> filter. The viewer clause has to go
    /// because it exempts the <em>active user</em> when they authored the story: the email composer
    /// has no active user, and the recipients of these kinds are not the author by construction, so
    /// the answer must not depend on who is asking.
    /// </summary>
    private static IQueryable<Story> PublicWindowStories(ReadOnlyApplicationDbContext readDb) =>
        readDb.Stories
            // elevated read: rating is lifted because notifications are Personal plane (never
            // rating-filtered); StoryStatus is lifted only to restate it below without its viewer
            // clause. IsTakenDown stays active: a taken-down story drops out, as on BlogPostDirect.
            .IgnoreQueryFilters(["ContentRating", "StoryStatus"])
            .Where(s => s.StoryStatusId != StoryStatusEnum.Draft
                        && s.StoryStatusId != StoryStatusEnum.PendingApproval
                        && s.StoryStatusId != StoryStatusEnum.Rejected);

    /// <summary>
    /// Every story as stored, for the <c>Own*</c> kinds: their recipient is always the story's
    /// author, who keeps their own story's title in any status, taken down included. This is what
    /// these kinds read before the split (the unfiltered <c>StoryListings</c> table); unlike
    /// <c>OwnBlogPost</c>, which has always kept the removal filter, a taken-down story keeps its title
    /// for its author. Its <em>link</em> does not survive a takedown: the story page and the chapter
    /// page keep the <c>IsTakenDown</c> filter for the author too (only the editor lifts it), so the
    /// loaders below drop the URL of a taken-down story rather than link a 404.
    /// </summary>
    private static IQueryable<Story> StoriesAsStored(ReadOnlyApplicationDbContext readDb) =>
        readDb.Stories.IgnoreQueryFilters(); // elevated read: the recipient is the story's author

    /// <summary>
    /// Story targets. A taken-down story keeps its title but loses its link (only
    /// <see cref="StoriesAsStored"/> can return one; <see cref="PublicWindowStories"/> has already
    /// dropped it): <c>/story/{id}</c> is a 404 for its author too.
    /// </summary>
    private static async Task<Dictionary<int, NotificationTarget>> LoadStoriesAsync(
        IQueryable<Story> stories, HashSet<int> storyIds) =>
        (await stories
                .Where(s => storyIds.Contains(s.StoryId))
                .Select(s => new { s.StoryId, StoryTitle = (string?)s.StoryListing.StoryTitle, s.IsTakenDown })
                .ToListAsync())
            .ToDictionary(
                s => s.StoryId,
                s => new NotificationTarget(s.StoryTitle, s.IsTakenDown ? null : $"/story/{s.StoryId}", null));

    /// <summary>
    /// Chapter targets. The story title rides along as context (type 10: "New chapter of {story}:
    /// {chapter}"). The inner join on <paramref name="stories"/> is the confidentiality step: a chapter
    /// whose story is outside the source drops whole, chapter title and link included. A chapter the
    /// reading page would not serve (its story taken down, or the chapter unpublished, for its author
    /// too) keeps its titles and loses its link; only the author's source can return one.
    /// </summary>
    private static async Task<Dictionary<int, NotificationTarget>> LoadChaptersAsync(
        ReadOnlyApplicationDbContext readDb, IQueryable<Story> stories, HashSet<int> chapterIds,
        bool publishedChaptersOnly) =>
        (await (
                from c in readDb.Chapters
                where chapterIds.Contains(c.ChapterId) && (c.IsPublished || !publishedChaptersOnly)
                join s in stories on c.StoryId equals s.StoryId
                select new
                {
                    c.ChapterId, c.Title, c.StoryId, c.ChapterNumber,
                    StoryTitle = (string?)s.StoryListing.StoryTitle,
                    Readable = c.IsPublished && !s.IsTakenDown
                })
                .ToListAsync())
            .ToDictionary(
                c => c.ChapterId,
                c => new NotificationTarget(
                    c.Title, c.Readable ? $"/story/{c.StoryId}/{c.ChapterNumber}" : null, c.StoryTitle));

    /// <summary>
    /// Group-story targets (owner ruling D16: one id names the pairing). Explicit joins (not
    /// navigations) so the group read carries the same elevated <c>GroupAudience</c> bypass as the
    /// <c>Group</c> kind — Personal plane. The title and link are the group, which every recipient can
    /// still open, so the story joins on the left: a story outside <paramref name="stories"/> leaves
    /// the target in place with no context title ("A new story was added to {group}").
    /// </summary>
    private static async Task<Dictionary<int, NotificationTarget>> LoadGroupStoriesAsync(
        ReadOnlyApplicationDbContext readDb, IQueryable<Story> stories, HashSet<int> groupStoryIds) =>
        (await (
                from gs in readDb.GroupStories
                where groupStoryIds.Contains(gs.GroupStoryId)
                join g in readDb.Groups.IgnoreQueryFilters(["GroupAudience"]) on gs.GroupId equals g.GroupId
                join s in stories on gs.StoryId equals s.StoryId into visible
                from s in visible.DefaultIfEmpty()
                select new
                {
                    gs.GroupStoryId, g.GroupId, g.GroupName,
                    StoryTitle = s == null ? null : (string?)s.StoryListing.StoryTitle
                })
                .ToListAsync())
            .ToDictionary(
                x => x.GroupStoryId,
                x => new NotificationTarget(x.GroupName, $"/group/{x.GroupId}", x.StoryTitle));
}
