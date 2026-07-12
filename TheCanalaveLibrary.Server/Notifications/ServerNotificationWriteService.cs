using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation of <see cref="INotificationWriteService"/>. Inherits
/// <see cref="ServerNotificationReadService"/> for the CQRS-lite read path.
///
/// <para><b>Private create-core (<see cref="CreateCoreAsync"/>).</b> All <c>NotifyNew*Async</c>
/// methods are thin wrappers over this single private method, which owns the two universal
/// invariants: <b>drop-self</b> (a user is never notified of their own action) and
/// <b>dedup</b> (skip a recipient who already holds an unread notification for the same
/// type + source + related entity — prevents duplicate notifications from idempotent
/// or retry-style call sites). There is <em>no</em> public generic <c>CreateAsync</c> that
/// bypasses these invariants — see <c>cross-cutting.md</c> "Notification Creation" for the
/// rationale.</para>
///
/// <para><b>DAG rule:</b> fan-out <c>NotifyNew*</c> methods that need to resolve a
/// recipient list (e.g. followers of an author) will inject <em>read</em> services (e.g.
/// <c>IFollowingReadService</c>) when those methods land with their work-units. No write
/// service of a feature that calls this service is injected here — that would create a
/// cycle. See <c>layer2-services.md</c> "The DAG rule."</para>
///
/// <para><b>Best-effort post-commit:</b> callers invoke the <c>NotifyNew*Async</c> methods
/// after their own <c>SaveChangesAsync</c>, inside a <c>try/catch</c> that logs and swallows.
/// This service's own <c>SaveChangesAsync</c> inside <see cref="CreateCoreAsync"/> is a
/// separate transaction covering only the notification rows.</para>
/// </summary>
public class ServerNotificationWriteService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser)
    : ServerNotificationReadService(readDbFactory, activeUser), INotificationWriteService
{
    // ── Read-side mutations ──────────────────────────────────────────────────────

    public async Task MarkAsReadAsync(long notificationId)
    {
        int userId = RequireAuthenticatedUser();
        await writeDb.Notifications
            .Where(n => n.NotificationId == notificationId
                        && n.RecipientUserId == userId
                        && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    public async Task MarkAllAsReadAsync()
    {
        int userId = RequireAuthenticatedUser();
        await writeDb.Notifications
            .Where(n => n.RecipientUserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    // ── Settings ─────────────────────────────────────────────────────────────────

    public async Task SetSettingAsync(NotificationTypeEnum notifType, bool emailEnabled, bool collapsed)
    {
        int userId = RequireAuthenticatedUser();

        // Load the type defaults from the read context (no-tracking, fast).
        // Uses ReadDbFactory (the protected property on the base class), not the readDbFactory
        // constructor parameter directly, to avoid CS9107 double-capture (layer2-services.md).
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        NotificationType? type = await readDb.NotificationTypes
            .FirstOrDefaultAsync(t => t.NotificationTypeId == notifType);
        if (type is null) return; // unknown type enum — no-op (should not happen in practice)

        bool matchesDefault = emailEnabled == type.DefaultEmailEnabled
                              && collapsed == type.DefaultCollapsed;

        if (matchesDefault)
        {
            // Sparse model: delete the override row so that NULL = "use default."
            await writeDb.UserNotificationSettings
                .Where(s => s.UserId == userId && s.NotificationTypeId == notifType)
                .ExecuteDeleteAsync();
        }
        else
        {
            UserNotificationSetting? existing = await writeDb.UserNotificationSettings
                .FirstOrDefaultAsync(s => s.UserId == userId && s.NotificationTypeId == notifType);

            if (existing is null)
            {
                writeDb.UserNotificationSettings.Add(new UserNotificationSetting
                {
                    UserId = userId,
                    NotificationTypeId = notifType,
                    EmailEnabled = emailEnabled,
                    Collapsed = collapsed
                });
            }
            else
            {
                existing.EmailEnabled = emailEnabled;
                existing.Collapsed = collapsed;
            }

            await writeDb.SaveChangesAsync();
        }
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

    // ── Semantic generation methods (WU32 slice — group fan-out) ─────────────────

    /// <inheritdoc/>
    public async Task NotifyNewGroupStoryAsync(int groupId, int storyAuthorId, int sourceUserId)
    {
        // Fan-out to all members with NotifyForNewStory = true (type NewGroupStory = 60).
        List<int> memberIds = await writeDb.GroupMembers
            .Where(m => m.GroupId == groupId && m.NotifyForNewStory)
            .Select(m => m.UserId)
            .ToListAsync();

        // Build (recipientId, relatedEntityId=groupId) pairs for fan-out.
        IReadOnlyList<(int recipientId, int relatedEntityId)> fanOutTargets =
            memberIds.Select(id => (id, groupId)).ToArray();

        if (fanOutTargets.Count > 0)
            await CreateCoreAsync(NotificationTypeEnum.NewGroupStory, sourceUserId, fanOutTargets);

        // Also notify the story author (YourStoryAddedToGroup). Drop-self handled by create-core.
        await CreateCoreAsync(
            NotificationTypeEnum.YourStoryAddedToGroup,
            sourceUserId,
            [(storyAuthorId, groupId)]);
    }

    /// <inheritdoc/>
    public async Task NotifyNewGroupBlogPostAsync(int groupId, int blogPostId, int authorId)
    {
        // Fan-out to all members with NotifyForNewBlogPost = true (type NewGroupBlogPost = 61).
        List<int> memberIds = await writeDb.GroupMembers
            .Where(m => m.GroupId == groupId && m.NotifyForNewBlogPost)
            .Select(m => m.UserId)
            .ToListAsync();

        IReadOnlyList<(int recipientId, int relatedEntityId)> targets =
            memberIds.Select(id => (id, blogPostId)).ToArray();

        if (targets.Count > 0)
            await CreateCoreAsync(NotificationTypeEnum.NewGroupBlogPost, authorId, targets);
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

    // ── Semantic generation methods (WU34 slice — moderation) ────────────────────

    /// <inheritdoc/>
    public Task NotifyReportReceivedAsync(int reporterUserId, int moderatorSourceId) =>
        CreateCoreAsync(NotificationTypeEnum.ReportReceived, moderatorSourceId,
            [(reporterUserId, 0)]);

    /// <inheritdoc/>
    public Task NotifyReportResolvedAsync(int reporterUserId, int moderatorSourceId) =>
        CreateCoreAsync(NotificationTypeEnum.ReportResolved, moderatorSourceId,
            [(reporterUserId, 0)]);

    /// <inheritdoc/>
    public Task NotifyReportResolvedNoActionAsync(int reporterUserId, int moderatorSourceId) =>
        CreateCoreAsync(NotificationTypeEnum.ReportResolvedNoAction, moderatorSourceId,
            [(reporterUserId, 0)]);

    /// <inheritdoc/>
    public Task NotifyContentRemovedAsync(int contentAuthorUserId, int moderatorSourceId) =>
        CreateCoreAsync(NotificationTypeEnum.ContentRemoved, moderatorSourceId,
            [(contentAuthorUserId, 0)]);

    /// <inheritdoc/>
    public Task NotifyStoryApprovedAsync(int storyAuthorUserId, int storyId, int moderatorSourceId) =>
        CreateCoreAsync(NotificationTypeEnum.StoryApproved, moderatorSourceId,
            [(storyAuthorUserId, storyId)]);

    /// <inheritdoc/>
    public Task NotifyStoryRejectedAsync(int storyAuthorUserId, int storyId, int moderatorSourceId) =>
        CreateCoreAsync(NotificationTypeEnum.StoryRejected, moderatorSourceId,
            [(storyAuthorUserId, storyId)]);

    /// <inheritdoc/>
    public Task NotifyAccountWarningAsync(int targetUserId, int moderatorSourceId) =>
        CreateCoreAsync(NotificationTypeEnum.AccountWarning, moderatorSourceId,
            [(targetUserId, 0)]);

    /// <inheritdoc/>
    public Task NotifyAccountSuspendedAsync(int targetUserId, int moderatorSourceId) =>
        CreateCoreAsync(NotificationTypeEnum.AccountSuspended, moderatorSourceId,
            [(targetUserId, 0)]);

    /// <inheritdoc/>
    public Task NotifyAccountBannedAsync(int targetUserId, int moderatorSourceId) =>
        CreateCoreAsync(NotificationTypeEnum.AccountBanned, moderatorSourceId,
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
            voterUserIds.Select(id => (id, relatedEntityId)).ToArray());

    // ── Private create-core ───────────────────────────────────────────────────────

    /// <summary>
    /// Inserts <c>Notification</c> rows for the given <paramref name="targets"/>, enforcing:
    /// <list type="bullet">
    ///   <item><b>Drop-self:</b> any target whose <c>recipientId == sourceUserId</c> is silently
    ///   skipped (users are never notified of their own actions).</item>
    ///   <item><b>Within-batch dedup:</b> duplicate <c>recipientId</c> values in
    ///   <paramref name="targets"/> are collapsed (first-wins).</item>
    ///   <item><b>Cross-existing dedup:</b> recipients who already hold an unread notification
    ///   of the same <paramref name="type"/> + <paramref name="sourceUserId"/> + related entity
    ///   are skipped (prevents duplicate notifications from idempotent or retry-style callers).
    ///   </item>
    /// </list>
    /// All remaining rows are bulk-inserted in a single <c>SaveChangesAsync</c>. No-ops when
    /// every target is filtered out.
    /// </summary>
    /// <param name="type">The notification type to create.</param>
    /// <param name="sourceUserId">The user whose action triggered the notification.</param>
    /// <param name="targets">
    /// Each element is <c>(recipientId, relatedEntityId)</c>. For follow/vouch the
    /// <c>relatedEntityId</c> is the source user's id; for chapter notifications it will be
    /// the chapter id; etc. — polymorphic, type-specific.
    /// </param>
    private async Task CreateCoreAsync(
        NotificationTypeEnum type,
        int sourceUserId,
        IReadOnlyList<(int recipientId, int relatedEntityId)> targets)
    {
        // Step 1 — drop-self + within-batch dedup (first-wins on duplicate recipientId).
        Dictionary<int, int> deduped = new(); // recipientId → relatedEntityId
        foreach (var (recipientId, relatedEntityId) in targets)
        {
            if (recipientId != sourceUserId) // drop self
                deduped.TryAdd(recipientId, relatedEntityId);
        }

        if (deduped.Count == 0) return;

        // Step 2 — cross-existing dedup: check all candidate recipients in one query,
        // skipping those who already have an unread notification of this type + source + related.
        IReadOnlyList<int> candidateIds = [.. deduped.Keys];

        // Cross-existing dedup: load existing unread notifications of this type + source for the
        // candidates, then match (recipientId, relatedEntityId) in memory. Including RelatedEntityId
        // in the key ensures two notifications from the same source about *different* targets
        // (e.g. two content-removed notifications for different stories) both reach the recipient.
        var existingPairs = await writeDb.Notifications
            .Where(n =>
                candidateIds.Contains(n.RecipientUserId) &&
                n.NotificationTypeId == type &&
                n.SourceUserId == sourceUserId &&
                !n.IsRead)
            .Select(n => new { n.RecipientUserId, n.RelatedEntityId })
            .ToListAsync();

        HashSet<(int recipientId, int relatedEntityId)> alreadyNotified =
            existingPairs.Select(x => (x.RecipientUserId, x.RelatedEntityId)).ToHashSet();

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
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private int RequireAuthenticatedUser()
    {
        // Uses the base class's CurrentUserId property so the derived class doesn't capture
        // the activeUser primary constructor parameter (avoids CS9107 double-capture warning).
        if (CurrentUserId is not int id)
            throw new InvalidOperationException("This operation requires an authenticated user.");
        return id;
    }
}
