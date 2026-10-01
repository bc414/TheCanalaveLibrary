using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation of <see cref="IFollowingWriteService"/>. Inherits
/// <see cref="ServerFollowingReadService"/> for the CQRS-lite read path.
///
/// <b>Sanitization contract:</b> <c>VouchText</c> is rich HTML authored in <c>EditorView</c>;
/// <see cref="VouchAsync"/> sanitizes it via <see cref="IHtmlSanitizationService"/> before persisting
/// (sanitize-once-on-save — <c>layer2-services.md</c>). The stored value in the DTO is therefore
/// already trusted; <c>RichTextView</c> renders it directly without re-sanitizing.
///
/// <b>Notification seam (WU22):</b> <see cref="FollowAsync"/> and <see cref="VouchAsync"/> call
/// <see cref="INotificationWriteService"/> after their primary <c>SaveChangesAsync</c> (best-effort
/// post-commit — see <c>cross-cutting.md</c> "Notification Creation"). Any notification failure is
/// logged and swallowed; it never rolls back the primary action.
/// </summary>
public class ServerFollowingWriteService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    IHtmlSanitizationService sanitizer,
    INotificationWriteService notifications,
    ILogger<ServerFollowingWriteService> logger)
    : ServerFollowingReadService(readDbFactory, activeUser), IFollowingWriteService
{
    /// <summary>
    /// Kind (g): the parent here is the target's profile. Both reads in
    /// <see cref="ServerFollowingReadService"/> have called <c>ProfileVisibilityGuard</c> since
    /// WU-AccessGate; neither write did, so a Private profile still accrued followers and vouches —
    /// each bumping the target's public <c>UserStats.FollowerCount</c> and firing a notification, and
    /// in the vouch case persisting attacker-authored HTML onto a profile the actor cannot open.
    /// <para>
    /// Raises only (owner ruling D6): <see cref="FollowAsync"/>, <see cref="VouchAsync"/> and
    /// <see cref="SetReceiveAlertsAsync"/> switching alerts on. The clears —
    /// <see cref="UnfollowAsync"/>, <see cref="RemoveVouchAsync"/>, alerts off — are deliberately
    /// unguarded, a recorded conformance enrolled in <c>ParentVisibilityContractTests</c>.
    /// </para>
    /// </summary>
    private async Task RequireProfileVisibleAsync(int targetUserId)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        if (!await ProfileVisibilityGuard.IsProfileVisibleAsync(readDb, ActiveUser, targetUserId))
            throw new KeyNotFoundException($"User {targetUserId} not found.");
    }

    public async Task FollowAsync(int targetUserId)
    {
        int actorId = ActiveUser.RequireUserId();

        if (actorId == targetUserId)
            throw new FollowingValidationException(["A user cannot follow themselves."]);

        await RequireProfileVisibleAsync(targetUserId);

        bool alreadyFollowing = await writeDb.FollowedUsers
            .AnyAsync(f => f.UserId == actorId && f.FollowedUserId == targetUserId);

        if (alreadyFollowing) return; // idempotent

        writeDb.FollowedUsers.Add(new FollowedUser
        {
            UserId = actorId,
            FollowedUserId = targetUserId,
            ReceiveAlerts = true,
            DateFollowed = DateTime.UtcNow
        });

        await writeDb.SaveChangesAsync();

        // Increment UserStats counters for both sides (layer2-services.md §"UserStats Updates").
        await writeDb.UserStats.Where(us => us.UserId == targetUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.FollowerCount, us => us.FollowerCount + 1));
        await writeDb.UserStats.Where(us => us.UserId == actorId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.AuthorsFollowed, us => us.AuthorsFollowed + 1));

        // Best-effort post-commit notification (WU22). Primary save already committed above;
        // a notification failure must not roll back the follow. See cross-cutting.md.
        try { await notifications.NotifyNewFollowerAsync(targetUserId, actorId); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "NewFollower notification failed for user {UserId} following {TargetUserId}",
                actorId, targetUserId);
        }
    }

    public async Task UnfollowAsync(int targetUserId)
    {
        int actorId = ActiveUser.RequireUserId();

        FollowedUser? row = await writeDb.FollowedUsers
            .FirstOrDefaultAsync(f => f.UserId == actorId && f.FollowedUserId == targetUserId);

        if (row is null) return; // idempotent

        writeDb.FollowedUsers.Remove(row);
        await writeDb.SaveChangesAsync();

        // Decrement UserStats counters for both sides (layer2-services.md §"UserStats Updates").
        await writeDb.UserStats.Where(us => us.UserId == targetUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.FollowerCount, us => us.FollowerCount - 1));
        await writeDb.UserStats.Where(us => us.UserId == actorId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.AuthorsFollowed, us => us.AuthorsFollowed - 1));
    }

    public async Task SetReceiveAlertsAsync(int targetUserId, bool receiveAlerts)
    {
        int actorId = ActiveUser.RequireUserId();

        FollowedUser? row = await writeDb.FollowedUsers
            .FirstOrDefaultAsync(f => f.UserId == actorId && f.FollowedUserId == targetUserId);

        if (row is null)
            throw new FollowingValidationException(["Cannot set alert preference — you are not following this user."]);

        // Kind (g), raise only (owner ruling D6): switching alerts back on re-enrolls the actor in
        // the target's notification fan-out — the same entanglement FollowAsync's guard refuses for
        // a profile the actor can no longer see. Switching them off is a clear and stays unguarded.
        if (receiveAlerts && !row.ReceiveAlerts)
            await RequireProfileVisibleAsync(targetUserId);

        row.ReceiveAlerts = receiveAlerts;
        await writeDb.SaveChangesAsync();
    }

    public async Task VouchAsync(int targetUserId, string? vouchText)
    {
        int actorId = ActiveUser.RequireUserId();

        if (actorId == targetUserId)
            throw new FollowingValidationException(["A user cannot vouch for themselves."]);

        await RequireProfileVisibleAsync(targetUserId);

        // Kept even though the insert below is idempotent on its own: it fixes the semantic order,
        // so a re-vouch is a no-op even when the user is at the limit (not a VouchLimitException).
        bool alreadyVouched = await writeDb.Vouches
            .AnyAsync(v => v.VouchingUserId == actorId && v.VouchedUserId == targetUserId);

        if (alreadyVouched) return; // idempotent — already vouched is a no-op

        // Constraint check on writeDb for consistency (layer2-services.md "Write-Side Reads").
        // Check-then-act by design: the 5-limit is a stated-soft policy (owner ruling D23) — a
        // concurrent overshoot is bounded and the next vouch at the limit is refused.
        int currentCount = await writeDb.Vouches.CountAsync(v => v.VouchingUserId == actorId);
        if (currentCount >= FollowingConstants.MaxVouchesPerUser)
            throw new VouchLimitException();

        // Sanitize the rich-text vouch note before persisting (sanitize-once-on-save).
        string? sanitizedText = vouchText is not null ? sanitizer.Sanitize(vouchText) : null;

        // The idempotency half is declarative (D23): a concurrent double vouch used to pass the
        // AnyAsync above twice and 500 on pk_vouches. Now the loser's insert lands 0 rows.
        Vouch vouch = new()
        {
            VouchingUserId = actorId,
            VouchedUserId = targetUserId,
            VouchText = sanitizedText,
            DateVouched = DateTime.UtcNow
        };
        int inserted = await writeDb.Database.ExecuteSqlAsync($"""
            INSERT INTO vouches (vouching_user_id, vouched_user_id, vouch_text, date_vouched)
            VALUES ({vouch.VouchingUserId}, {vouch.VouchedUserId}, {vouch.VouchText}, {vouch.DateVouched})
            ON CONFLICT (vouching_user_id, vouched_user_id) DO NOTHING
            """);

        // Only the vouch that landed notifies — the target gets one NewVouchOnYou, not two.
        if (inserted == 0) return;

        // Best-effort post-commit notification (WU22). Primary save already committed above;
        // a notification failure must not roll back the vouch. See cross-cutting.md.
        try { await notifications.NotifyNewVouchAsync(targetUserId, actorId); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "NewVouch notification failed for user {UserId} vouching for {TargetUserId}",
                actorId, targetUserId);
        }
    }

    public async Task RemoveVouchAsync(int targetUserId)
    {
        int actorId = ActiveUser.RequireUserId();

        Vouch? row = await writeDb.Vouches
            .FirstOrDefaultAsync(v => v.VouchingUserId == actorId && v.VouchedUserId == targetUserId);

        if (row is null) return; // idempotent

        writeDb.Vouches.Remove(row);
        await writeDb.SaveChangesAsync();
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

}
