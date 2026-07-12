using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server write implementation for Community Spotlight redemption (Feature 55, WU-Spotlight).
///
/// <para><b>Concurrency:</b> block capacity is a count-then-insert check, safe only when
/// serialized — the whole redemption runs in one transaction holding
/// <c>pg_advisory_xact_lock(hashtext('canalave_spotlight_booking'))</c>, wrapped in
/// <c>CreateExecutionStrategy()</c> because <c>EnableRetryOnFailure</c> refuses bare
/// user-initiated transactions (the <c>UserDeletionService</c> precedent). Redemption volume is
/// a handful per month — one global lock is deliberate simplicity, not a bottleneck.</para>
///
/// <para>No <c>IWriteRateLimitService</c> here: the consumed slot <i>is</i> the rate limit
/// (grants are mod-gated/capped). No notification fires at booking — go-live notifications come
/// from <see cref="SpotlightGoLiveWorker"/> when the window opens.</para>
/// </summary>
public class ServerSpotlightWriteService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    IStoryReadService storyReadService,
    IRecommendationReadService recommendationReadService,
    ISiteSettingsReadService siteSettings)
    : ServerSpotlightReadService(readDbFactory, activeUser, storyReadService, recommendationReadService, siteSettings),
      ISpotlightWriteService
{
    public async Task RedeemSlotAsync(RedeemSpotlightSlotDto dto)
    {
        if (ActiveUser.UserId is not int userId)
            throw new InvalidOperationException("This operation requires an authenticated user.");

        // Knob reads sit outside the transaction — they're tuning values, not transactional state.
        int durationDays = await SiteSettings.GetIntAsync(
            SiteSettingKeys.SpotlightBlockDurationDays, SiteSettingKeys.SpotlightBlockDurationDaysDefault);
        int horizonDays = await SiteSettings.GetIntAsync(
            SiteSettingKeys.SpotlightBookingHorizonDays, SiteSettingKeys.SpotlightBookingHorizonDaysDefault);
        int capacity = await SiteSettings.GetIntAsync(
            SiteSettingKeys.SpotlightPositionCount, SiteSettingKeys.SpotlightPositionCountDefault);
        int cooldownDays = await SiteSettings.GetIntAsync(
            SiteSettingKeys.SpotlightCooldownDays, SiteSettingKeys.SpotlightCooldownDaysDefault);

        // Npgsql maps DateTime → timestamptz, which requires Kind=Utc; the dto travels through
        // serialization boundaries that can drop the kind.
        DateTime blockStart = DateTime.SpecifyKind(dto.BlockStartUtc, DateTimeKind.Utc);
        DateTime blockEnd = blockStart.AddDays(durationDays);
        DateTime now = DateTime.UtcNow;

        // ── Grid/window validation (pure math — no DB needed, so before the transaction) ──────
        var errors = new List<string>();
        if (!SpotlightBlocks.IsOnGrid(blockStart, durationDays))
            errors.Add("The chosen start does not lie on the booking grid.");
        if (blockEnd <= now)
            errors.Add("That block has already ended.");
        if (blockStart >= now.AddDays(horizonDays))
            errors.Add($"Blocks can be booked at most {horizonDays} days ahead.");
        if (errors.Count > 0) throw new SpotlightValidationException(errors);

        var strategy = writeDb.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // A transient-failure retry re-runs this whole delegate; without a clean tracker the
            // previous attempt's Add would duplicate. Writes are discrete per-circuit actions, so
            // nothing else is mid-flight on this context.
            writeDb.ChangeTracker.Clear();

            await using var transaction = await writeDb.Database.BeginTransactionAsync();

            // Serialize all bookings — the capacity check below is count-then-insert.
            await writeDb.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(hashtext('canalave_spotlight_booking'))");

            // ── Slot: mine + Available ─────────────────────────────────────────────────────────
            SpotlightSlot? slot = await writeDb.SpotlightSlots
                .FirstOrDefaultAsync(s => s.SlotId == dto.SlotId);
            if (slot is null || slot.GrantedToUserId != userId)
                throw new SpotlightValidationException("That spotlight slot does not exist or is not yours.");
            if (slot.Status != SpotlightSlotStatus.Available)
                throw new SpotlightValidationException("That spotlight slot has already been used or revoked.");

            // ── Story eligibility (ground truth — the write context is unfiltered by design) ──
            var story = await writeDb.Stories
                .Where(s => s.StoryId == dto.StoryId)
                .Select(s => new { s.AuthorId, s.StoryStatusId, s.IsTakenDown })
                .FirstOrDefaultAsync();
            if (story is null)
                throw new SpotlightValidationException("Story not found.");
            if (story.AuthorId == userId)
                throw new SpotlightValidationException(
                    "You can't spotlight your own story — the Community Spotlight features someone else's work.");
            if (story.IsTakenDown
                || story.StoryStatusId is StoryStatusEnum.Draft or StoryStatusEnum.PendingApproval or StoryStatusEnum.Rejected)
                throw new SpotlightValidationException("That story isn't publicly visible, so it can't be spotlighted.");

            // ── Optional recommendation: belongs to the story, Approved, not taken down.
            //    Anyone's — self-recommendation is fine; only self-STORY is banned. ─────────────
            if (dto.RecommendationId is int recId)
            {
                var rec = await writeDb.Recommendations
                    .Where(r => r.RecommendationId == recId)
                    .Select(r => new { r.StoryId, r.StatusId, r.IsTakenDown })
                    .FirstOrDefaultAsync();
                if (rec is null || rec.StoryId != dto.StoryId)
                    throw new SpotlightValidationException("That recommendation doesn't belong to the chosen story.");
                if (rec.IsTakenDown || rec.StatusId != (short)RecommendationStatusEnum.Approved)
                    throw new SpotlightValidationException("That recommendation isn't publicly visible.");
            }

            // ── Per-story cooldown — no placement of this story within cooldownDays on either
            //    side of the new window (also blocks double-booking the same story). ────────────
            DateTime cooldownFloor = blockStart.AddDays(-cooldownDays);
            DateTime cooldownCeiling = blockEnd.AddDays(cooldownDays);
            bool inCooldown = await writeDb.CommunitySpotlights
                .AnyAsync(cs => cs.StoryId == dto.StoryId
                                && cs.StartDate < cooldownCeiling
                                && cs.EndDate > cooldownFloor);
            if (inCooldown)
                throw new SpotlightValidationException(
                    $"That story was (or will be) spotlighted too recently — stories rest for {cooldownDays} days between spotlights.");

            // ── Block capacity (the check the advisory lock exists for) ────────────────────────
            int overlapping = await writeDb.CommunitySpotlights
                .CountAsync(cs => cs.StartDate < blockEnd && cs.EndDate > blockStart);
            if (overlapping >= capacity)
                throw new SpotlightValidationException("That block just filled up — pick another one.");

            // ── Book it ────────────────────────────────────────────────────────────────────────
            slot.Status = SpotlightSlotStatus.Redeemed;
            writeDb.CommunitySpotlights.Add(new CommunitySpotlight
            {
                SlotId = slot.SlotId,
                StoryId = dto.StoryId,
                SponsoringUserId = userId,
                RecommendationId = dto.RecommendationId,
                StartDate = blockStart,
                EndDate = blockEnd,
                DateCreated = now
            });

            await writeDb.SaveChangesAsync();
            await transaction.CommitAsync();
        });
    }
}
