using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The sparse-override write rule for <see cref="UserNotificationSetting"/>, in one place.
///
/// <para>The rule that makes this worth extracting: <b>an override row exists only while it
/// differs from the type's defaults.</b> Setting a value back to its default <em>deletes</em> the
/// row rather than storing a redundant copy — "no row" is the canonical representation of
/// "default," which is what lets a later change to a type's <c>DefaultEmailEnabled</c> propagate to
/// every user who never expressed a preference. Reimplementing that in a second caller is how
/// stale rows start pinning users to an old default.</para>
///
/// <para>Extracted at WU-NotifEmail (2026-07-31) when the one-click unsubscribe endpoint became a
/// second writer: it acts on a user resolved from a signed token, not from
/// <c>IActiveUserContext</c>, so it cannot go through
/// <see cref="ServerNotificationWriteService.SetSettingAsync"/>. Both callers land here.</para>
/// </summary>
public static class NotificationSettingUpsert
{
    /// <summary>
    /// Applies both settings for one (user, type), collapsing to "no row" when they match the
    /// type's defaults. No-ops when <paramref name="notifType"/> is not a seeded type.
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

        bool matchesDefault = emailEnabled == type.DefaultEmailEnabled
                              && collapsed == type.DefaultCollapsed;

        if (matchesDefault)
        {
            // Sparse model: delete the override row so that "no row" = "use default."
            await writeDb.UserNotificationSettings
                .Where(s => s.UserId == userId && s.NotificationTypeId == notifType)
                .ExecuteDeleteAsync(cancellationToken);
            return;
        }

        UserNotificationSetting? existing = await writeDb.UserNotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == userId && s.NotificationTypeId == notifType, cancellationToken);

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

        await writeDb.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Turns email off for one (user, type) while preserving that user's effective
    /// <c>Collapsed</c> preference — the one-click unsubscribe action.
    ///
    /// <para>Reading <c>Collapsed</c> first matters: unsubscribe must not silently reset an
    /// unrelated display preference as a side effect, and passing a hardcoded <c>false</c> would do
    /// exactly that for any type whose default is <c>true</c> or any user who overrode it.</para>
    ///
    /// <para>Idempotent — unsubscribing an already-unsubscribed type rewrites the same state.
    /// Mail clients and link scanners both re-POST these URLs, so that is a requirement, not a
    /// nicety.</para>
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

        UserNotificationSetting? existing = await readDb.UserNotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == userId && s.NotificationTypeId == notifType, cancellationToken);

        bool effectiveCollapsed = existing?.Collapsed ?? type.DefaultCollapsed;

        await ApplyAsync(writeDb, readDb, userId, notifType,
            emailEnabled: false, collapsed: effectiveCollapsed, cancellationToken);
        return true;
    }
}
