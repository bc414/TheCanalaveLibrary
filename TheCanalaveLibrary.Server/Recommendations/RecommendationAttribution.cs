using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The shared write-side rules of recommendation attribution (Feature 30, owner ruling D3) — used by
/// both entry points (<c>IUserStoryInteractionWriteService.SetReadItLaterFromRecommendationAsync</c>,
/// the RIL-from-card producer, and <c>MarkStartedAsync</c>'s direct-link parameter) and by the two
/// service-level removal sweeps. Internal static, like <see cref="StoryVisibilityGuard"/>: one
/// definition, many callers, no DI identity of its own.
///
/// <para>The attribution is <b>metadata on the <c>IsReadItLater</c> bit</b> — how that bit came to be
/// set. Rules: <c>layer2-services.md</c> §"Attribution (Feature 30)".</para>
/// </summary>
internal static class RecommendationAttribution
{
    private const short ApprovedStatusId = (short)RecommendationStatusEnum.Approved;

    /// <summary>
    /// Whether <paramref name="recommendationId"/> may become <paramref name="userId"/>'s attribution
    /// for <paramref name="storyId"/>: the recommendation exists, belongs to that story, is
    /// <c>Approved</c> and not taken down, the caller is not the story's author (D3's author gate —
    /// the author may save their own story for later, but no attribution row is created that could
    /// never be consumed), and the caller has not already credited it (the prompt's fourth gate hides
    /// such a row forever, so it could never be consumed either — the same reasoning, derived at the
    /// WU-InertFeatures review fixes). Reads the unfiltered write context: this is ground truth, not a
    /// viewer read.
    /// </summary>
    public static async Task<bool> IsAttributableAsync(
        ApplicationDbContext writeDb, int userId, int storyId, int recommendationId)
    {
        var rec = await writeDb.Recommendations
            .Where(r => r.RecommendationId == recommendationId)
            .Select(r => new
            {
                r.StoryId,
                r.StatusId,
                r.IsTakenDown,
                StoryAuthorId = r.Story.AuthorId,
                AlreadyCredited = writeDb.RecommendationSuccesses
                    .Any(s => s.UserId == userId && s.RecommendationId == r.RecommendationId),
            })
            .FirstOrDefaultAsync();

        return rec is not null
            && rec.StoryId == storyId
            && rec.StatusId == ApprovedStatusId
            && !rec.IsTakenDown
            && rec.StoryAuthorId != userId
            && !rec.AlreadyCredited;
    }

    /// <summary>
    /// D3 removal trigger 5: stages (tracked <c>RemoveRange</c>) the deletion of every attribution row
    /// naming <paramref name="recommendationId"/> — called when the recommendation is author-rejected
    /// or taken down, flag/status changes no FK cascade can see. The caller's own
    /// <c>SaveChangesAsync</c> commits it in the same unit of work as the status change. Not restored if
    /// the rejection or takedown is later reversed (accepted by D3).
    /// </summary>
    public static async Task StageSweepAsync(ApplicationDbContext writeDb, int recommendationId)
    {
        List<UserStoryRecommendationSource> rows = await writeDb.UserStoryRecommendationSources
            .Where(s => s.SourceRecommendationId == recommendationId)
            .ToListAsync();
        writeDb.UserStoryRecommendationSources.RemoveRange(rows);
    }
}
