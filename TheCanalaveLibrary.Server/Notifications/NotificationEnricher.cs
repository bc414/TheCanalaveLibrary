using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

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
    /// </summary>
    private enum RelatedEntityKind { None, User, Story, Chapter, Group, BlogPost, BlogPostDirect, Tag }

    /// <summary>
    /// Batch-resolves <c>(type, relatedEntityId)</c> pairs to their <c>(Title, Url)</c> targets.
    /// Pairs whose type has no navigable target, or whose entity no longer resolves (deleted,
    /// taken down), are simply absent from the returned dictionary — callers treat a miss as
    /// "no title, no link," which every consumer already renders gracefully.
    /// </summary>
    /// <param name="readDb">
    /// A no-tracking read context. Callers pass their own short-lived context and use it
    /// sequentially — see <c>layer2-services.md</c> §"Read-context concurrency: factory per method."
    /// </param>
    /// <param name="pairs">One entry per notification being enriched. Duplicates are fine.</param>
    public static async Task<Dictionary<(NotificationTypeEnum TypeId, int RelatedEntityId), (string? Title, string? Url)>>
        ResolveTargetsAsync(
            ReadOnlyApplicationDbContext readDb,
            IReadOnlyList<(NotificationTypeEnum TypeId, int RelatedEntityId)> pairs)
    {
        Dictionary<RelatedEntityKind, Dictionary<int, (string? Title, string? Url)>> kindLookups =
            await BatchLoadEntitiesAsync(readDb, pairs);

        var resolved = new Dictionary<(NotificationTypeEnum, int), (string?, string?)>();
        foreach ((NotificationTypeEnum typeId, int relatedEntityId) in pairs)
        {
            if (resolved.ContainsKey((typeId, relatedEntityId))) continue;

            if (kindLookups.TryGetValue(KindFor(typeId), out var byId) &&
                byId.TryGetValue(relatedEntityId, out (string? Title, string? Url) target))
            {
                resolved[(typeId, relatedEntityId)] = target;
            }
        }

        return resolved;
    }

    /// <summary>
    /// Maps each <see cref="NotificationTypeEnum"/> to the kind of entity its
    /// <c>RelatedEntityId</c> references. Derived from the <c>CreateCoreAsync</c> call-sites
    /// in <see cref="ServerNotificationWriteService"/> — those are the authoritative source of
    /// what each semantic method stores in <c>RelatedEntityId</c>.
    ///
    /// <para>Types whose generating write-path is not yet implemented are stubbed with the kind
    /// their future implementation is expected to store; they produce no DB rows until the
    /// triggering work-unit lands, but the branch exists for forward-compat.</para>
    /// </summary>
    private static RelatedEntityKind KindFor(NotificationTypeEnum type) => type switch
    {
        // ── Implemented WU22: RelatedEntityId = follower's / voucher's user id ──
        NotificationTypeEnum.NewFollowerOnYou => RelatedEntityKind.User,
        NotificationTypeEnum.NewVouchOnYou    => RelatedEntityKind.User,

        // ── Implemented WU29: RelatedEntityId = recommender's user id ────────────
        NotificationTypeEnum.HiddenGem        => RelatedEntityKind.User,

        // ── Implemented WU32: group fan-out ──────────────────────────────────────
        NotificationTypeEnum.NewGroupStory          => RelatedEntityKind.Group,
        NotificationTypeEnum.YourStoryAddedToGroup  => RelatedEntityKind.Group,
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
        // profileOwnerId for NewCommentOnYourProfile. CommentReply stays None below — one
        // cross-context type cannot map to one table (relatedId is the context entity's id).
        NotificationTypeEnum.NewStoryComment                 => RelatedEntityKind.Chapter,
        NotificationTypeEnum.NewCommentOnBlog                => RelatedEntityKind.BlogPostDirect,
        NotificationTypeEnum.NewCommentOnYourProfile         => RelatedEntityKind.User,
        NotificationTypeEnum.NewBlogPostByFollowedUser       => RelatedEntityKind.BlogPostDirect,
        NotificationTypeEnum.NewBlogPostOnFollowedStory      => RelatedEntityKind.BlogPostDirect,
        NotificationTypeEnum.NewBlogPostOnFavoritedStory     => RelatedEntityKind.BlogPostDirect,
        NotificationTypeEnum.NewBlogPostOnReadItLaterStory   => RelatedEntityKind.BlogPostDirect,

        // ── Forward-compat stubs (no rows until triggering work-units land) ──────
        NotificationTypeEnum.NewChapterOnFollowedStory       => RelatedEntityKind.Chapter,
        NotificationTypeEnum.NewStoryByFollowedUser          => RelatedEntityKind.Story,
        NotificationTypeEnum.NewRecommendationByFollowedUser => RelatedEntityKind.Story,
        NotificationTypeEnum.NewStoryFavorite                => RelatedEntityKind.Story,
        NotificationTypeEnum.NewStoryFollower                => RelatedEntityKind.Story,
        // NewRecommendationOnYourStory + RecommendationApproved gained production senders in
        // WU-RecLifecycle (submit / unblock); the two types below were minted by the same WU.
        NotificationTypeEnum.NewRecommendationOnYourStory    => RelatedEntityKind.Story,
        NotificationTypeEnum.RecommendationApproved          => RelatedEntityKind.Story,
        NotificationTypeEnum.RecommendationRevisionRequested => RelatedEntityKind.Story,
        NotificationTypeEnum.RecommendationRevised           => RelatedEntityKind.Story,
        NotificationTypeEnum.RecommendationHighlighted       => RelatedEntityKind.Story,
        NotificationTypeEnum.SuccessfulRec                   => RelatedEntityKind.Story,
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

        // ── No navigable target (site announcements, account warnings, reports) ──
        _ => RelatedEntityKind.None
    };

    /// <summary>
    /// For each distinct <see cref="RelatedEntityKind"/> present in
    /// <paramref name="typeIdPairs"/>, queries the relevant table by the id-set found in the
    /// input, returning one dictionary per kind.
    ///
    /// <para><see cref="RelatedEntityKind.None"/> produces no query.</para>
    /// </summary>
    private static async Task<Dictionary<RelatedEntityKind, Dictionary<int, (string? Title, string? Url)>>>
        BatchLoadEntitiesAsync(
            ReadOnlyApplicationDbContext readDb,
            IReadOnlyList<(NotificationTypeEnum TypeId, int RelatedEntityId)> typeIdPairs)
    {
        // Classify each row's kind and group ids per kind — skip None entirely.
        var idsByKind = typeIdPairs
            .GroupBy(p => KindFor(p.TypeId))
            .Where(g => g.Key != RelatedEntityKind.None)
            .ToDictionary(
                g => g.Key,
                g => g.Select(p => p.RelatedEntityId).ToHashSet());

        var result = new Dictionary<RelatedEntityKind, Dictionary<int, (string? Title, string? Url)>>();

        if (idsByKind.TryGetValue(RelatedEntityKind.Story, out var storyIds))
        {
            result[RelatedEntityKind.Story] = (await readDb.StoryListings
                    .Where(s => storyIds.Contains(s.StoryId))
                    .Select(s => new { s.StoryId, s.StoryTitle })
                    .ToListAsync())
                .ToDictionary(
                    s => s.StoryId,
                    s => ((string?)s.StoryTitle, (string?)$"/story/{s.StoryId}"));
        }

        if (idsByKind.TryGetValue(RelatedEntityKind.Chapter, out var chapterIds))
        {
            result[RelatedEntityKind.Chapter] = (await readDb.Chapters
                    .Where(c => chapterIds.Contains(c.ChapterId))
                    .Select(c => new { c.ChapterId, c.Title, c.StoryId, c.ChapterNumber })
                    .ToListAsync())
                .ToDictionary(
                    c => c.ChapterId,
                    c => ((string?)c.Title, (string?)$"/story/{c.StoryId}/{c.ChapterNumber}"));
        }

        if (idsByKind.TryGetValue(RelatedEntityKind.User, out var userIds))
        {
            result[RelatedEntityKind.User] = (await readDb.Users
                    .Where(u => userIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.UserName })
                    .ToListAsync())
                .ToDictionary(
                    u => u.Id,
                    u => ((string?)(u.UserName ?? "Unknown User"), (string?)$"/user/{u.Id}"));
        }

        if (idsByKind.TryGetValue(RelatedEntityKind.Group, out var groupIds))
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
                    g => ((string?)g.GroupName, (string?)$"/group/{g.GroupId}"));
        }

        if (idsByKind.TryGetValue(RelatedEntityKind.BlogPost, out var blogPostIds))
        {
            // Group-scoped kind (NewGroupBlogPost only): links to the GROUP, not the post —
            // that type's chosen navigation target. Post-scoped types use BlogPostDirect below.
            result[RelatedEntityKind.BlogPost] = (await readDb.GroupBlogPosts
                    .Where(b => blogPostIds.Contains(b.BlogPostId))
                    .Select(b => new { b.BlogPostId, b.Title, b.GroupId })
                    .ToListAsync())
                .ToDictionary(
                    b => b.BlogPostId,
                    b => ((string?)b.Title, (string?)$"/group/{b.GroupId}"));
        }

        if (idsByKind.TryGetValue(RelatedEntityKind.BlogPostDirect, out var directPostIds))
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
                    b => ((string?)b.Title, (string?)$"/blog/{b.BlogPostId}"));
        }

        if (idsByKind.TryGetValue(RelatedEntityKind.Tag, out var tagIds))
        {
            // WU-TagFanon: the adoption invitation deep-links to the author's per-tag page.
            result[RelatedEntityKind.Tag] = (await readDb.Tags
                    .Where(t => tagIds.Contains(t.TagId))
                    .Select(t => new { t.TagId, t.TagName })
                    .ToListAsync())
                .ToDictionary(
                    t => t.TagId,
                    t => ((string?)t.TagName, (string?)$"/tag-adoptions/{t.TagId}"));
        }

        return result;
    }
}
