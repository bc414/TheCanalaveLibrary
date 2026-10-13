using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The sparse-override write rule for <see cref="UserNotificationSetting"/>, in one place.
///
/// <para><b>The rule (owner ruling D19): sparse per field, not per row.</b> Each column stores NULL
/// when its value equals the type's default for that field, so "NULL" is the canonical
/// representation of "default" and a later change to a type's <c>DefaultEmailEnabled</c> (or
/// <c>DefaultCollapsed</c>) reaches every user who never chose that field — including users who did
/// override the <em>other</em> field. The row exists only while at least one column is non-NULL: it
/// is deleted when both are NULL. A row-level rule (the shape before D19) pinned an
/// <c>EmailEnabled</c> the user never chose the moment they collapsed a panel section, because the
/// settings page submits both effective values on every toggle. Reimplementing any of this in a
/// second caller is how stale values start pinning users to an old default — so both callers land
/// here, and <see cref="StoredValue"/> / <see cref="DeleteIfBothDefaultAsync"/> are its only copies.</para>
///
/// <para><b>Each writer owns only its own columns.</b> <see cref="ApplyAsync"/> (the settings page)
/// owns both; <see cref="UnsubscribeAsync"/> (the one-click email link) owns <c>email_enabled</c>
/// only and never reads or rewrites <c>collapsed</c>.</para>
///
/// <para><b>Race-free (D23: "a visible 500 is never a posture").</b> A non-delete write is a single
/// parameterized <c>INSERT … ON CONFLICT (user_id, notification_type_id) DO UPDATE</c> that sets only
/// the writer's columns, so two concurrent writers for one key (two re-POSTs of the same unsubscribe
/// link — mail clients and link scanners both do it) cannot collide on the primary key. A statement
/// that can leave both columns NULL is followed by <see cref="DeleteIfBothDefaultAsync"/>, whose
/// <c>IS NULL</c> predicate makes it safe against a concurrent writer that has just set a column.
/// Nothing here tracks a <see cref="UserNotificationSetting"/> entity, so the shared scoped write
/// context never holds a stale copy of a row these statements change.</para>
///
/// <para>Extracted at WU-NotifEmail (2026-07-31) when the one-click unsubscribe endpoint became a
/// second writer: it acts on a user resolved from a signed token, not from
/// <c>IActiveUserContext</c>, so it cannot go through
/// <see cref="ServerNotificationWriteService.SetSettingAsync"/>. Rewritten per field at
/// WU-NotificationCorrectness (2026-10-12). Rule text: <c>layer2-services.md</c>
/// §"Notification Generation" → "Filtering semantics".</para>
/// </summary>
public static class NotificationSettingUpsert
{
    /// <summary>
    /// Applies both effective settings for one (user, type): each field equal to its type default
    /// is stored as NULL, and the row is deleted when both are. No-ops when
    /// <paramref name="notifType"/> is not a seeded type.
    /// </summary>
    public static async Task ApplyAsync(
        ApplicationDbContext writeDb,
        ReadOnlyApplicationDbContext readDb,
        int userId,
        NotificationTypeEnum notifType,
        bool emailEnabled,
        bool collapsed,
        CancellationToken cancellationToken = default)
    {
        NotificationType? type = await readDb.NotificationTypes
            .FirstOrDefaultAsync(t => t.NotificationTypeId == notifType, cancellationToken);
        if (type is null) return; // unknown type enum — no-op (should not happen in practice)

        bool? storedEmail = StoredValue(emailEnabled, type.DefaultEmailEnabled);
        bool? storedCollapsed = StoredValue(collapsed, type.DefaultCollapsed);

        if (storedEmail is null && storedCollapsed is null)
        {
            // Both fields follow their defaults: no row at all.
            await writeDb.UserNotificationSettings
                .Where(s => s.UserId == userId && s.NotificationTypeId == notifType)
                .ExecuteDeleteAsync(cancellationToken);
            return;
        }

        // At least one column is non-NULL, so this statement can never leave an all-NULL row.
        await writeDb.Database.ExecuteSqlAsync($"""
            INSERT INTO user_notification_settings (user_id, notification_type_id, email_enabled, collapsed)
            VALUES ({userId}, {(short)notifType}, {storedEmail}, {storedCollapsed})
            ON CONFLICT (user_id, notification_type_id)
            DO UPDATE SET email_enabled = EXCLUDED.email_enabled, collapsed = EXCLUDED.collapsed
            """, cancellationToken);
    }

    /// <summary>
    /// Turns email off for one (user, type) — the one-click unsubscribe action. Writes only the
    /// <c>email_enabled</c> column: <c>false</c> when the type defaults email on, NULL (follow the
    /// default, which is already off) when it does not. The <c>collapsed</c> column is never read or
    /// written, so an unrelated display preference survives, and a user with no row keeps tracking
    /// <c>DefaultCollapsed</c>.
    ///
    /// <para>Idempotent — unsubscribing an already-unsubscribed type rewrites the same state and
    /// returns <c>true</c>. Mail clients and link scanners both re-POST these URLs, so that is a
    /// requirement, not a nicety; two concurrent POSTs both succeed (see the class doc).</para>
    /// </summary>
    /// <returns><c>false</c> when the type is unknown; otherwise <c>true</c>.</returns>
    public static async Task<bool> UnsubscribeAsync(
        ApplicationDbContext writeDb,
        ReadOnlyApplicationDbContext readDb,
        int userId,
        NotificationTypeEnum notifType,
        CancellationToken cancellationToken = default)
    {
        NotificationType? type = await readDb.NotificationTypes
            .FirstOrDefaultAsync(t => t.NotificationTypeId == notifType, cancellationToken);
        if (type is null) return false;

        bool? storedEmail = StoredValue(false, type.DefaultEmailEnabled);

        if (storedEmail is bool off)
        {
            // Insert with collapsed NULL (tracks the default), or update email_enabled alone.
            await writeDb.Database.ExecuteSqlAsync($"""
                INSERT INTO user_notification_settings (user_id, notification_type_id, email_enabled, collapsed)
                VALUES ({userId}, {(short)notifType}, {off}, NULL)
                ON CONFLICT (user_id, notification_type_id)
                DO UPDATE SET email_enabled = EXCLUDED.email_enabled
                """, cancellationToken);
            return true;
        }

        // The type already defaults email off: drop an email override if one exists (no row → no-op),
        // then remove the row if that left both columns NULL.
        await writeDb.UserNotificationSettings
            .Where(s => s.UserId == userId && s.NotificationTypeId == notifType && s.EmailEnabled != null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.EmailEnabled, (bool?)null), cancellationToken);
        await DeleteIfBothDefaultAsync(writeDb, userId, notifType, cancellationToken);
        return true;
    }

    /// <summary>
    /// The canonical stored form of one field: NULL when the effective value equals the type's
    /// default for it (so a later default change reaches this user), the value otherwise.
    /// </summary>
    private static bool? StoredValue(bool effective, bool typeDefault) =>
        effective == typeDefault ? null : effective;

    /// <summary>
    /// Deletes the (user, type) row only if both columns are NULL — the all-default state, which is
    /// represented by "no row". The predicate makes it a no-op when a concurrent writer has just set
    /// either column.
    /// </summary>
    private static Task<int> DeleteIfBothDefaultAsync(
        ApplicationDbContext writeDb, int userId, NotificationTypeEnum notifType, CancellationToken cancellationToken) =>
        writeDb.UserNotificationSettings
            .Where(s => s.UserId == userId && s.NotificationTypeId == notifType
                        && s.EmailEnabled == null && s.Collapsed == null)
            .ExecuteDeleteAsync(cancellationToken);
}
