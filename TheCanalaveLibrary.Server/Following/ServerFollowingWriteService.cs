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
    ReadOnlyApplicationDbContext readDb,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    IHtmlSanitizationService sanitizer,
    INotificationWriteService notifications,
    ILogger<ServerFollowingWriteService> logger)
    : ServerFollowingReadService(readDb, activeUser), IFollowingWriteService
{
    public async Task FollowAsync(int targetUserId)
    {
        int actorId = RequireAuthenticatedUser();

        if (actorId == targetUserId)
            throw new InvalidOperationException("A user cannot follow themselves.");

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

        // Increment UserStats counters for both sides (cross-cutting.md §"UserStats Updates").
        await writeDb.UserStats.Where(us => us.UserId == targetUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.FollowerCount, us => us.FollowerCount + 1));
        await writeDb.UserStats.Where(us => us.UserId == actorId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.AuthorsFollowed, us => us.AuthorsFollowed + 1));

        // Best-effort post-commit notification (WU22). Primary save already committed above;
        // a notification failure must not roll back the follow. See cross-cutting.md.
        try { await notifications.NotifyNewFollowerAsync(targetUserId, actorId); }
        catch (Exception ex) { logger.LogError(ex, "Notification failed — FollowAsync (non-fatal)"); }
    }

    public async Task UnfollowAsync(int targetUserId)
    {
        int actorId = RequireAuthenticatedUser();

        FollowedUser? row = await writeDb.FollowedUsers
            .FirstOrDefaultAsync(f => f.UserId == actorId && f.FollowedUserId == targetUserId);

        if (row is null) return; // idempotent

        writeDb.FollowedUsers.Remove(row);
        await writeDb.SaveChangesAsync();

        // Decrement UserStats counters for both sides (cross-cutting.md §"UserStats Updates").
        await writeDb.UserStats.Where(us => us.UserId == targetUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.FollowerCount, us => us.FollowerCount - 1));
        await writeDb.UserStats.Where(us => us.UserId == actorId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.AuthorsFollowed, us => us.AuthorsFollowed - 1));
    }

    public async Task SetReceiveAlertsAsync(int targetUserId, bool receiveAlerts)
    {
        int actorId = RequireAuthenticatedUser();

        FollowedUser? row = await writeDb.FollowedUsers
            .FirstOrDefaultAsync(f => f.UserId == actorId && f.FollowedUserId == targetUserId);

        if (row is null)
            throw new InvalidOperationException("Cannot set alert preference — you are not following this user.");

        row.ReceiveAlerts = receiveAlerts;
        await writeDb.SaveChangesAsync();
    }

    public async Task VouchAsync(int targetUserId, string? vouchText)
    {
        int actorId = RequireAuthenticatedUser();

        if (actorId == targetUserId)
            throw new InvalidOperationException("A user cannot vouch for themselves.");

        bool alreadyVouched = await writeDb.Vouches
            .AnyAsync(v => v.VouchingUserId == actorId && v.VouchedUserId == targetUserId);

        if (alreadyVouched) return; // idempotent — already vouched is a no-op

        // Constraint check on writeDb for consistency (layer2-services.md "Write-Side Reads").
        int currentCount = await writeDb.Vouches.CountAsync(v => v.VouchingUserId == actorId);
        if (currentCount >= FollowingConstants.MaxVouchesPerUser)
            throw new VouchLimitException();

        // Sanitize the rich-text vouch note before persisting (sanitize-once-on-save).
        string? sanitizedText = vouchText is not null ? sanitizer.Sanitize(vouchText) : null;

        writeDb.Vouches.Add(new Vouch
        {
            VouchingUserId = actorId,
            VouchedUserId = targetUserId,
            VouchText = sanitizedText,
            DateVouched = DateTime.UtcNow
        });

        await writeDb.SaveChangesAsync();

        // Best-effort post-commit notification (WU22). Primary save already committed above;
        // a notification failure must not roll back the vouch. See cross-cutting.md.
        try { await notifications.NotifyNewVouchAsync(targetUserId, actorId); }
        catch (Exception ex) { logger.LogError(ex, "Notification failed — VouchAsync (non-fatal)"); }
    }

    public async Task RemoveVouchAsync(int targetUserId)
    {
        int actorId = RequireAuthenticatedUser();

        Vouch? row = await writeDb.Vouches
            .FirstOrDefaultAsync(v => v.VouchingUserId == actorId && v.VouchedUserId == targetUserId);

        if (row is null) return; // idempotent

        writeDb.Vouches.Remove(row);
        await writeDb.SaveChangesAsync();
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    private int RequireAuthenticatedUser()
    {
        // Uses the base class's CurrentUserId property so the derived class doesn't capture the
        // activeUser primary constructor parameter (avoids CS9107 double-capture warning).
        if (CurrentUserId is not int id)
            throw new InvalidOperationException("This operation requires an authenticated user.");
        return id;
    }
}
