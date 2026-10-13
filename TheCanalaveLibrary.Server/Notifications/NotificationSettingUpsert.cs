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
    /// returns <see cref="UnsubscribeResult.Applied"/>. Mail clients and link scanners both re-POST
    /// these URLs, so that is a requirement, not a nicety; two concurrent POSTs both succeed (see the
    /// class doc).</para>
    ///
    /// <para><b>The account may be gone.</b> The signed token lives 365 days and account deletion is
    /// a hard delete, so a re-POST of an old message's link can name a user who no longer exists. The
    /// insert is conditional on the user existing (the <c>ReadingProgressFlusher</c> deleted-user
    /// shape), so it never trips the <c>user_id</c> foreign key into a 500, and the caller answers
    /// <see cref="UnsubscribeResult.UserGone"/> as an invalid link (WU-NotificationCorrectness review
    /// fixes, 2026-10-13).</para>
    /// </summary>
    public static async Task<UnsubscribeResult> UnsubscribeAsync(
        ApplicationDbContext writeDb,
        ReadOnlyApplicationDbContext readDb,
        int userId,
        NotificationTypeEnum notifType,
        CancellationToken cancellationToken = default)
    {
        NotificationType? type = await readDb.NotificationTypes
            .FirstOrDefaultAsync(t => t.NotificationTypeId == notifType, cancellationToken);
        if (type is null) return UnsubscribeResult.UnknownType;

        bool? storedEmail = StoredValue(false, type.DefaultEmailEnabled);

        if (storedEmail is bool off)
        {
            // Insert with collapsed NULL (tracks the default), or update email_enabled alone. The
            // SELECT … WHERE EXISTS yields no row for a deleted account, so nothing is written (and
            // the FK is never reached); an existing row of a live account is updated by ON CONFLICT.
            int written = await writeDb.Database.ExecuteSqlAsync($"""
                INSERT INTO user_notification_settings (user_id, notification_type_id, email_enabled, collapsed)
                SELECT {userId}, {(short)notifType}, {off}, NULL::boolean
                WHERE EXISTS (SELECT 1 FROM "AspNetUsers" u WHERE u.id = {userId})
                ON CONFLICT (user_id, notification_type_id)
                DO UPDATE SET email_enabled = EXCLUDED.email_enabled
                """, cancellationToken);
            return written > 0 ? UnsubscribeResult.Applied : UnsubscribeResult.UserGone;
        }

        // The type already defaults email off: drop an email override if one exists (no row → no-op),
        // then remove the row if that left both columns NULL.
        int cleared = await writeDb.UserNotificationSettings
            .Where(s => s.UserId == userId && s.NotificationTypeId == notifType && s.EmailEnabled != null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.EmailEnabled, (bool?)null), cancellationToken);
        await DeleteIfBothDefaultAsync(writeDb, userId, notifType, cancellationToken);

        // Nothing to clear is the normal idempotent case — unless the account itself is gone, which
        // answers the same way as the insert branch above.
        if (cleared == 0 && !await writeDb.Users.AnyAsync(u => u.Id == userId, cancellationToken))
            return UnsubscribeResult.UserGone;
        return UnsubscribeResult.Applied;
    }

    /// <summary>What <see cref="UnsubscribeAsync"/> did.</summary>
    public enum UnsubscribeResult
    {
        /// <summary>Email is off for the type (or already was) — the success page.</summary>
        Applied,
        /// <summary>The token named a type that is not seeded — nothing written.</summary>
        UnknownType,
        /// <summary>The token's account no longer exists — nothing written; answered as an invalid link.</summary>
        UserGone,
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
