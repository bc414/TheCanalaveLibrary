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
/// on <c>SourceUserId</c>); the second pass delegates to <see cref="NotificationEnricher"/>,
/// which classifies each row by entity kind and batch-loads each distinct kind present on the page
/// (one query per kind, none if the kind is absent). See <c>layer2-services.md</c>
/// §"Polymorphic RelatedEntityId — Two-Pass Batch Enrichment." That second pass used to live here
/// as private members; it moved out at WU-NotifEmail (2026-07-31) so the notification-email flusher
/// could produce the same titles and links without going through this user-scoped read path.</para>
///
/// <para><see cref="GetSettingsAsync"/> LEFT-JOINs settings onto types; NULL means "use
/// default" (sparse-override model, Feature 43).</para>
/// </summary>
public class ServerNotificationReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser) : INotificationReadService
{
    // ── Protected surface for the derived write service ────────────────────────────

    /// <summary>
    /// Exposed so the derived write service can create read contexts without re-capturing
    /// the <c>readDbFactory</c> primary constructor parameter (avoids CS9107 double-capture warning
    /// — see <c>layer2-services.md</c> §"CS9107/CS9124: shared constructor parameters").
    /// Every method creates its own short-lived context (`await using`) — see
    /// <c>layer2-services.md</c> §"Read-context concurrency: factory per method".
    /// </summary>
    protected IDbContextFactory<ReadOnlyApplicationDbContext> ReadDbFactory { get; } = readDbFactory;

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

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        return await readDb.Notifications
            .CountAsync(n => n.RecipientUserId == userId && !n.IsRead);
    }

    public async Task<int> GetTotalCountAsync()
    {
        int? userId = activeUser.UserId;
        if (userId is null) return 0;

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        return await readDb.Notifications
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

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // ── First pass: materialize the page ──────────────────────────────────────
        // LEFT JOINs:
        //   • UserNotificationSettings (sparse) → effective Collapsed per type.
        //   • Users on SourceUserId (int?) → SourceUserName; null when source deleted
        //     (SET NULL policy) or type has no actor field.
        var q =
            from n in readDb.Notifications
            where n.RecipientUserId == userId
            join nt in readDb.NotificationTypes
                on n.NotificationTypeId equals nt.NotificationTypeId
            join uns in readDb.UserNotificationSettings.Where(s => s.UserId == userId)
                on n.NotificationTypeId equals uns.NotificationTypeId into settings
            from s in settings.DefaultIfEmpty()
            join u in readDb.Users
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
        // One query per entity kind present on this page; kinds absent produce no query.
        // Reuses this method's context (sequential within the method — no concurrency).
        var targets = await NotificationEnricher.ResolveTargetsAsync(
            readDb,
            rows.Select(r => (r.NotificationTypeId, r.RelatedEntityId)).ToList());

        // ── Stitch: merge enriched fields into DTOs ────────────────────────────────
        return [..rows.Select(r =>
        {
            (string? targetTitle, string? targetUrl) =
                targets.TryGetValue((r.NotificationTypeId, r.RelatedEntityId), out var target)
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

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        if (userId is null)
        {
            // Anonymous: return defaults for all types (IsDefault = true — no override rows).
            return await readDb.NotificationTypes
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
            from nt in readDb.NotificationTypes
            join uns in readDb.UserNotificationSettings.Where(s => s.UserId == userId)
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

}
