using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation for Story Lineage (Feature 10, WU42). Inherits the read path via
/// primary-constructor chaining, mirroring <see cref="ServerSeriesWriteService"/>. Ownership rule:
/// requesting/deleting requires owning the <b>source</b> story; approving/rejecting requires owning
/// the <b>target</b> story (see <c>audit/Stories.md</c> Feature 10 settled note).
/// </summary>
public class ServerStoryLineageWriteService(
    ApplicationDbContext writeDb,
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser,
    INotificationWriteService notifications,
    ILogger<ServerStoryLineageWriteService> logger)
    : ServerStoryLineageReadService(readDbFactory, activeUser), IStoryLineageWriteService
{
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

        // Kept as a Rejected row (not deleted) — prevents immediate re-request spam and preserves
        // an audit trail. No notification (silent rejection, matching the moderation model).
        link.StatusId = StoryLineageStatus.Rejected;
        await writeDb.SaveChangesAsync();
    }

    public async Task DeleteLineageAsync(int sourceStoryId, int targetStoryId, short typeId)
    {
        int userId = RequireAuthenticatedUser();

        StoryLineage? link = await writeDb.StoryLineages.FindAsync(sourceStoryId, targetStoryId, typeId);
        if (link is null) return; // idempotent — no-op if not present

        Story? source = await writeDb.Stories.FirstOrDefaultAsync(s => s.StoryId == link.SourceStoryId);
        if (source is null || source.AuthorId != userId)
            throw new UnauthorizedAccessException("You must own the source story to remove a lineage link.");

        writeDb.StoryLineages.Remove(link);
        await writeDb.SaveChangesAsync();
    }
}
