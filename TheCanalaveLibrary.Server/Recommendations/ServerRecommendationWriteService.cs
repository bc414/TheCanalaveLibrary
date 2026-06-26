using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation for Recommendations. Inherits the read path via primary-constructor
/// chaining. All user HTML is sanitized on save (IHtmlSanitizationService); min-length is validated on
/// the stripped plain text (RecommendationText.CountPlainTextLength — layer2-services.md WU29 conventions).
///
/// <para><b>Auto-approve MVP:</b> StatusId is set to Approved (2) on submit. The moderation lifecycle
/// (Pending → author approval → moderator review) is deferred to WU34 — tracked in audit/Recommendations.md.</para>
/// <para><b>One-per-user-per-story:</b> enforced by the DB unique index. Duplicate submissions are caught
/// as <see cref="InvalidOperationException"/> with a friendly message.</para>
/// <para><b>Hidden Gem limit:</b> reject-at-5 (count against writeDb before setting). No auto-evict.
/// On set, best-effort post-commit notification fires to the story author via INotificationWriteService.</para>
/// <para><b>Like toggle:</b> no notification — anti-addictive design (§6.11).</para>
/// </summary>
public class ServerRecommendationWriteService(
    ReadOnlyApplicationDbContext readDb,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    IHtmlSanitizationService sanitizer,
    INotificationWriteService notifications,
    IBadgeWriteService badges,
    ILogger<ServerRecommendationWriteService> logger)
    : ServerRecommendationReadService(readDb, activeUser), IRecommendationWriteService
{
    private const short ApprovedStatusId = 2;

    // ── Helper ────────────────────────────────────────────────────────────────────

    private int RequireAuthenticatedUser(string action) =>
        ActiveUser.UserId ?? throw new InvalidOperationException($"{action} requires an authenticated user.");

    // ── Submit ───────────────────────────────────────────────────────────────────

    public async Task<int> SubmitAsync(RecommendationSubmitDto dto)
    {
        int userId = RequireAuthenticatedUser("Submitting a recommendation");

        // IgnoreQueryFilters: the ContentRating filter must not prevent recommending M-rated stories;
        // the story must exist, not be visible to the recommender.
        // Project to anonymous type so null AuthorId (authorless story) is not confused with "row
        // not found" — FirstOrDefault<int?> cannot distinguish the two cases.
        var storyRow = await writeDb.Stories
            .IgnoreQueryFilters(["ContentRating"])
            .Where(s => s.StoryId == dto.StoryId)
            .Select(s => new { s.AuthorId })
            .FirstOrDefaultAsync();
        if (storyRow is null)
            throw new KeyNotFoundException($"Story {dto.StoryId} not found.");
        int? storyAuthorId = storyRow.AuthorId;

        string sanitizedText = sanitizer.Sanitize(dto.Text);

        List<string> errors = dto.CanSave(sanitizedText);
        if (errors.Count > 0) throw new RecommendationValidationException(errors);

        Recommendation rec = new()
        {
            StoryId     = dto.StoryId,
            RecommenderId = userId,
            StatusId    = ApprovedStatusId, // auto-approve MVP (moderation deferred to WU34)
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

        string sanitizedText = sanitizer.Sanitize(dto.Text);

        List<string> errors = dto.CanSave(sanitizedText);
        if (errors.Count > 0) throw new RecommendationValidationException(errors);

        rec.RecommendationDetail.Text = sanitizedText;
        await writeDb.SaveChangesAsync();
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

        RecommendationLike? existing = rec.Likes.FirstOrDefault();
        bool nowLiked;

        if (existing is not null)
        {
            writeDb.RecommendationLikes.Remove(existing);
            rec.LikeCount = Math.Max(0, rec.LikeCount - 1);
            nowLiked = false;
        }
        else
        {
            writeDb.RecommendationLikes.Add(new RecommendationLike
            {
                RecommendationId = recommendationId,
                UserId = userId
            });
            rec.LikeCount++;
            nowLiked = true;
        }

        await writeDb.SaveChangesAsync();
        // No notification — anti-addictive design (§6.11).

        return new RecommendationLikeResultDto(rec.LikeCount, nowLiked);
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

        if (rec.IsHiddenGem == isHiddenGem) return; // already in desired state

        if (isHiddenGem)
        {
            // Reject-at-limit: count active Hidden Gems for this user (writeDb for consistency).
            int currentCount = await writeDb.Recommendations
                .CountAsync(r => r.RecommenderId == userId && r.IsHiddenGem);
            if (currentCount >= RecommendationConstants.MaxHiddenGemsPerUser)
                throw new InvalidOperationException(
                    $"You already have {RecommendationConstants.MaxHiddenGemsPerUser} Hidden Gem designations. " +
                    "Remove one before adding another.");
        }

        rec.IsHiddenGem = isHiddenGem;
        await writeDb.SaveChangesAsync();

        if (isHiddenGem)
        {
            // Best-effort post-commit notification to story author.
            try
            {
                int? storyAuthorId = await writeDb.Stories
                    .Where(s => s.StoryId == rec.StoryId)
                    .Select(s => (int?)s.AuthorId)
                    .FirstOrDefaultAsync();
                if (storyAuthorId.HasValue)
                    await notifications.NotifyStoryHiddenGemAsync(storyAuthorId.Value, userId);
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

        if (rec.IsHighlightedByAuthor == isHighlighted) return;

        if (isHighlighted)
        {
            int currentCount = await writeDb.Recommendations
                .CountAsync(r => r.StoryId == rec.StoryId && r.IsHighlightedByAuthor);
            if (currentCount >= RecommendationConstants.MaxHighlightedPerStory)
                throw new InvalidOperationException(
                    $"A story may have at most {RecommendationConstants.MaxHighlightedPerStory} spotlighted recommendations.");
        }

        rec.IsHighlightedByAuthor = isHighlighted;
        await writeDb.SaveChangesAsync();
    }

    // ── Attribution (Feature 30 — minted here, triggered by WU26) ───────────────

    public async Task RecordSuccessAsync(int recommendationId)
    {
        int userId = RequireAuthenticatedUser("Recording recommendation success");

        Recommendation? rec = await writeDb.Recommendations
            .FirstOrDefaultAsync(r => r.RecommendationId == recommendationId);
        if (rec is null)
            throw new KeyNotFoundException($"Recommendation {recommendationId} not found.");

        // Idempotent — composite PK prevents duplicates.
        bool alreadyRecorded = await writeDb.RecommendationSuccesses
            .AnyAsync(s => s.UserId == userId && s.RecommendationId == recommendationId);
        if (alreadyRecorded) return;

        writeDb.RecommendationSuccesses.Add(new RecommendationSuccess
        {
            UserId           = userId,
            RecommendationId = recommendationId
        });
        rec.SuccessfulRecCount++;

        await writeDb.SaveChangesAsync();

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
