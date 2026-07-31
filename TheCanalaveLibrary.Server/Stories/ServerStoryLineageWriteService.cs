using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation for Story Lineage (Feature 10, WU42). Inherits the read path via
/// primary-constructor chaining, mirroring <see cref="ServerSeriesWriteService"/>. Ownership rule:
/// requesting/deleting requires owning the <b>source</b> story; approving/rejecting requires owning
/// the <b>target</b> story (see <c>audit/Stories.md</c> Feature 10 settled note).
///
/// <para><b>Producer of <c>UserStat.AcknowledgedAsInspirationCount</c> (WU-StatBadgeProducers)</b> —
/// type id 1 ("Inspired By") only, counted toward the TARGET story's author (the person who
/// inspired), guarded against same-author links. Increments on <see cref="ApproveLineageAsync"/>
/// (a genuine Pending→Approved transition — a self-owned link auto-approves via
/// <see cref="RequestLineageAsync"/> instead, but is always same-author and so never passes the
/// guard, meaning no increment call is needed there). Decrements on <see cref="RejectLineageAsync"/>
/// / <see cref="DeleteLineageAsync"/> only when the row was Approved beforehand — the transition-
/// delta rule (<c>layer2-services.md</c>) — since both methods can act on an already-Approved row
/// without a status precondition.</para>
/// </summary>
public class ServerStoryLineageWriteService(
    ApplicationDbContext writeDb,
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser,
    INotificationWriteService notifications,
    ILogger<ServerStoryLineageWriteService> logger)
    : ServerStoryLineageReadService(readDbFactory, activeUser), IStoryLineageWriteService
{
    /// <summary>Type id whose Approved count feeds <c>AcknowledgedAsInspirationCount</c> — matches
    /// the seeded "Inspired By" row (StoryConfigurations.cs).</summary>
    private const short InspiredByTypeId = 1;

    /// <summary>Adjusts the TARGET author's inspiration counter by <paramref name="delta"/>,
    /// guarded to Inspired-By links between two different authors — mirrors
    /// <c>UserStatRecalculator.AcknowledgedAsInspirationCountAgg</c>'s <c>IS DISTINCT FROM</c>
    /// exclusion exactly (a null source author is never "the same" as a real target author).</summary>
    private async Task AdjustInspirationCounterAsync(short typeId, int? targetAuthorId, int? sourceAuthorId, int delta)
    {
        if (typeId != InspiredByTypeId) return;
        if (targetAuthorId is not int realTargetAuthorId) return;
        if (realTargetAuthorId == sourceAuthorId) return; // same-author link — not a real inspiration credit

        await writeDb.UserStats
            .Where(us => us.UserId == realTargetAuthorId)
            .ExecuteUpdateAsync(s => s.SetProperty(
                us => us.AcknowledgedAsInspirationCount, us => us.AcknowledgedAsInspirationCount + delta));
    }

    public async Task RequestLineageAsync(CreateStoryLineageDto dto)
    {
        int userId = RequireAuthenticatedUser();

        List<string> errors = dto.CanSave();
        if (errors.Count > 0) throw new StoryLineageValidationException(errors);

        // Ground truth (write context, no filters) — ownership/existence checks must see everything.
        Story? source = await writeDb.Stories.FirstOrDefaultAsync(s => s.StoryId == dto.SourceStoryId);
        if (source is null)
            throw new StoryLineageValidationException(["The source story could not be found."]);
        if (source.AuthorId != userId)
            throw new UnauthorizedAccessException("You must own the source story to request a lineage link.");

        Story? target = await writeDb.Stories.FirstOrDefaultAsync(s => s.StoryId == dto.TargetStoryId);
        if (target is null)
            throw new StoryLineageValidationException(["The target story could not be found."]);

        // Kind (g) on the TARGET (the source is already ownership-gated above). Without it a request
        // could point at someone's unpublished, rejected or taken-down story and notify its author —
        // and the found-vs-not-found distinction was an oracle across the whole Stories keyspace.
        // Same message as a genuinely missing target (non-disclosure).
        if (target.AuthorId != userId)
        {
            await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
            if (!await StoryVisibilityGuard.IsStoryVisibleAsync(readDb, ActiveUser, dto.TargetStoryId))
                throw new StoryLineageValidationException(["The target story could not be found."]);
        }

        bool typeExists = await writeDb.StoryLineageTypes.AnyAsync(t => t.RelationshipTypeId == dto.TypeId);
        if (!typeExists)
            throw new StoryLineageValidationException(["The selected lineage type is not valid."]);

        bool selfOwned = target.AuthorId == userId;

        StoryLineage? existing = await writeDb.StoryLineages.FindAsync(dto.SourceStoryId, dto.TargetStoryId, dto.TypeId);
        if (existing is not null)
        {
            if (existing.StatusId != StoryLineageStatus.Rejected)
                throw new StoryLineageValidationException(
                    ["A lineage link of this type to this story already exists."]);

            // Re-request after a prior rejection reuses the row (composite PK) rather than
            // duplicate-inserting — see IStoryLineageWriteService.RequestLineageAsync doc.
            existing.StatusId = selfOwned ? StoryLineageStatus.Approved : StoryLineageStatus.Pending;
            existing.DateCreated = DateTime.UtcNow;
        }
        else
        {
            writeDb.StoryLineages.Add(new StoryLineage
            {
                SourceStoryId = dto.SourceStoryId,
                TargetStoryId = dto.TargetStoryId,
                RelationshipTypeId = dto.TypeId,
                StatusId = selfOwned ? StoryLineageStatus.Approved : StoryLineageStatus.Pending,
                DateCreated = DateTime.UtcNow
            });
        }

        await writeDb.SaveChangesAsync();

        // Best-effort post-commit — never let a notification failure roll back the primary action.
        if (!selfOwned && target.AuthorId is int targetAuthorId)
        {
            try
            {
                await notifications.NotifyStoryLineageRequestedAsync(targetAuthorId, userId, dto.SourceStoryId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to send StoryLineageRequested notification for source story {SourceStoryId} to user {TargetAuthorId}",
                    dto.SourceStoryId, targetAuthorId);
            }
        }
    }

    public async Task ApproveLineageAsync(int sourceStoryId, int targetStoryId, short typeId)
    {
        int userId = RequireAuthenticatedUser();

        StoryLineage? link = await writeDb.StoryLineages.FindAsync(sourceStoryId, targetStoryId, typeId);
        if (link is null) throw new KeyNotFoundException("Lineage link not found.");

        Story? target = await writeDb.Stories.FirstOrDefaultAsync(s => s.StoryId == targetStoryId);
        if (target is null || target.AuthorId != userId)
            throw new UnauthorizedAccessException("You must own the target story to approve a lineage request.");

        link.StatusId = StoryLineageStatus.Approved;
        await writeDb.SaveChangesAsync();

        Story? source = await writeDb.Stories.FirstOrDefaultAsync(s => s.StoryId == sourceStoryId);

        await AdjustInspirationCounterAsync(typeId, target.AuthorId, source?.AuthorId, delta: 1);

        if (source?.AuthorId is int sourceAuthorId)
        {
            try
            {
                await notifications.NotifyStoryLineageApprovedAsync(sourceAuthorId, userId, targetStoryId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to send StoryLineageApproved notification for target story {TargetStoryId} to user {SourceAuthorId}",
                    targetStoryId, sourceAuthorId);
            }
        }
    }

    public async Task RejectLineageAsync(int sourceStoryId, int targetStoryId, short typeId)
    {
        int userId = RequireAuthenticatedUser();

        StoryLineage? link = await writeDb.StoryLineages.FindAsync(sourceStoryId, targetStoryId, typeId);
        if (link is null) throw new KeyNotFoundException("Lineage link not found.");

        Story? target = await writeDb.Stories.FirstOrDefaultAsync(s => s.StoryId == targetStoryId);
        if (target is null || target.AuthorId != userId)
            throw new UnauthorizedAccessException("You must own the target story to reject a lineage request.");

        // Transition-delta: this method carries no status precondition, so it can act on an
        // already-Approved row — capture that BEFORE mutating, so the counter only unwinds a
        // genuine Approved→Rejected transition, never a Pending→Rejected one (never counted).
        bool wasApproved = link.StatusId == StoryLineageStatus.Approved;

        // Kept as a Rejected row (not deleted) — prevents immediate re-request spam and preserves
        // an audit trail. No notification (silent rejection, matching the moderation model).
        link.StatusId = StoryLineageStatus.Rejected;
        await writeDb.SaveChangesAsync();

        if (wasApproved)
        {
            Story? source = await writeDb.Stories.FirstOrDefaultAsync(s => s.StoryId == sourceStoryId);
            await AdjustInspirationCounterAsync(typeId, target.AuthorId, source?.AuthorId, delta: -1);
        }
    }

    public async Task DeleteLineageAsync(int sourceStoryId, int targetStoryId, short typeId)
    {
        int userId = RequireAuthenticatedUser();

        StoryLineage? link = await writeDb.StoryLineages.FindAsync(sourceStoryId, targetStoryId, typeId);
        if (link is null) return; // idempotent — no-op if not present

        Story? source = await writeDb.Stories.FirstOrDefaultAsync(s => s.StoryId == link.SourceStoryId);
        if (source is null || source.AuthorId != userId)
            throw new UnauthorizedAccessException("You must own the source story to remove a lineage link.");

        // Transition-delta — same reasoning as RejectLineageAsync: capture status before removing.
        // (typeId is already the method's own parameter — the FindAsync lookup key above.)
        bool wasApproved = link.StatusId == StoryLineageStatus.Approved;

        writeDb.StoryLineages.Remove(link);
        await writeDb.SaveChangesAsync();

        if (wasApproved)
        {
            Story? target = await writeDb.Stories.FirstOrDefaultAsync(s => s.StoryId == targetStoryId);
            await AdjustInspirationCounterAsync(typeId, target?.AuthorId, source.AuthorId, delta: -1);
        }
    }
}
