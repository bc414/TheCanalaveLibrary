using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation of <see cref="INotificationWriteService"/>. Inherits
/// <see cref="ServerNotificationReadService"/> for the CQRS-lite read path.
///
/// <para><b>Private create-core (<see cref="CreateCoreAsync"/>).</b> All <c>Notify*Async</c>
/// methods are thin wrappers over this single private method, which owns the two universal
/// invariants: <b>drop-self</b> (an actor is never notified of their own action — conditional on
/// there being an actor: a null source drops nobody, owner ruling D4) and <b>dedup</b> (skip a
/// recipient who already holds an unread notification for the same type + source + related entity,
/// except for the D4-exempt types that have no related entity). There is <em>no</em> public generic
/// <c>CreateAsync</c> that bypasses these invariants — see <c>layer2-services.md</c>
/// §"Notification Generation" for the rationale.</para>
///
/// <para><b>Recipient resolution / DAG rule:</b> fan-out methods resolve their recipients with
/// direct queries on this service's own write context — ground truth, Personal plane, never rating-
/// or audience-filtered. No other feature's service (read or write) is injected here: a feature that
/// calls this service must never be called back by it. See <c>layer2-services.md</c> "The DAG
/// rule."</para>
///
/// <para><b>Best-effort post-commit:</b> callers invoke the <c>Notify*Async</c> methods
/// after their own <c>SaveChangesAsync</c>, inside a <c>try/catch</c> that logs and swallows.
/// This service's own <c>SaveChangesAsync</c> inside <see cref="CreateCoreAsync"/> is a
/// separate transaction covering only the notification rows.</para>
///
/// <para><b>Email fan-out (WU-NotifEmail):</b> create-core <em>enqueues</em>, it never sends.
/// After the notification rows commit, their ids go onto <see cref="NotificationEmailBuffer"/> and
/// <see cref="NotificationEmailWorker"/> drains them out-of-band. Hooking the fan-out here rather
/// than at each semantic method inherits both invariants for free — only rows that survived
/// drop-self and dedup, and were actually inserted, become candidates for email. Eligibility
/// (per-type <c>EmailEnabled</c>, confirmed address, still-unread) is resolved at drain time by
/// <see cref="NotificationEmailFlusher"/>, not here. See <c>layer2-services.md</c>
/// §"Email fan-out".</para>
/// </summary>
public class ServerNotificationWriteService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    NotificationEmailBuffer emailBuffer,
    ILogger<ServerNotificationWriteService> logger)
    : ServerNotificationReadService(readDbFactory, activeUser), INotificationWriteService
{
    /// <summary>
    /// Owner ruling D4: types whose target is the recipient's account itself carry
    /// <c>RelatedEntityId = 0</c>, and with a null (or constant) source the dedup key would degenerate
    /// to "one unread row per type per user" — a second warning while the first is unread would
    /// vanish. These skip the cross-existing dedup step entirely; within-batch dedup still applies.
    /// Any future dedup unique index must respect this set (and the null source).
    /// </summary>
    private static readonly HashSet<NotificationTypeEnum> CrossExistingDedupExempt =
    [
        NotificationTypeEnum.AccountWarning,
        NotificationTypeEnum.AccountSuspended,
        NotificationTypeEnum.AccountBanned,
        NotificationTypeEnum.ExternalAccountVerified,
        NotificationTypeEnum.ExternalAccountRejected,
        NotificationTypeEnum.SpotlightSlotGranted,
    ];

    // ── Read-side mutations ──────────────────────────────────────────────────────

    public async Task MarkAsReadAsync(long notificationId)
    {
        int userId = ActiveUser.RequireUserId();
        await writeDb.Notifications
            .Where(n => n.NotificationId == notificationId
                        && n.RecipientUserId == userId
                        && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    public async Task MarkAllAsReadAsync()
    {
        int userId = ActiveUser.RequireUserId();
        await writeDb.Notifications
            .Where(n => n.RecipientUserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    // ── Settings ─────────────────────────────────────────────────────────────────

    public async Task SetSettingAsync(NotificationTypeEnum notifType, bool emailEnabled, bool collapsed)
    {
        int userId = ActiveUser.RequireUserId();

        // The sparse upsert/delete rule itself lives in NotificationSettingUpsert — shared with the
        // one-click unsubscribe endpoint, which writes for a user resolved from a signed token
        // rather than from IActiveUserContext and so cannot come through this self-scoped method.
        //
        // Uses ReadDbFactory (the protected property on the base class), not the readDbFactory
        // constructor parameter directly, to avoid CS9107 double-capture (layer2-services.md).
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        await NotificationSettingUpsert.ApplyAsync(writeDb, readDb, userId, notifType, emailEnabled, collapsed);
    }

    // ── Semantic generation methods (WU22 slice — single-recipient) ──────────────

    /// <inheritdoc/>
    public Task NotifyNewFollowerAsync(int recipientUserId, int followerUserId) =>
        CreateCoreAsync(
            NotificationTypeEnum.NewFollowerOnYou,
            sourceUserId: followerUserId,
            targets: [(recipientUserId, followerUserId)]);

    /// <inheritdoc/>
    public Task NotifyNewVouchAsync(int recipientUserId, int voucherUserId) =>
        CreateCoreAsync(
            NotificationTypeEnum.NewVouchOnYou,
            sourceUserId: voucherUserId,
            targets: [(recipientUserId, voucherUserId)]);

    // ── Semantic generation methods (WU29 slice — single-recipient) ──────────────

    /// <inheritdoc/>
    public Task NotifyStoryHiddenGemAsync(int recipientStoryAuthorId, int sourceRecommenderId) =>
        CreateCoreAsync(
            NotificationTypeEnum.HiddenGem,
            sourceUserId: sourceRecommenderId,
            targets: [(recipientStoryAuthorId, sourceRecommenderId)]);

    // ── Semantic generation methods (WU-RecLifecycle slice) ──────────────────────

    /// <inheritdoc/>
    public Task NotifyNewRecommendationOnYourStoryAsync(int recipientStoryAuthorId, int sourceRecommenderId, int storyId) =>
        CreateCoreAsync(NotificationTypeEnum.NewRecommendationOnYourStory, sourceRecommenderId,
            [(recipientStoryAuthorId, storyId)]);

    /// <inheritdoc/>
    public Task NotifyRecommendationRevisionRequestedAsync(int recipientRecommenderId, int sourceStoryAuthorId, int storyId) =>
        CreateCoreAsync(NotificationTypeEnum.RecommendationRevisionRequested, sourceStoryAuthorId,
            [(recipientRecommenderId, storyId)]);

    /// <inheritdoc/>
    public Task NotifyRecommendationRevisedAsync(int recipientStoryAuthorId, int sourceRecommenderId, int storyId) =>
        CreateCoreAsync(NotificationTypeEnum.RecommendationRevised, sourceRecommenderId,
            [(recipientStoryAuthorId, storyId)]);

    /// <inheritdoc/>
    public Task NotifyRecommendationApprovedAsync(int recipientRecommenderId, int sourceStoryAuthorId, int storyId) =>
        CreateCoreAsync(NotificationTypeEnum.RecommendationApproved, sourceStoryAuthorId,
            [(recipientRecommenderId, storyId)]);

    // ── Semantic generation methods (WU32 slice — group fan-out; D16) ────────────

    /// <inheritdoc/>
    public async Task NotifyNewGroupStoryAsync(int groupId, int groupStoryId, int? storyAuthorId, int sourceUserId)
    {
        // Fan-out to members with NotifyForNewStory = true (type NewGroupStory = 60). The story's
        // author is excluded: they get the more specific 25 below — one event, one notification
        // (service audit §2.8). The adder is dropped by the create-core's drop-self rule.
        List<int> memberIds = await writeDb.GroupMembers
            .Where(m => m.GroupId == groupId && m.NotifyForNewStory
                        && (storyAuthorId == null || m.UserId != storyAuthorId))
            .Select(m => m.UserId)
            .ToListAsync();

        // D16: both types anchor on the GroupStory junction row — the single most specific entity
        // of the event; the enricher's GroupStory kind resolves group name + story title from it.
        if (memberIds.Count > 0)
            await CreateCoreAsync(NotificationTypeEnum.NewGroupStory, sourceUserId,
                memberIds.Select(id => (id, (long)groupStoryId)).ToArray());

        // An authorless story still notifies members (above); only the author's own type needs one.
        if (storyAuthorId is int authorId)
            await CreateCoreAsync(NotificationTypeEnum.YourStoryAddedToGroup, sourceUserId,
                [(authorId, groupStoryId)]);
    }

    /// <inheritdoc/>
    public async Task NotifyNewGroupBlogPostAsync(int groupId, int blogPostId, int authorId)
    {
        // Fan-out to all members with NotifyForNewBlogPost = true (type NewGroupBlogPost = 61).
        List<int> memberIds = await writeDb.GroupMembers
            .Where(m => m.GroupId == groupId && m.NotifyForNewBlogPost)
            .Select(m => m.UserId)
            .ToListAsync();

        if (memberIds.Count > 0)
            await CreateCoreAsync(NotificationTypeEnum.NewGroupBlogPost, authorId,
                memberIds.Select(id => (id, (long)blogPostId)).ToArray());
    }

    /// <inheritdoc/>
    public async Task NotifyNewSiteAnnouncementAsync(int blogPostId, int authorId)
    {
        // Fan-out to every user on the site (type SiteAnnouncement = 0). Mirrors
        // NotifyNewGroupBlogPostAsync's shape — GroupMembers swapped for the full Users table.
        List<int> allUserIds = await writeDb.Users.Select(u => u.Id).ToListAsync();

        if (allUserIds.Count > 0)
            await CreateCoreAsync(NotificationTypeEnum.SiteAnnouncement, authorId,
                allUserIds.Select(id => (id, (long)blogPostId)).ToArray());
    }

    // ── Semantic generation methods (WU-InertFeatures — new-chapter fan-out) ─────

    /// <inheritdoc/>
    public async Task NotifyNewChapterAsync(int storyId, int chapterId, int authorId)
    {
        // Story followers — presence of IsFollowed is the signal (no per-row opt-in on a story
        // follow; ReceiveAlerts belongs to user-to-user follows). Personal plane: no rating filter.
        // A hidden favorite does not enter — there is no private-follow flag (D17).
        List<int> followerIds = await writeDb.UserStoryInteractions
            .Where(i => i.StoryId == storyId && i.IsFollowed)
            .Select(i => i.UserId)
            .ToListAsync();

        // RelatedEntityId = chapterId: the chapter joins up to its story (D16-conformant). The author
        // is the source, so an author following their own story is dropped.
        if (followerIds.Count > 0)
            await CreateCoreAsync(NotificationTypeEnum.NewChapterOnFollowedStory, authorId,
                followerIds.Select(id => (id, (long)chapterId)).ToArray());
    }

    // ── Semantic generation methods (WU42 slice — Story Lineage) ─────────────────

    /// <inheritdoc/>
    public Task NotifyStoryLineageRequestedAsync(int targetAuthorId, int requesterId, int sourceStoryId) =>
        CreateCoreAsync(
            NotificationTypeEnum.StoryLineageRequested,
            sourceUserId: requesterId,
            targets: [(targetAuthorId, sourceStoryId)]);

    /// <inheritdoc/>
    public Task NotifyStoryLineageApprovedAsync(int sourceAuthorId, int approverId, int targetStoryId) =>
        CreateCoreAsync(
            NotificationTypeEnum.StoryLineageApproved,
            sourceUserId: approverId,
            targets: [(sourceAuthorId, targetStoryId)]);

    /// <inheritdoc/>
    public Task NotifyStoryAcknowledgedAsync(int acknowledgedUserId, int authorId, int storyId) =>
        CreateCoreAsync(
            NotificationTypeEnum.NewStoryAcknowledgement,
            sourceUserId: authorId,
            targets: [(acknowledgedUserId, storyId)]);

    // ── Semantic generation methods (WU34 slice — moderation; null-sourced, D4/D5) ─
    //
    // Every method in this band passes sourceUserId: null — the acting moderator is never disclosed
    // to the recipient (D5), and none of these methods accepts a moderator id (type-level
    // enforcement). The Report row (or ReviewedByModeratorUserId) keeps the real moderator.

    /// <inheritdoc/>
    public Task NotifyReportReceivedAsync(int reporterUserId, long reportId) =>
        CreateCoreAsync(NotificationTypeEnum.ReportReceived, sourceUserId: null,
            [(reporterUserId, reportId)]);

    /// <inheritdoc/>
    public Task NotifyReportResolvedAsync(int reporterUserId, long reportId) =>
        CreateCoreAsync(NotificationTypeEnum.ReportResolved, sourceUserId: null,
            [(reporterUserId, reportId)]);

    /// <inheritdoc/>
    public Task NotifyReportResolvedNoActionAsync(int reporterUserId, long reportId) =>
        CreateCoreAsync(NotificationTypeEnum.ReportResolvedNoAction, sourceUserId: null,
            [(reporterUserId, reportId)]);

    /// <inheritdoc/>
    public Task NotifyContentRemovedAsync(int contentAuthorUserId, long reportId) =>
        CreateCoreAsync(NotificationTypeEnum.ContentRemoved, sourceUserId: null,
            [(contentAuthorUserId, reportId)]);

    /// <inheritdoc/>
    public Task NotifyStoryApprovedAsync(int storyAuthorUserId, int storyId) =>
        CreateCoreAsync(NotificationTypeEnum.StoryApproved, sourceUserId: null,
            [(storyAuthorUserId, storyId)]);

    /// <inheritdoc/>
    public Task NotifyStoryRejectedAsync(int storyAuthorUserId, int storyId) =>
        CreateCoreAsync(NotificationTypeEnum.StoryRejected, sourceUserId: null,
            [(storyAuthorUserId, storyId)]);

    /// <inheritdoc/>
    public Task NotifyExternalAccountVerifiedAsync(int userId) =>
        CreateCoreAsync(NotificationTypeEnum.ExternalAccountVerified, sourceUserId: null,
            [(userId, 0)]);

    /// <inheritdoc/>
    public Task NotifyExternalAccountRejectedAsync(int userId) =>
        CreateCoreAsync(NotificationTypeEnum.ExternalAccountRejected, sourceUserId: null,
            [(userId, 0)]);

    /// <inheritdoc/>
    public Task NotifyExternalLinkVerifiedAsync(int storyAuthorUserId, int storyId) =>
        CreateCoreAsync(NotificationTypeEnum.ExternalLinkVerified, sourceUserId: null,
            [(storyAuthorUserId, storyId)]);

    /// <inheritdoc/>
    public Task NotifyExternalLinkRejectedAsync(int storyAuthorUserId, int storyId) =>
        CreateCoreAsync(NotificationTypeEnum.ExternalLinkRejected, sourceUserId: null,
            [(storyAuthorUserId, storyId)]);

    /// <inheritdoc/>
    public Task NotifyAccountWarningAsync(int targetUserId) =>
        CreateCoreAsync(NotificationTypeEnum.AccountWarning, sourceUserId: null,
            [(targetUserId, 0)]);

    /// <inheritdoc/>
    public Task NotifyAccountSuspendedAsync(int targetUserId) =>
        CreateCoreAsync(NotificationTypeEnum.AccountSuspended, sourceUserId: null,
            [(targetUserId, 0)]);

    /// <inheritdoc/>
    public Task NotifyAccountBannedAsync(int targetUserId) =>
        CreateCoreAsync(NotificationTypeEnum.AccountBanned, sourceUserId: null,
            [(targetUserId, 0)]);

    // ── Semantic generation methods (WU-Spotlight slice) ─────────────────────────

    /// <inheritdoc/>
    public Task NotifySpotlightSlotGrantedAsync(int awardeeUserId, int grantingModeratorId) =>
        CreateCoreAsync(NotificationTypeEnum.SpotlightSlotGranted, grantingModeratorId,
            [(awardeeUserId, 0)]);

    /// <inheritdoc/>
    public Task NotifyStorySpotlightedAsync(int storyAuthorUserId, int sponsorUserId, int storyId) =>
        CreateCoreAsync(NotificationTypeEnum.StorySpotlighted, sponsorUserId,
            [(storyAuthorUserId, storyId)]);

    /// <inheritdoc/>
    public Task NotifyRecommendationSpotlightedAsync(int recommenderUserId, int sponsorUserId, int storyId) =>
        CreateCoreAsync(NotificationTypeEnum.RecommendationSpotlighted, sponsorUserId,
            [(recommenderUserId, storyId)]);

    // ── Semantic generation methods (WU-Polls slice) ─────────────────────────────

    /// <inheritdoc/>
    public Task NotifyPollUpdatedAsync(int pollOwnerUserId, IReadOnlyList<int> voterUserIds, int relatedEntityId) =>
        CreateCoreAsync(NotificationTypeEnum.PollUpdated, pollOwnerUserId,
            voterUserIds.Select(id => (id, (long)relatedEntityId)).ToArray());

    // ── Semantic generation methods (WU-B2 slice — comments & profile blog posts) ─

    /// <inheritdoc/>
    public Task NotifyNewStoryCommentAsync(int storyAuthorId, int commenterId, int chapterId) =>
        CreateCoreAsync(NotificationTypeEnum.NewStoryComment, commenterId,
            [(storyAuthorId, chapterId)]);

    /// <inheritdoc/>
    public Task NotifyNewBlogCommentAsync(int blogAuthorId, int commenterId, int blogPostId) =>
        CreateCoreAsync(NotificationTypeEnum.NewCommentOnBlog, commenterId,
            [(blogAuthorId, blogPostId)]);

    /// <inheritdoc/>
    public Task NotifyNewProfileCommentAsync(int profileOwnerId, int commenterId) =>
        CreateCoreAsync(NotificationTypeEnum.NewCommentOnYourProfile, commenterId,
            [(profileOwnerId, profileOwnerId)]);

    /// <inheritdoc/>
    public Task NotifyCommentReplyAsync(int parentAuthorId, int commenterId, int contextEntityId) =>
        CreateCoreAsync(NotificationTypeEnum.CommentReply, commenterId,
            [(parentAuthorId, contextEntityId)]);

    /// <inheritdoc/>
    public async Task NotifyNewProfileBlogPostAsync(int blogPostId, int authorId, int? storyId)
    {
        // Class A first (WU-AccessGateSweep2 review fixes): a profile post is exactly as visible as
        // its author's profile, and every recipient below is someone other than the author — so a
        // Private author's post has no recipient who could open it, and a notification would only
        // disclose its title beside a link that 404s. UsersOnly needs no check: every recipient is
        // signed in. (layer2-services.md §"Comment & blog-post semantic methods".)
        ProfileVisibility? authorVisibility = await writeDb.Users
            .Where(u => u.Id == authorId)
            .Select(u => (ProfileVisibility?)u.PrivacySettings.ProfileVisibility)
            .FirstOrDefaultAsync();
        if (authorVisibility == ProfileVisibility.Private)
            return;

        // Recipient resolution on the write context — ground truth, Personal plane (same pattern
        // as the group fan-outs above; no rating/audience filtering applies to recipients).
        // Author-followers gate on the per-follow ReceiveAlerts flag; story-interaction sets have
        // no per-row opt-in — presence of the flag is the signal.
        List<int> authorFollowers = await writeDb.FollowedUsers
            .Where(f => f.FollowedUserId == authorId && f.ReceiveAlerts)
            .Select(f => f.UserId)
            .ToListAsync();

        List<int> storyFollowers = [];
        List<int> storyFavoriters = [];
        List<int> storyReadLaters = [];
        if (storyId is int sid)
        {
            storyFollowers = await writeDb.UserStoryInteractions
                .Where(i => i.StoryId == sid && i.IsFollowed)
                .Select(i => i.UserId)
                .ToListAsync();
            // D17: every favoriter state counts — public, hidden-from-visitors, and private-only. A
            // hidden favorite suppresses public-plane consequences only; this notification goes to
            // the favoriter alone, so withholding it would just punish choosing privacy.
            storyFavoriters = await writeDb.UserStoryInteractions
                .Where(i => i.StoryId == sid && (i.IsFavorite || i.IsHiddenFavorite))
                .Select(i => i.UserId)
                .ToListAsync();
            storyReadLaters = await writeDb.UserStoryInteractions
                .Where(i => i.StoryId == sid && i.IsReadItLater)
                .Select(i => i.UserId)
                .ToListAsync();
        }

        // Precedence-dedup: 13 > 14 > 15 > 16 (most-direct relationship wins). Each user receives
        // exactly one notification per publish event; per-type email settings stay meaningful.
        HashSet<int> claimed = [.. authorFollowers];
        List<int> followedStorySet = storyFollowers.Where(id => claimed.Add(id)).ToList();
        List<int> favoritedStorySet = storyFavoriters.Where(id => claimed.Add(id)).ToList();
        List<int> readLaterStorySet = storyReadLaters.Where(id => claimed.Add(id)).ToList();

        if (authorFollowers.Count > 0)
            await CreateCoreAsync(NotificationTypeEnum.NewBlogPostByFollowedUser, authorId,
                authorFollowers.Select(id => (id, (long)blogPostId)).ToArray());
        if (followedStorySet.Count > 0)
            await CreateCoreAsync(NotificationTypeEnum.NewBlogPostOnFollowedStory, authorId,
                followedStorySet.Select(id => (id, (long)blogPostId)).ToArray());
        if (favoritedStorySet.Count > 0)
            await CreateCoreAsync(NotificationTypeEnum.NewBlogPostOnFavoritedStory, authorId,
                favoritedStorySet.Select(id => (id, (long)blogPostId)).ToArray());
        if (readLaterStorySet.Count > 0)
            await CreateCoreAsync(NotificationTypeEnum.NewBlogPostOnReadItLaterStory, authorId,
                readLaterStorySet.Select(id => (id, (long)blogPostId)).ToArray());
    }

    /// <inheritdoc/>
    public Task NotifyTagAdoptionSuggestedAsync(IReadOnlyList<int> recipientAuthorIds, int targetTagId) =>
        // Null-sourced: the invitation is a moderation act (D5's routed sub-edge).
        CreateCoreAsync(NotificationTypeEnum.TagUpdateSuggestion, sourceUserId: null,
            recipientAuthorIds.Select(id => (id, (long)targetTagId)).ToArray());

    // ── Private create-core ───────────────────────────────────────────────────────

    /// <summary>
    /// Inserts <c>Notification</c> rows for the given <paramref name="targets"/>, enforcing:
    /// <list type="bullet">
    ///   <item><b>Drop-self (conditional, D4):</b> a target is skipped only when there IS an actor
    ///   and the recipient is that actor (<c>sourceUserId is int s &amp;&amp; recipientId == s</c>). A
    ///   null source — no actor — drops nobody.</item>
    ///   <item><b>Within-batch dedup:</b> duplicate <c>recipientId</c> values in
    ///   <paramref name="targets"/> are collapsed (first-wins).</item>
    ///   <item><b>Cross-existing dedup:</b> recipients who already hold an unread notification
    ///   of the same <paramref name="type"/> + <paramref name="sourceUserId"/> + related entity are
    ///   skipped (absorbs idempotent or retry-style callers). A null source matches a null source.
    ///   Skipped entirely for <see cref="CrossExistingDedupExempt"/>.</item>
    /// </list>
    /// All remaining rows are bulk-inserted in a single <c>SaveChangesAsync</c>. No-ops when
    /// every target is filtered out.
    /// </summary>
    /// <param name="type">The notification type to create.</param>
    /// <param name="sourceUserId">
    /// The actor whose action triggered the notification, or <c>null</c> for no actor
    /// (system-sourced, self-caused, or de-identified — the moderation band and type 26). A type with
    /// a real actor must always pass it: null removes drop-self's protection (D4 guardrail).
    /// </param>
    /// <param name="targets">
    /// Each element is <c>(recipientId, relatedEntityId)</c> — the single most specific entity of the
    /// event, interpreted per type (D16); <c>0</c> = none.
    /// </param>
    private async Task CreateCoreAsync(
        NotificationTypeEnum type,
        int? sourceUserId,
        IReadOnlyList<(int recipientId, long relatedEntityId)> targets)
    {
        // Step 1 — drop-self + within-batch dedup (first-wins on duplicate recipientId).
        Dictionary<int, long> deduped = new(); // recipientId → relatedEntityId
        foreach (var (recipientId, relatedEntityId) in targets)
        {
            if (sourceUserId is int source && recipientId == source) continue; // drop self (D4)
            deduped.TryAdd(recipientId, relatedEntityId);
        }

        if (deduped.Count == 0) return;

        // Step 2 — cross-existing dedup: skip recipients who already hold an unread notification of
        // this type + source + related entity. RelatedEntityId is in the key so two notifications
        // about *different* targets both arrive — for the moderation band that target is the report
        // id (D4), so two removals of one author's items are two rows. The D4-exempt types have no
        // related entity at all (0) and skip this step: two warnings must be two rows.
        HashSet<(int recipientId, long relatedEntityId)> alreadyNotified = [];
        if (!CrossExistingDedupExempt.Contains(type))
        {
            IReadOnlyList<int> candidateIds = [.. deduped.Keys];

            // `n.SourceUserId == sourceUserId` with a null sourceUserId translates to IS NULL (EF's
            // C# null semantics), so null-sourced rows dedup against null-sourced rows only.
            var existingPairs = await writeDb.Notifications
                .Where(n =>
                    candidateIds.Contains(n.RecipientUserId) &&
                    n.NotificationTypeId == type &&
                    n.SourceUserId == sourceUserId &&
                    !n.IsRead)
                .Select(n => new { n.RecipientUserId, n.RelatedEntityId })
                .ToListAsync();

            alreadyNotified = existingPairs.Select(x => (x.RecipientUserId, x.RelatedEntityId)).ToHashSet();
        }

        List<Notification> rows = deduped
            .Where(kv => !alreadyNotified.Contains((kv.Key, kv.Value)))
            .Select(kv => new Notification
            {
                RecipientUserId = kv.Key,
                NotificationTypeId = type,
                SourceUserId = sourceUserId,
                RelatedEntityId = kv.Value,
                IsRead = false,
                DateCreated = DateTime.UtcNow
            })
            .ToList();

        if (rows.Count == 0) return;

        writeDb.Notifications.AddRange(rows);
        await writeDb.SaveChangesAsync();

        // Email fan-out hand-off. After the commit, so every queued id references a durable row
        // (identity values are populated by SaveChangesAsync). Enqueue is an in-memory append and
        // cannot throw for a caller-visible reason, but it is still not allowed to break the
        // notification: the in-app row is the contract, mail is the side-channel.
        int dropped = emailBuffer.Enqueue(rows.Select(r => r.NotificationId));
        if (dropped > 0)
        {
            logger.LogError(
                "Notification email buffer at capacity ({MaxDepth}); dropped {DroppedCount} of {RowCount} {NotificationType} notification(s) without sending. The drain has stalled.",
                NotificationEmailBuffer.MaxDepth, dropped, rows.Count, type);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

}
