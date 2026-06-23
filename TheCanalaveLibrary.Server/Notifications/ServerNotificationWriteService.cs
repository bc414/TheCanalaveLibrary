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
    ReadOnlyApplicationDbContext readDb,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser)
    : ServerNotificationReadService(readDb, activeUser), INotificationWriteService
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
        // Uses ReadDb (the protected property on the base class), not the readDb constructor
        // parameter directly, to avoid CS9107 double-capture (layer2-services.md).
        NotificationType? type = await ReadDb.NotificationTypes
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

        HashSet<int> alreadyNotified = (await writeDb.Notifications
            .Where(n =>
                candidateIds.Contains(n.RecipientUserId) &&
                n.NotificationTypeId == type &&
                n.SourceUserId == sourceUserId &&
                !n.IsRead)
            .Select(n => n.RecipientUserId)
            .ToListAsync()).ToHashSet();

        List<Notification> rows = deduped
            .Where(kv => !alreadyNotified.Contains(kv.Key))
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
