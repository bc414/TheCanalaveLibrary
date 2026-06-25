using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation of <see cref="INotificationReadService"/>. Uses
/// <see cref="ReadOnlyApplicationDbContext"/> (no-tracking) and projects straight to DTOs.
///
/// <para>All methods are self-scoped: they operate on the currently authenticated user via
/// <c>IActiveUserContext</c>. Anonymous callers receive safe zero/empty responses.</para>
///
/// <para><see cref="GetNotificationsAsync"/> uses two-pass batch enrichment (WU33): the
/// first pass materializes the page with <c>SourceUserName</c> (LEFT JOIN to <c>Users</c>
/// on <c>SourceUserId</c>); the second pass classifies each row by a private
/// <c>RelatedEntityKind</c> switch and batch-loads each distinct kind present on the page
/// (one query per kind, none if the kind is absent). See <c>layer2-services.md</c>
/// §"Polymorphic RelatedEntityId — Two-Pass Batch Enrichment."</para>
///
/// <para><see cref="GetSettingsAsync"/> LEFT-JOINs settings onto types; NULL means "use
/// default" (sparse-override model, Feature 43).</para>
/// </summary>
public class ServerNotificationReadService(
    ReadOnlyApplicationDbContext readDb,
    IActiveUserContext activeUser) : INotificationReadService
{
    // ── Internal kind classification ──────────────────────────────────────────────

    /// <summary>
    /// Internal enum classifying what entity table a notification's <c>RelatedEntityId</c>
    /// references. Used by <see cref="KindFor"/> and <see cref="BatchLoadEntitiesAsync"/>.
    /// </summary>
    private enum RelatedEntityKind { None, User, Story, Chapter, Group, BlogPost }

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

        // ── Forward-compat stubs (no rows until triggering work-units land) ──────
        NotificationTypeEnum.NewChapterOnFollowedStory       => RelatedEntityKind.Chapter,
        NotificationTypeEnum.NewStoryByFollowedUser          => RelatedEntityKind.Story,
        NotificationTypeEnum.NewRecommendationByFollowedUser => RelatedEntityKind.Story,
        NotificationTypeEnum.NewBlogPostByFollowedUser       => RelatedEntityKind.BlogPost,
        NotificationTypeEnum.NewBlogPostOnFollowedStory      => RelatedEntityKind.BlogPost,
        NotificationTypeEnum.NewBlogPostOnFavoritedStory     => RelatedEntityKind.BlogPost,
        NotificationTypeEnum.NewBlogPostOnReadItLaterStory   => RelatedEntityKind.BlogPost,
        NotificationTypeEnum.NewStoryFavorite                => RelatedEntityKind.Story,
        NotificationTypeEnum.NewStoryFollower                => RelatedEntityKind.Story,
        NotificationTypeEnum.NewRecommendationOnYourStory    => RelatedEntityKind.Story,
        NotificationTypeEnum.NewStoryComment                 => RelatedEntityKind.Story,
        NotificationTypeEnum.NewCommentOnBlog                => RelatedEntityKind.BlogPost,
        NotificationTypeEnum.RecommendationApproved          => RelatedEntityKind.Story,
        NotificationTypeEnum.RecommendationHighlighted       => RelatedEntityKind.Story,
        NotificationTypeEnum.SuccessfulRec                   => RelatedEntityKind.Story,
        NotificationTypeEnum.StoryRelationshipRequested      => RelatedEntityKind.Story,
        NotificationTypeEnum.StoryRelationshipApproved       => RelatedEntityKind.Story,
        NotificationTypeEnum.NewStoryAcknowledgement         => RelatedEntityKind.Story,
        NotificationTypeEnum.StoryRejected                   => RelatedEntityKind.Story,

        // ── No navigable target (site announcements, account warnings, reports) ──
        _ => RelatedEntityKind.None
    };

    // ── Protected surface for the derived write service ────────────────────────────

    /// <summary>
    /// Exposed so the derived write service can use the read context without re-capturing
    /// the <c>readDb</c> primary constructor parameter (avoids CS9107 double-capture warning
    /// — see <c>layer2-services.md</c> §"CS9107/CS9124: shared constructor parameters").
    /// </summary>
    protected ReadOnlyApplicationDbContext ReadDb { get; } = readDb;

    /// <summary>
    /// Exposed so the derived write service can access the current user's id without
    /// re-capturing the <c>activeUser</c> primary constructor parameter (avoids CS9107).
    /// </summary>
    protected int? CurrentUserId => activeUser.UserId;

    // ── Interface implementation ───────────────────────────────────────────────────

    public async Task<int> GetUnreadCountAsync()
    {
        int? userId = activeUser.UserId;
        if (userId is null) return 0;

        return await ReadDb.Notifications
            .CountAsync(n => n.RecipientUserId == userId && !n.IsRead);
    }

    public async Task<int> GetTotalCountAsync()
    {
        int? userId = activeUser.UserId;
        if (userId is null) return 0;

        return await ReadDb.Notifications
            .CountAsync(n => n.RecipientUserId == userId);
    }

    public async Task<NotificationDto[]> GetNotificationsAsync(
        int page,
        int pageSize,
        NotificationFeedOrder order = NotificationFeedOrder.NewestFirst)
    {
        int? userId = activeUser.UserId;
        if (userId is null) return [];

        int skip = (page - 1) * pageSize;

        // ── First pass: materialize the page ──────────────────────────────────────
        // LEFT JOINs:
        //   • UserNotificationSettings (sparse) → effective Collapsed per type.
        //   • Users on SourceUserId (int?) → SourceUserName; null when source deleted
        //     (SET NULL policy) or type has no actor field.
        var q =
            from n in ReadDb.Notifications
            where n.RecipientUserId == userId
            join nt in ReadDb.NotificationTypes
                on n.NotificationTypeId equals nt.NotificationTypeId
            join uns in ReadDb.UserNotificationSettings.Where(s => s.UserId == userId)
                on n.NotificationTypeId equals uns.NotificationTypeId into settings
            from s in settings.DefaultIfEmpty()
            join u in ReadDb.Users
                on n.SourceUserId equals u.Id into sources
            from src in sources.DefaultIfEmpty()
            select new
            {
                n.NotificationId,
                n.NotificationTypeId,
                CategoryId = nt.NotificationCategory,
                n.SourceUserId,
                SourceUserName = src.UserName,
                n.RelatedEntityId,
                n.IsRead,
                n.DateCreated,
                Collapsed = s != null ? s.Collapsed : nt.DefaultCollapsed
            };

        // Ordering:
        // NewestFirst (default) — most recently created first; tie-break by id desc.
        // OldestUnreadFirst     — unread (IsRead=false → 0 in SQL) before read (1),
        //                         then chronological ascending within each group.
        var orderedQ = order switch
        {
            NotificationFeedOrder.OldestUnreadFirst =>
                q.OrderBy(x => x.IsRead).ThenBy(x => x.DateCreated),
            _ =>
                q.OrderByDescending(x => x.DateCreated).ThenByDescending(x => x.NotificationId)
        };

        var rows = await orderedQ.Skip(skip).Take(pageSize).ToListAsync();
        if (rows.Count == 0) return [];

        // ── Second pass: batch-load RelatedEntity data per kind ───────────────────
        // One query per RelatedEntityKind present on this page; kinds absent produce no query.
        var kindLookups = await BatchLoadEntitiesAsync(
            rows.Select(r => (r.NotificationTypeId, r.RelatedEntityId)).ToList());

        // ── Stitch: merge enriched fields into DTOs ────────────────────────────────
        return [..rows.Select(r =>
        {
            var kind = KindFor(r.NotificationTypeId);
            var (targetTitle, targetUrl) =
                kindLookups.TryGetValue(kind, out var dict) &&
                dict.TryGetValue(r.RelatedEntityId, out var target)
                    ? target
                    : (null, null);

            return new NotificationDto(
                r.NotificationId,
                r.NotificationTypeId,
                r.CategoryId,
                r.SourceUserId,
                r.SourceUserName,
                targetTitle,
                targetUrl,
                r.RelatedEntityId,
                r.IsRead,
                r.DateCreated,
                r.Collapsed);
        })];
    }

    public async Task<NotificationSettingDto[]> GetSettingsAsync()
    {
        int? userId = activeUser.UserId;

        if (userId is null)
        {
            // Anonymous: return defaults for all types (IsDefault = true — no override rows).
            return await ReadDb.NotificationTypes
                .OrderBy(nt => nt.NotificationCategory).ThenBy(nt => nt.NotificationTypeId)
                .Select(nt => new NotificationSettingDto(
                    nt.NotificationTypeId,
                    nt.NotificationCategory,
                    nt.DisplayName,
                    nt.Description,
                    nt.DefaultEmailEnabled,
                    nt.DefaultCollapsed,
                    true))
                .ToArrayAsync();
        }

        // LEFT JOIN UserNotificationSettings onto NotificationTypes.
        // NULL from the left join → no override → IsDefault = true, values come from type defaults.
        return await (
            from nt in ReadDb.NotificationTypes
            join uns in ReadDb.UserNotificationSettings.Where(s => s.UserId == userId)
                on nt.NotificationTypeId equals uns.NotificationTypeId into settings
            from s in settings.DefaultIfEmpty()
            orderby nt.NotificationCategory, nt.NotificationTypeId
            select new NotificationSettingDto(
                nt.NotificationTypeId,
                nt.NotificationCategory,
                nt.DisplayName,
                nt.Description,
                s != null ? s.EmailEnabled : nt.DefaultEmailEnabled,
                s != null ? s.Collapsed : nt.DefaultCollapsed,
                s == null // IsDefault
            )
        ).ToArrayAsync();
    }

    // ── Private: two-pass batch enrichment ────────────────────────────────────────

    /// <summary>
    /// For each distinct <see cref="RelatedEntityKind"/> present in
    /// <paramref name="typeIdPairs"/>, queries the relevant table by the id-set found on this
    /// page, returning one dictionary per kind. The caller looks up each row's
    /// <c>(kind, relatedEntityId)</c> to obtain the resolved <c>(Title, Url)</c> pair.
    ///
    /// <para><see cref="RelatedEntityKind.None"/> produces no query (null/null is always
    /// returned at stitch time for such rows — the caller checks
    /// <c>kindLookups.TryGetValue</c> first).</para>
    /// </summary>
    private async Task<Dictionary<RelatedEntityKind, Dictionary<int, (string? Title, string? Url)>>>
        BatchLoadEntitiesAsync(
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
            result[RelatedEntityKind.Story] = (await ReadDb.StoryListings
                    .Where(s => storyIds.Contains(s.StoryId))
                    .Select(s => new { s.StoryId, s.StoryTitle })
                    .ToListAsync())
                .ToDictionary(
                    s => s.StoryId,
                    s => ((string?)s.StoryTitle, (string?)$"/story/{s.StoryId}"));
        }

        if (idsByKind.TryGetValue(RelatedEntityKind.Chapter, out var chapterIds))
        {
            result[RelatedEntityKind.Chapter] = (await ReadDb.Chapters
                    .Where(c => chapterIds.Contains(c.ChapterId))
                    .Select(c => new { c.ChapterId, c.Title, c.StoryId, c.ChapterNumber })
                    .ToListAsync())
                .ToDictionary(
                    c => c.ChapterId,
                    c => ((string?)c.Title, (string?)$"/story/{c.StoryId}/{c.ChapterNumber}"));
        }

        if (idsByKind.TryGetValue(RelatedEntityKind.User, out var userIds))
        {
            result[RelatedEntityKind.User] = (await ReadDb.Users
                    .Where(u => userIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.UserName })
                    .ToListAsync())
                .ToDictionary(
                    u => u.Id,
                    u => ((string?)(u.UserName ?? "Unknown User"), (string?)$"/user/{u.Id}"));
        }

        if (idsByKind.TryGetValue(RelatedEntityKind.Group, out var groupIds))
        {
            result[RelatedEntityKind.Group] = (await ReadDb.Groups
                    .Where(g => groupIds.Contains(g.GroupId))
                    .Select(g => new { g.GroupId, g.GroupName })
                    .ToListAsync())
                .ToDictionary(
                    g => g.GroupId,
                    g => ((string?)g.GroupName, (string?)$"/group/{g.GroupId}"));
        }

        if (idsByKind.TryGetValue(RelatedEntityKind.BlogPost, out var blogPostIds))
        {
            // Only GroupBlogPost generates notifications today (NewGroupBlogPost via WU32).
            // GroupBlogPost inherits Title from BaseBlogPost (TPT); GroupId needed for the URL.
            // ProfileBlogPost notification types are not yet implemented (no generating path).
            result[RelatedEntityKind.BlogPost] = (await ReadDb.GroupBlogPosts
                    .Where(b => blogPostIds.Contains(b.BlogPostId))
                    .Select(b => new { b.BlogPostId, b.Title, b.GroupId })
                    .ToListAsync())
                .ToDictionary(
                    b => b.BlogPostId,
                    b => ((string?)b.Title, (string?)$"/group/{b.GroupId}"));
        }

        return result;
    }
}
