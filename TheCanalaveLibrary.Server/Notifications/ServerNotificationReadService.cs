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
/// <para><see cref="GetNotificationsAsync"/> LEFT-JOINs <c>UserNotificationSettings</c> to
/// produce the effective <c>Collapsed</c> value (user override when a row exists, otherwise
/// <c>NotificationType.DefaultCollapsed</c>).</para>
///
/// <para><see cref="GetSettingsAsync"/> LEFT-JOINs settings onto types; NULL means "use
/// default" (sparse-override model, Feature 43).</para>
/// </summary>
public class ServerNotificationReadService(
    ReadOnlyApplicationDbContext readDb,
    IActiveUserContext activeUser) : INotificationReadService
{
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

    public async Task<int> GetUnreadCountAsync()
    {
        int? userId = activeUser.UserId;
        if (userId is null) return 0;

        return await ReadDb.Notifications
            .CountAsync(n => n.RecipientUserId == userId && !n.IsRead);
    }

    public async Task<NotificationDto[]> GetNotificationsAsync(int page, int pageSize)
    {
        int? userId = activeUser.UserId;
        if (userId is null) return [];

        int skip = (page - 1) * pageSize;

        // LEFT JOIN UserNotificationSettings to resolve the effective Collapsed value per
        // notification type (sparse-override model: NULL row → fall back to type default).
        return await (
            from n in ReadDb.Notifications
            where n.RecipientUserId == userId
            join nt in ReadDb.NotificationTypes
                on n.NotificationTypeId equals nt.NotificationTypeId
            join uns in ReadDb.UserNotificationSettings.Where(s => s.UserId == userId)
                on n.NotificationTypeId equals uns.NotificationTypeId into settings
            from s in settings.DefaultIfEmpty()
            orderby n.DateCreated descending
            select new NotificationDto(
                n.NotificationId,
                n.NotificationTypeId,
                nt.NotificationCategory,
                n.SourceUserId,
                n.RelatedEntityId,
                n.IsRead,
                n.DateCreated,
                s != null ? s.Collapsed : nt.DefaultCollapsed
            )
        ).Skip(skip).Take(pageSize).ToArrayAsync();
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
}
