using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation for Story Acknowledgments (WU-StatBadgeProducers). Inherits the
/// read path via primary-constructor chaining, mirroring <see cref="ServerStoryLineageWriteService"/>.
/// Ownership rule: requesting/revoking requires owning the <b>story</b>; accepting/declining requires
/// <b>being</b> the credited user (see <see cref="IStoryAcknowledgmentWriteService"/>).
///
/// <para><b>Producer of <c>UserStat.AcknowledgedAsBetaReaderCount</c> and the <c>BetaReader</c> badge
/// (role Beta Reader only; other roles carry no counter).</b> Increments on <see cref="AcceptAsync"/>
/// (a genuine Pending→Accepted transition); decrements on <see cref="RevokeAsync"/> only when the
/// credit was Accepted at the time — the transition-delta rule (<c>layer2-services.md</c>). A
/// <see cref="DeclineAsync"/> credit was never counted, so it never decrements.</para>
/// </summary>
public class ServerStoryAcknowledgmentWriteService(
    ApplicationDbContext writeDb,
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser,
    INotificationWriteService notifications,
    IBadgeWriteService badges,
    ILogger<ServerStoryAcknowledgmentWriteService> logger)
    : ServerStoryAcknowledgmentReadService(readDbFactory, activeUser), IStoryAcknowledgmentWriteService
{
    /// <summary>Role id whose Accepted count feeds <c>AcknowledgedAsBetaReaderCount</c> and the
    /// <c>BetaReader</c> badge — matches the seeded "Beta Reader" row (StoryConfigurations.cs).</summary>
    private const short BetaReaderRoleId = 1;

    public async Task RequestAcknowledgmentAsync(CreateStoryAcknowledgmentDto dto)
    {
        int userId = RequireAuthenticatedUser();

        List<string> errors = dto.CanSave();
        if (errors.Count > 0) throw new StoryAcknowledgmentValidationException(errors);

        // Ground truth (write context, no filters) — ownership/existence checks must see everything.
        Story? story = await writeDb.Stories.FirstOrDefaultAsync(s => s.StoryId == dto.StoryId);
        if (story is null)
            throw new StoryAcknowledgmentValidationException(["The story could not be found."]);
        if (story.AuthorId != userId)
            throw new UnauthorizedAccessException("You must own the story to credit someone on it.");

        // Anti-self-farm: crediting yourself is meaningless (and would let a single account mint
        // its own beta-reader count) — rejected outright, unlike lineage's self-owned-target
        // auto-approve. There is deliberately no "self-owned" case for a credit.
        if (dto.AcknowledgedUserId == userId)
            throw new StoryAcknowledgmentValidationException(["You cannot credit yourself."]);

        // A user is addressable regardless of ProfileVisibility (settled, WU-StatBadgeProducers —
        // see design/access-gating-first-principles.md "UserPicker search ignores ProfileVisibility")
        // — a plain existence check is the correct gate, not a visibility one.
        bool recipientExists = await writeDb.Users.AnyAsync(u => u.Id == dto.AcknowledgedUserId);
        if (!recipientExists)
            throw new StoryAcknowledgmentValidationException(["The selected user could not be found."]);

        bool roleExists = await writeDb.AcknowledgmentRoles.AnyAsync(r => r.AcknowledgmentRoleId == dto.AcknowledgmentRoleId);
        if (!roleExists)
            throw new StoryAcknowledgmentValidationException(["The selected role is not valid."]);

        StoryAcknowledgment? existing = await writeDb.StoryAcknowledgments
            .FindAsync(dto.StoryId, dto.AcknowledgedUserId, dto.AcknowledgmentRoleId);
        if (existing is not null)
        {
            if (existing.StatusId != StoryAcknowledgmentStatus.Declined)
                throw new StoryAcknowledgmentValidationException(
                    ["A credit of this role for this user already exists."]);

            // Re-request after a prior decline reuses the row (composite PK) rather than
            // duplicate-inserting — see IStoryAcknowledgmentWriteService.RequestAcknowledgmentAsync doc.
            existing.StatusId = StoryAcknowledgmentStatus.Pending;
            existing.DateAcknowledged = DateTime.UtcNow;
            existing.DateResponded = null;
        }
        else
        {
            writeDb.StoryAcknowledgments.Add(new StoryAcknowledgment
            {
                StoryId = dto.StoryId,
                AcknowledgedUserId = dto.AcknowledgedUserId,
                AcknowledgmentRoleId = dto.AcknowledgmentRoleId,
                StatusId = StoryAcknowledgmentStatus.Pending,
                DateAcknowledged = DateTime.UtcNow
            });
        }

        await writeDb.SaveChangesAsync();

        // Best-effort post-commit — never let a notification failure roll back the primary action.
        try
        {
            await notifications.NotifyStoryAcknowledgedAsync(dto.AcknowledgedUserId, userId, dto.StoryId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to send NewStoryAcknowledgement notification for story {StoryId} to user {AcknowledgedUserId}",
                dto.StoryId, dto.AcknowledgedUserId);
        }
    }

    public async Task AcceptAsync(int storyId, short roleId)
    {
        int userId = RequireAuthenticatedUser();

        StoryAcknowledgment? credit = await writeDb.StoryAcknowledgments.FindAsync(storyId, userId, roleId);
        if (credit is null) throw new KeyNotFoundException("Acknowledgment not found.");
        if (credit.StatusId != StoryAcknowledgmentStatus.Pending)
            throw new StoryAcknowledgmentValidationException(["This credit is no longer pending."]);

        credit.StatusId = StoryAcknowledgmentStatus.Accepted;
        credit.DateResponded = DateTime.UtcNow;
        await writeDb.SaveChangesAsync();

        if (roleId != BetaReaderRoleId) return;

        await writeDb.UserStats
            .Where(us => us.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(
                us => us.AcknowledgedAsBetaReaderCount, us => us.AcknowledgedAsBetaReaderCount + 1));

        int total = await writeDb.UserStats
            .Where(us => us.UserId == userId)
            .Select(us => us.AcknowledgedAsBetaReaderCount)
            .FirstOrDefaultAsync();

        try
        {
            if (total >= 1) await badges.AwardAsync(userId, SiteBadges.BetaReader, total);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Badge award failed for user {UserId} after AcceptAsync — swallowed.", userId);
        }
    }

    public async Task DeclineAsync(int storyId, short roleId)
    {
        int userId = RequireAuthenticatedUser();

        StoryAcknowledgment? credit = await writeDb.StoryAcknowledgments.FindAsync(storyId, userId, roleId);
        if (credit is null) throw new KeyNotFoundException("Acknowledgment not found.");
        if (credit.StatusId != StoryAcknowledgmentStatus.Pending)
            throw new StoryAcknowledgmentValidationException(["This credit is no longer pending."]);

        // Kept as a Declined row (not deleted) — allows a later re-credit to reuse the row. No
        // counter change: a Pending credit was never counted, so there is nothing to undo.
        credit.StatusId = StoryAcknowledgmentStatus.Declined;
        credit.DateResponded = DateTime.UtcNow;
        await writeDb.SaveChangesAsync();
    }

    public async Task RevokeAsync(int storyId, int acknowledgedUserId, short roleId)
    {
        int userId = RequireAuthenticatedUser();

        StoryAcknowledgment? credit = await writeDb.StoryAcknowledgments.FindAsync(storyId, acknowledgedUserId, roleId);
        if (credit is null) return; // idempotent — no-op if not present

        Story? story = await writeDb.Stories.FirstOrDefaultAsync(s => s.StoryId == storyId);
        if (story is null || story.AuthorId != userId)
            throw new UnauthorizedAccessException("You must own the story to revoke a credit on it.");

        bool wasAccepted = credit.StatusId == StoryAcknowledgmentStatus.Accepted;

        // Revoke removes the credit assertion entirely — the author retracting a claim, not a
        // decision the recipient made, so there is no reuse-the-row history to preserve (unlike
        // Decline, which stays a row for a future re-credit).
        writeDb.StoryAcknowledgments.Remove(credit);
        await writeDb.SaveChangesAsync();

        if (!wasAccepted || roleId != BetaReaderRoleId) return;

        // Transition-delta: the credit WAS counted (Accepted), so removing it must undo that —
        // mirrors the UserStoryInteraction-derived counters' rule (layer2-services.md).
        await writeDb.UserStats
            .Where(us => us.UserId == acknowledgedUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(
                us => us.AcknowledgedAsBetaReaderCount, us => us.AcknowledgedAsBetaReaderCount - 1));
    }
}
