using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation for Recommendations. Inherits the read path via primary-constructor
/// chaining. All user HTML is sanitized on save (IHtmlSanitizationService); min-length is validated on
/// the stripped plain text (RecommendationText.CountPlainTextLength — layer2-services.md WU29 conventions).
///
/// <para><b>Lifecycle (WU-RecLifecycle):</b> recommendations publish immediately (Approved on submit —
/// the pre-publication gate was rejected by design, not deferred). The story author holds two distinct
/// actions: <see cref="RequestRevisionAsync"/> (note + hide-until-edited, not sticky — the recommender's
/// edit auto-returns it to Approved) and <see cref="RemoveAsync"/> (silent, sticky Rejected; only
/// <see cref="UnblockAsync"/> reverses it). Self-recommendation is blocked. Flag invariant:
/// IsHiddenGem/IsHighlightedByAuthor only ever true on Approved recs. See layer2-services.md
/// §"Publish-immediately + the Recommendation Lifecycle".</para>
/// <para><b>One-per-user-per-story:</b> enforced by the DB unique index. Duplicate submissions are caught
/// as <see cref="InvalidOperationException"/> with a friendly message. A Rejected row keeps the slot
/// occupied — that persistence is what makes a removal sticky.</para>
/// <para><b>Hidden Gem limit:</b> reject-at-5 (count against writeDb before setting). No auto-evict.
/// On set, best-effort post-commit notification fires to the story author via INotificationWriteService.</para>
/// <para><b>Like toggle:</b> no notification — anti-addictive design (§6.11).</para>
/// </summary>
public class ServerRecommendationWriteService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    IHtmlSanitizationService sanitizer,
    INotificationWriteService notifications,
    IBadgeWriteService badges,
    IWriteRateLimitService rateLimit,
    ILogger<ServerRecommendationWriteService> logger)
    : ServerRecommendationReadService(readDbFactory, activeUser), IRecommendationWriteService
{
    private const short ApprovedStatusId = (short)RecommendationStatusEnum.Approved;
    private const short NeedsRevisionStatusId = (short)RecommendationStatusEnum.NeedsRevision;
    private const short RejectedStatusId = (short)RecommendationStatusEnum.Rejected;

    /// <summary>Length cap for the author's Request-Revision note (mirrors the entity's MaxLength).</summary>
    public const int MaxRevisionNoteLength = 500;

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private int RequireAuthenticatedUser(string action) =>
        ActiveUser.UserId ?? throw new InvalidOperationException($"{action} requires an authenticated user.");

    /// <summary>
    /// Kind (g): refuses a write whose parent story the caller cannot see. <c>writeDb</c> carries no
    /// visibility filters, so every "story loads" check in this file proves existence only — the guard
    /// needs a read context. Throws the same message a missing story produces (non-disclosure rule).
    /// <para>
    /// <paramref name="confidentialityOnly"/> keeps the viewer's rating ceiling out of the decision,
    /// for the one path where that permissiveness is a recorded WU29 decision rather than an
    /// oversight (see <see cref="SubmitAsync"/>).
    /// </para>
    /// </summary>
    private async Task RequireStoryVisibleAsync(int storyId, bool confidentialityOnly = false)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        bool visible = confidentialityOnly
            ? await StoryVisibilityGuard.IsStoryPublishedAsync(readDb, ActiveUser, storyId)
            : await StoryVisibilityGuard.IsStoryVisibleAsync(readDb, ActiveUser, storyId);

        if (!visible) throw new KeyNotFoundException($"Story {storyId} not found.");
    }

    /// <summary>
    /// Kind (g) keyed by recommendation id: resolves the rec's parent story and applies
    /// <see cref="RequireStoryVisibleAsync"/>. Used by the paths a non-owner can reach.
    /// </summary>
    private async Task RequireRecommendationVisibleAsync(int recommendationId)
    {
        int? storyId = await writeDb.Recommendations
            .Where(r => r.RecommendationId == recommendationId)
            .Select(r => (int?)r.StoryId)
            .FirstOrDefaultAsync();

        if (storyId is not int parentStoryId)
            throw new KeyNotFoundException($"Recommendation {recommendationId} not found.");

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        if (!await StoryVisibilityGuard.IsStoryVisibleAsync(readDb, ActiveUser, parentStoryId))
            throw new KeyNotFoundException($"Recommendation {recommendationId} not found.");
    }

    /// <summary>
    /// Loads a recommendation and authorizes the caller as the author of its story (the
    /// SetHighlightedByAuthorAsync ownership pattern). Co-authors deliberately excluded until the
    /// dormant CoAuthor feature is built.
    /// </summary>
    private async Task<(Recommendation rec, int userId)> RequireStoryAuthorAsync(int recommendationId, string action)
    {
        int userId = RequireAuthenticatedUser(action);

        Recommendation? rec = await writeDb.Recommendations
            .FirstOrDefaultAsync(r => r.RecommendationId == recommendationId);
        if (rec is null)
            throw new KeyNotFoundException($"Recommendation {recommendationId} not found.");

        bool isStoryAuthor = await writeDb.Stories
            .AnyAsync(s => s.StoryId == rec.StoryId && s.AuthorId == userId);
        if (!isStoryAuthor)
            throw new UnauthorizedAccessException("Only the story author can manage recommendations on their story.");

        return (rec, userId);
    }

    /// <summary>Fires a best-effort post-commit notification; failures are logged and swallowed.</summary>
    private async Task NotifyBestEffortAsync(Func<Task> notify, string what, int recommendationId)
    {
        try
        {
            await notify();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{What} notification failed for recommendation {Id} — swallowed.", what, recommendationId);
        }
    }

    // ── Submit ───────────────────────────────────────────────────────────────────

    public async Task<int> SubmitAsync(RecommendationSubmitDto dto)
    {
        int userId = RequireAuthenticatedUser("Submitting a recommendation");
        rateLimit.EnsureAllowed(WriteActionKind.ContentCreate, userId);

        // Write context is unfiltered — story loads regardless of ContentRating, so a reader with
        // mature content off can still recommend an M-rated story. Project to anonymous type so null
        // AuthorId (authorless story) is not confused with "row not found" — FirstOrDefault<int?>
        // cannot distinguish the two cases.
        var storyRow = await writeDb.Stories
            .Where(s => s.StoryId == dto.StoryId)
            .Select(s => new { s.AuthorId })
            .FirstOrDefaultAsync();
        if (storyRow is null)
            throw new KeyNotFoundException($"Story {dto.StoryId} not found.");

        // Kind (g), confidentiality axis only: "mature off can still recommend an M-rated story" is
        // the deliberate WU29 behavior documented immediately above and is preserved. What was never
        // intended is a rec on a Draft/PendingApproval/Rejected or taken-down story — it takes the
        // one-per-user slot permanently, bumps the author's RecommendationsReceived, and notifies
        // them that someone guessed an unpublished id.
        await RequireStoryVisibleAsync(dto.StoryId, confidentialityOnly: true);

        int? storyAuthorId = storyRow.AuthorId;

        // Self-recommendation blocked (WU-RecLifecycle): a recommendation is a peer endorsement
        // by definition — the story's author cannot recommend their own story.
        if (storyAuthorId == userId)
            throw new RecommendationValidationException(["You cannot recommend your own story."]);

        string sanitizedText = sanitizer.Sanitize(dto.Text);

        List<string> errors = dto.CanSave(sanitizedText);
        if (errors.Count > 0) throw new RecommendationValidationException(errors);

        Recommendation rec = new()
        {
            StoryId     = dto.StoryId,
            RecommenderId = userId,
            StatusId    = ApprovedStatusId, // publish-immediately (WU-RecLifecycle — permanent design, no gate)
            DatePosted  = DateTime.UtcNow
        };
        rec.RecommendationDetail = new RecommendationDetail { Text = sanitizedText };

        writeDb.Recommendations.Add(rec);

        try
        {
            await writeDb.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("ix_recommendations_recommender_id_story_id") == true)
        {
            throw new InvalidOperationException("You have already submitted a recommendation for this story.");
        }

        // Increment UserStats counters (cross-cutting.md §"UserStats Updates").
        await writeDb.UserStats.Where(us => us.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.RecommendationsWritten, us => us.RecommendationsWritten + 1));
        // AuthorId is nullable (stories with no explicit author skip the author-stat update).
        if (storyAuthorId.HasValue)
            await writeDb.UserStats.Where(us => us.UserId == storyAuthorId.Value)
                .ExecuteUpdateAsync(s => s.SetProperty(us => us.RecommendationsReceived, us => us.RecommendationsReceived + 1));

        // Best-effort post-commit: tell the story author a new recommendation is live on their story.
        if (storyAuthorId is int authorId)
            await NotifyBestEffortAsync(
                () => notifications.NotifyNewRecommendationOnYourStoryAsync(authorId, userId, dto.StoryId),
                "NewRecommendationOnYourStory", rec.RecommendationId);

        return rec.RecommendationId;
    }

    // ── Edit ─────────────────────────────────────────────────────────────────────

    public async Task EditAsync(UpdateRecommendationDto dto)
    {
        int userId = RequireAuthenticatedUser("Editing a recommendation");

        Recommendation? rec = await writeDb.Recommendations
            .Include(r => r.RecommendationDetail)
            .FirstOrDefaultAsync(r => r.RecommendationId == dto.RecommendationId);
        if (rec is null)
            throw new KeyNotFoundException($"Recommendation {dto.RecommendationId} not found.");
        if (rec.RecommenderId != userId)
            throw new UnauthorizedAccessException("You can only edit your own recommendations.");

        // Sticky removal (WU-RecLifecycle): a Rejected rec is out of the recommender's hands —
        // only the story author's Unblock can revive it.
        if (rec.StatusId == RejectedStatusId)
            throw new UnauthorizedAccessException(
                "This recommendation was removed by the story author and can no longer be edited.");

        string sanitizedText = sanitizer.Sanitize(dto.Text);

        List<string> errors = dto.CanSave(sanitizedText);
        if (errors.Count > 0) throw new RecommendationValidationException(errors);

        bool wasNeedsRevision = rec.StatusId == NeedsRevisionStatusId;

        rec.RecommendationDetail.Text = sanitizedText;
        if (wasNeedsRevision)
        {
            // The edit IS the revision — auto-return to live, no author re-blessing step.
            rec.StatusId = ApprovedStatusId;
            rec.RevisionRequestNote = null;
        }
        await writeDb.SaveChangesAsync();

        if (wasNeedsRevision)
        {
            // Best-effort post-commit: tell the story AUTHOR the flagged rec is live again (their
            // recourse is Remove). The recommender is not self-notified — their own edit caused it.
            int? storyAuthorId = await writeDb.Stories
                .Where(s => s.StoryId == rec.StoryId)
                .Select(s => s.AuthorId)
                .FirstOrDefaultAsync();
            if (storyAuthorId is int authorId)
                await NotifyBestEffortAsync(
                    () => notifications.NotifyRecommendationRevisedAsync(authorId, userId, rec.StoryId),
                    "RecommendationRevised", rec.RecommendationId);
        }
    }

    // ── Delete ───────────────────────────────────────────────────────────────────

    public async Task DeleteAsync(int recommendationId)
    {
        int userId = RequireAuthenticatedUser("Deleting a recommendation");

        Recommendation? rec = await writeDb.Recommendations
            .FirstOrDefaultAsync(r => r.RecommendationId == recommendationId);
        if (rec is null)
            throw new KeyNotFoundException($"Recommendation {recommendationId} not found.");
        if (rec.RecommenderId != userId)
            throw new UnauthorizedAccessException("You can only delete your own recommendations.");

        // Sticky removal (WU-RecLifecycle): deleting a Rejected rec would free the
        // one-per-user-per-story slot and let the recommender resubmit — the persisted Rejected
        // row IS the block record.
        if (rec.StatusId == RejectedStatusId)
            throw new UnauthorizedAccessException(
                "This recommendation was removed by the story author and can no longer be deleted.");

        writeDb.Recommendations.Remove(rec);
        await writeDb.SaveChangesAsync();
    }

    // ── Like toggle ──────────────────────────────────────────────────────────────

    public async Task<RecommendationLikeResultDto> ToggleLikeAsync(int recommendationId)
    {
        int userId = RequireAuthenticatedUser("Liking a recommendation");

        Recommendation? rec = await writeDb.Recommendations
            .Include(r => r.Likes.Where(l => l.UserId == userId))
            .FirstOrDefaultAsync(r => r.RecommendationId == recommendationId);
        if (rec is null)
            throw new KeyNotFoundException($"Recommendation {recommendationId} not found.");

        // Kind (g): liking requires seeing. writeDb also bypasses the Recommendation IsTakenDown
        // filter, so without this a moderator-removed rec on a hidden story stayed likeable.
        await RequireRecommendationVisibleAsync(recommendationId);

        RecommendationLike? existing = rec.Likes.FirstOrDefault();
        bool nowLiked;
        int delta;

        if (existing is not null)
        {
            writeDb.RecommendationLikes.Remove(existing);
            nowLiked = false;
            delta = -1;
        }
        else
        {
            writeDb.RecommendationLikes.Add(new RecommendationLike
            {
                RecommendationId = recommendationId,
                UserId = userId
            });
            nowLiked = true;
            delta = 1;
        }

        await writeDb.SaveChangesAsync();
        // No notification — anti-addictive design (§6.11).

        // Atomic counter update — see cross-cutting.md §"Counter mutation rule" for why
        // ExecuteUpdateAsync is used here instead of tracked read-modify-write.
        await writeDb.Recommendations
            .Where(r => r.RecommendationId == recommendationId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.LikeCount, r => r.LikeCount + delta));

        return new RecommendationLikeResultDto(Math.Max(0, rec.LikeCount + delta), nowLiked);
    }

    // ── Hidden Gem ───────────────────────────────────────────────────────────────

    public async Task SetHiddenGemAsync(int recommendationId, bool isHiddenGem)
    {
        int userId = RequireAuthenticatedUser("Setting a Hidden Gem");

        Recommendation? rec = await writeDb.Recommendations
            .FirstOrDefaultAsync(r => r.RecommendationId == recommendationId);
        if (rec is null)
            throw new KeyNotFoundException($"Recommendation {recommendationId} not found.");
        if (rec.RecommenderId != userId)
            throw new UnauthorizedAccessException("You can only manage your own Hidden Gem designations.");

        // Flag invariant (WU-RecLifecycle): curation flags only ever true on live (Approved) recs.
        if (isHiddenGem && rec.StatusId != ApprovedStatusId)
            throw new RecommendationValidationException(
                ["Only a live recommendation can be designated a Hidden Gem."]);

        if (rec.IsHiddenGem == isHiddenGem) return; // already in desired state

        if (isHiddenGem)
        {
            // Reject-at-limit: count active Hidden Gems for this user (writeDb for consistency).
            int currentCount = await writeDb.Recommendations
                .CountAsync(r => r.RecommenderId == userId && r.IsHiddenGem);
            if (currentCount >= RecommendationConstants.MaxHiddenGemsPerUser)
                throw new RecommendationValidationException(
                    [$"You already have {RecommendationConstants.MaxHiddenGemsPerUser} Hidden Gem designations. " +
                     "Remove one before adding another."]);
        }

        rec.IsHiddenGem = isHiddenGem;
        await writeDb.SaveChangesAsync();

        if (isHiddenGem)
        {
            // Best-effort post-commit notification to story author.
            try
            {
                // Anonymous-type projection so null AuthorId (authorless story) is not confused
                // with "row not found" — mirrors SubmitAsync's projection above.
                var storyRow = await writeDb.Stories
                    .Where(s => s.StoryId == rec.StoryId)
                    .Select(s => new { s.AuthorId })
                    .FirstOrDefaultAsync();
                if (storyRow is { AuthorId: int storyAuthorId })
                    await notifications.NotifyStoryHiddenGemAsync(storyAuthorId, userId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Hidden Gem notification failed for recommendation {Id} — swallowed.", recommendationId);
            }
        }
    }

    // ── Author spotlight ─────────────────────────────────────────────────────────

    public async Task SetHighlightedByAuthorAsync(int recommendationId, bool isHighlighted)
    {
        int userId = RequireAuthenticatedUser("Spotlighting a recommendation");

        Recommendation? rec = await writeDb.Recommendations
            .FirstOrDefaultAsync(r => r.RecommendationId == recommendationId);
        if (rec is null)
            throw new KeyNotFoundException($"Recommendation {recommendationId} not found.");

        // Verify caller is the story author.
        bool isStoryAuthor = await writeDb.Stories
            .AnyAsync(s => s.StoryId == rec.StoryId && s.AuthorId == userId);
        if (!isStoryAuthor)
            throw new UnauthorizedAccessException("Only the story author can spotlight recommendations.");

        // Flag invariant (WU-RecLifecycle): curation flags only ever true on live (Approved) recs.
        if (isHighlighted && rec.StatusId != ApprovedStatusId)
            throw new RecommendationValidationException(
                ["Only a live recommendation can be spotlighted."]);

        if (rec.IsHighlightedByAuthor == isHighlighted) return;

        if (isHighlighted)
        {
            int currentCount = await writeDb.Recommendations
                .CountAsync(r => r.StoryId == rec.StoryId && r.IsHighlightedByAuthor);
            if (currentCount >= RecommendationConstants.MaxHighlightedPerStory)
                throw new RecommendationValidationException(
                    [$"A story may have at most {RecommendationConstants.MaxHighlightedPerStory} spotlighted recommendations."]);
        }

        rec.IsHighlightedByAuthor = isHighlighted;
        await writeDb.SaveChangesAsync();
    }

    // ── Author lifecycle actions (WU-RecLifecycle) ───────────────────────────────

    public async Task RequestRevisionAsync(int recommendationId, string note)
    {
        (Recommendation rec, int userId) = await RequireStoryAuthorAsync(recommendationId, "Requesting a revision");

        // "Correct" path — inapplicable to a removed rec (use Unblock first if reconsidering).
        if (rec.StatusId == RejectedStatusId)
            throw new UnauthorizedAccessException(
                "This recommendation is removed. Unblock it before requesting a revision.");

        string trimmedNote = note?.Trim() ?? string.Empty;
        if (trimmedNote.Length == 0)
            throw new RecommendationValidationException(
                ["A revision request needs a note telling the recommender what to fix."]);
        if (trimmedNote.Length > MaxRevisionNoteLength)
            throw new RecommendationValidationException(
                [$"The revision note must be {MaxRevisionNoteLength} characters or fewer."]);

        rec.StatusId = NeedsRevisionStatusId;
        rec.RevisionRequestNote = trimmedNote; // repeat requests overwrite the note
        // Flag invariant: leaving Live clears both curation flags (slots freed, not auto-restored).
        rec.IsHiddenGem = false;
        rec.IsHighlightedByAuthor = false;
        await writeDb.SaveChangesAsync();

        if (rec.RecommenderId is int recommenderId)
            await NotifyBestEffortAsync(
                () => notifications.NotifyRecommendationRevisionRequestedAsync(recommenderId, userId, rec.StoryId),
                "RecommendationRevisionRequested", recommendationId);
    }

    public async Task RemoveAsync(int recommendationId)
    {
        (Recommendation rec, _) = await RequireStoryAuthorAsync(recommendationId, "Removing a recommendation");

        if (rec.StatusId == RejectedStatusId) return; // already removed — idempotent

        rec.StatusId = RejectedStatusId;
        rec.RevisionRequestNote = null;
        // Flag invariant: leaving Live clears both curation flags (slots freed, not auto-restored).
        rec.IsHiddenGem = false;
        rec.IsHighlightedByAuthor = false;
        await writeDb.SaveChangesAsync();
        // Silent — no notification (matches the moderation model's silent-rejection stance).
    }

    public async Task UnblockAsync(int recommendationId)
    {
        (Recommendation rec, int userId) = await RequireStoryAuthorAsync(recommendationId, "Unblocking a recommendation");

        if (rec.StatusId != RejectedStatusId)
            throw new InvalidOperationException("Only a removed recommendation can be unblocked.");

        rec.StatusId = ApprovedStatusId; // straight to live — the author already read it when removing
        await writeDb.SaveChangesAsync();

        if (rec.RecommenderId is int recommenderId)
            await NotifyBestEffortAsync(
                () => notifications.NotifyRecommendationApprovedAsync(recommenderId, userId, rec.StoryId),
                "RecommendationApproved", recommendationId);
    }

    // ── Attribution (Feature 30 — minted here, triggered by WU26) ───────────────

    public async Task RecordSuccessAsync(int recommendationId)
    {
        int userId = RequireAuthenticatedUser("Recording recommendation success");

        Recommendation? rec = await writeDb.Recommendations
            .FirstOrDefaultAsync(r => r.RecommendationId == recommendationId);
        if (rec is null)
            throw new KeyNotFoundException($"Recommendation {recommendationId} not found.");

        // Kind (g), and the sharpest case in the sweep: this method awards real site badges
        // (Recommender / RecommenderSilver) off an unverified parent, so a loop over guessed
        // recommendation ids could farm another user's SuccessfulRecCount and badges without ever
        // being able to see the stories involved. The anti-self-farm check below is not a substitute.
        await RequireRecommendationVisibleAsync(recommendationId);

        // Idempotent — composite PK prevents duplicates.
        bool alreadyRecorded = await writeDb.RecommendationSuccesses
            .AnyAsync(s => s.UserId == userId && s.RecommendationId == recommendationId);
        if (alreadyRecorded) return;

        writeDb.RecommendationSuccesses.Add(new RecommendationSuccess
        {
            UserId           = userId,
            RecommendationId = recommendationId
        });
        await writeDb.SaveChangesAsync();

        // Atomic delta after the insert commits (layer2-services.md counter rule — a tracked ++
        // is a read-modify-write that loses updates when concurrent readers trigger the same
        // recommendation; MA-502). Ordering matters: if the insert had thrown (composite-PK race),
        // this increment never runs.
        await writeDb.Recommendations
            .Where(r => r.RecommendationId == recommendationId)
            .ExecuteUpdateAsync(s => s.SetProperty(
                r => r.SuccessfulRecCount, r => r.SuccessfulRecCount + 1));

        // ── Tastemaker badge check (WU36) ────────────────────────────────────────
        // Anti-self-farm: skip if the reader IS the recommender, or if the rec is anonymous.
        // Best-effort: badge failure must never propagate back to the calling UI.
        int? recommenderId = rec.RecommenderId;
        if (recommenderId.HasValue && recommenderId.Value != userId)
        {
            // Increment the per-recommender aggregate counter.
            // ExecuteUpdateAsync is a no-op when no UserStat row exists — the award is skipped
            // harmlessly (counter stays 0, threshold not met). Production creates a UserStat row
            // on user registration; integration tests must seed one explicitly.
            await writeDb.UserStats
                .Where(us => us.UserId == recommenderId.Value)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    us => us.RecommendationSuccessesEarned,
                    us => us.RecommendationSuccessesEarned + 1));

            // Read the new total and evaluate badge thresholds.
            int total = await writeDb.UserStats
                .Where(us => us.UserId == recommenderId.Value)
                .Select(us => us.RecommendationSuccessesEarned)
                .FirstOrDefaultAsync();

            try
            {
                // Tier 1 (bronze) — 10 successful recommendations.
                if (total >= 10) await badges.AwardAsync(recommenderId.Value, SiteBadges.Recommender);
                // Tier 2 (silver) — 50 successful recommendations.
                if (total >= 50) await badges.AwardAsync(recommenderId.Value, SiteBadges.RecommenderSilver);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Badge award failed for recommender {RecommenderId} after RecordSuccessAsync — swallowed.",
                    recommenderId.Value);
            }
        }
    }

    public async Task RecordAttributionSourceAsync(int storyId, int recommendationId)
    {
        int userId = RequireAuthenticatedUser("Recording attribution source");

        // D3.2 (WU-RecLifecycle): the claimed source recommendation must exist AND belong to the
        // claimed story — otherwise a bogus self-attribution could later feed credit via
        // RecordSuccessAsync (modernization-audit/deferred-work.md §7).
        bool recBelongsToStory = await writeDb.Recommendations
            .AnyAsync(r => r.RecommendationId == recommendationId && r.StoryId == storyId);
        if (!recBelongsToStory)
            throw new KeyNotFoundException(
                $"Recommendation {recommendationId} does not exist for story {storyId}.");

        // Kind (g): D3.2 established that the rec must belong to the claimed story, but neither was
        // checked for visibility — the attribution feeds RecordSuccessAsync credit downstream.
        await RequireStoryVisibleAsync(storyId);

        // Upsert — if the source row already exists, keep the original attribution.
        bool alreadyExists = await writeDb.UserStoryRecommendationSources
            .AnyAsync(s => s.UserId == userId && s.StoryId == storyId);
        if (alreadyExists) return;

        writeDb.UserStoryRecommendationSources.Add(new UserStoryRecommendationSource
        {
            UserId                = userId,
            StoryId               = storyId,
            SourceRecommendationId = recommendationId
        });
        await writeDb.SaveChangesAsync();
    }
}
