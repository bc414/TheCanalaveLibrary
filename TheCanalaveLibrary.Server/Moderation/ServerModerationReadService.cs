using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation of <see cref="IModerationReadService"/>.
/// Uses <see cref="ReadOnlyApplicationDbContext"/> (no-tracking). All methods require the caller
/// to enforce moderator/admin role gating at the page or endpoint level — this service does not
/// re-check roles.
///
/// <para><b>Filter contract.</b> Review/entity loads bypass <em>only</em> <c>IsTakenDown</c>
/// (so already-taken-down content remains viewable in the queue). ContentRating and GroupAudience
/// stay live — a moderator's content-rating reach equals their personal ShowMatureContent setting,
/// same as when browsing (settled 2026-06-26). Reports on entities filtered out by ContentRating
/// are <em>dropped</em> from the queue rather than shown as a placeholder; an M-rated story simply
/// does not appear for a T-only moderator.</para>
///
/// <para><b>Polymorphic target resolution</b> in <see cref="GetReportQueueAsync"/> follows the
/// same two-pass batch-enrichment pattern as <c>GetNotificationsAsync</c> (WU33): one query per
/// distinct <c>ReportedEntityType</c> present on the page, never N+1.</para>
/// </summary>
public class ServerModerationReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory) : IModerationReadService
{
    // ── Expose the read-context factory for the derived write service ─────────────
    // Contexts are created per method (`await using`) — see layer2-services.md
    // §"Read-context concurrency: factory per method".

    protected IDbContextFactory<ReadOnlyApplicationDbContext> ReadDbFactory { get; } = readDbFactory;

    // ── Report reasons ────────────────────────────────────────────────────────────

    public async Task<ReportReasonDto[]> GetReportReasonsAsync()
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        return await readDb.ReportReasons
            .OrderBy(r => r.ReportReasonId)
            .Select(r => new ReportReasonDto(r.ReportReasonId, r.ReasonName, r.Description))
            .ToArrayAsync();
    }

    // ── Report queue ─────────────────────────────────────────────────────────────

    public async Task<ReportQueueItemDto[]> GetReportQueueAsync(bool includeResolved = false)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // Step 1: materialize all matching reports with reason name + reporter username.
        // Note: Report table has no named filters; no IgnoreQueryFilters needed here.
        var q = readDb.Reports
            .Where(r => includeResolved ||
                        r.ReportStatusId == ReportStatusEnum.Open ||
                        r.ReportStatusId == ReportStatusEnum.UnderReview);

        var rows = await (
            from r in q
            join rr in readDb.ReportReasons on r.ReportReasonId equals rr.ReportReasonId
            join reporter in readDb.Users on r.ReporterUserId equals reporter.Id into reporters
            from rep in reporters.DefaultIfEmpty()
            select new
            {
                r.ReportId,
                r.ReportedEntityType,
                r.ReportedEntityId,
                r.ReportStatusId,
                r.Notes,
                r.ModeratorUserId,
                r.ActionTaken,
                r.DateReported,
                r.DateResolved,
                ReasonName = rr.ReasonName,
                ReporterUserName = (string?)rep.UserName,
            }
        ).ToListAsync();

        if (rows.Count == 0) return [];

        // Step 2: batch-load target labels, URLs, and ActiveReportCount per entity type present.
        // ContentRating stays live here — entities filtered by the moderator's rating preference
        // simply won't appear in the dictionary, and their report rows are dropped in Step 3.
        var typesPresent = rows.Select(r => r.ReportedEntityType).Distinct().ToList();
        var labelMap = await BatchLoadTargetsAsync(readDb, typesPresent, rows
            .GroupBy(r => r.ReportedEntityType)
            .ToDictionary(g => g.Key, g => g.Select(r => r.ReportedEntityId).Distinct().ToList()));

        // Step 3: stitch — drop report rows whose target was filtered by ContentRating/GroupAudience.
        // This is the mechanism that makes the queue per-moderator-rating-scoped: an M story simply
        // doesn't appear for a T-only mod, rather than showing as a labelless placeholder.
        return [..rows
            .Where(r =>
                labelMap.TryGetValue(r.ReportedEntityType, out var dict) &&
                dict.ContainsKey(r.ReportedEntityId))
            .Select(r =>
            {
                var (label, url, reportCount) =
                    labelMap[r.ReportedEntityType][r.ReportedEntityId];

                return new ReportQueueItemDto(
                    r.ReportId,
                    r.ReportedEntityType,
                    r.ReportedEntityId,
                    label,
                    url,
                    r.ReasonName,
                    r.Notes,
                    r.ReportStatusId,
                    r.ReporterUserName,
                    r.ModeratorUserId,
                    r.ActionTaken,
                    r.DateReported,
                    r.DateResolved,
                    reportCount);
            })
            // Most-reported first; ties broken by oldest report first (triage urgency).
            .OrderByDescending(r => r.TargetActiveReportCount)
            .ThenBy(r => r.DateReported)];
    }

    // ── Pending submissions ───────────────────────────────────────────────────────

    public async Task<StorySubmissionQueueItemDto[]> GetPendingSubmissionsAsync()
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        return await (
            from s in readDb.Stories
                .IgnoreQueryFilters(["IsTakenDown"]) // elevated read: pending submissions may be taken-down
                .Where(s => s.StoryStatusId == StoryStatusEnum.PendingApproval)
            join sl in readDb.StoryListings on s.StoryId equals sl.StoryId
            join sd in readDb.StoryDetails on s.StoryId equals sd.StoryId
            join author in readDb.Users on s.AuthorId equals author.Id into authors
            from a in authors.DefaultIfEmpty()
            orderby s.PublishedDate
            select new StorySubmissionQueueItemDto(
                s.StoryId,
                sl.StoryTitle,
                a.UserName ?? "[deleted]",
                s.Rating,
                s.PublishedDate,
                sd.PostApprovalStatus,
                // WU38d remodel: "is an import" ⇒ the story lists at least one external source link.
                readDb.StoryExternalLinks.Any(sel => sel.StoryId == s.StoryId))
        ).ToArrayAsync();
    }

    // ── Private: two-pass batch target resolution ─────────────────────────────────

    /// <summary>
    /// Returns one dictionary per <see cref="ReportedEntityType"/> present in
    /// <paramref name="idsPerType"/>, each mapping <c>entityId → (label, url, activeReportCount)</c>.
    /// ContentRating and GroupAudience filters stay live — entities filtered out simply won't
    /// appear in the returned dictionary, causing their report rows to be dropped by the caller.
    /// Only <c>IsTakenDown</c> is bypassed so taken-down content is still reviewable.
    /// </summary>
    private static async Task<Dictionary<ReportedEntityType, Dictionary<long, (string Label, string? Url, int Count)>>>
        BatchLoadTargetsAsync(
            ReadOnlyApplicationDbContext readDb,
            IReadOnlyList<ReportedEntityType> typesPresent,
            Dictionary<ReportedEntityType, List<long>> idsPerType)
    {
        var result = new Dictionary<ReportedEntityType, Dictionary<long, (string, string?, int)>>();

        foreach (var type in typesPresent)
        {
            var ids = idsPerType[type];

            switch (type)
            {
                case ReportedEntityType.Story:
                {
                    var intIds = ids.Select(id => (int)id).ToList();
                    var data = await (
                        from s in readDb.Stories.IgnoreQueryFilters(["IsTakenDown"]) // elevated read: mod queue sees taken-down content
                            .Where(s => intIds.Contains(s.StoryId))
                        join sl in readDb.StoryListings on s.StoryId equals sl.StoryId
                        join sd in readDb.StoryDetails on s.StoryId equals sd.StoryId
                        select new { s.StoryId, sl.StoryTitle, sd.Slug, s.ActiveReportCount }
                    ).ToListAsync();

                    result[type] = data.ToDictionary(
                        x => (long)x.StoryId,
                        x => (x.StoryTitle, (string?)$"/story/{x.StoryId}/{x.Slug}", x.ActiveReportCount));
                    break;
                }
                case ReportedEntityType.User:
                {
                    var intIds = ids.Select(id => (int)id).ToList();
                    var data = await readDb.Users
                        .Where(u => intIds.Contains(u.Id))
                        .Select(u => new { u.Id, u.UserName, u.ActiveReportCount })
                        .ToListAsync();

                    result[type] = data.ToDictionary(
                        x => (long)x.Id,
                        x => (x.UserName ?? $"User#{x.Id}", (string?)$"/user/{x.UserName}", x.ActiveReportCount));
                    break;
                }
                case ReportedEntityType.Comment:
                {
                    var longIds = ids;
                    var data = await readDb.BaseComments
                        .IgnoreQueryFilters(["IsTakenDown"]) // elevated read: mod queue sees taken-down content
                        .Where(c => longIds.Contains(c.CommentId))
                        .Select(c => new
                        {
                            c.CommentId,
                            Preview = c.CommentText.Length > 80 ? c.CommentText.Substring(0, 80) + "…" : c.CommentText,
                            c.ActiveReportCount
                        })
                        .ToListAsync();

                    result[type] = data.ToDictionary(
                        x => x.CommentId,
                        x => ($"Comment: \"{x.Preview}\"", (string?)null, x.ActiveReportCount));
                    break;
                }
                case ReportedEntityType.BlogPost:
                {
                    var intIds = ids.Select(id => (int)id).ToList();
                    var data = await readDb.BlogPosts
                        .IgnoreQueryFilters(["IsTakenDown"]) // elevated read: mod queue sees taken-down content
                        .Where(b => intIds.Contains(b.BlogPostId))
                        .Select(b => new { b.BlogPostId, b.Title, b.ActiveReportCount })
                        .ToListAsync();

                    result[type] = data.ToDictionary(
                        x => (long)x.BlogPostId,
                        x => (x.Title, (string?)null, x.ActiveReportCount));
                    break;
                }
                case ReportedEntityType.Recommendation:
                {
                    var intIds = ids.Select(id => (int)id).ToList();
                    var data = await readDb.Recommendations
                        .IgnoreQueryFilters(["IsTakenDown"]) // elevated read: mod queue sees taken-down content
                        .Where(r => intIds.Contains(r.RecommendationId))
                        .Select(r => new { r.RecommendationId, r.StoryId, r.ActiveReportCount })
                        .ToListAsync();

                    result[type] = data.ToDictionary(
                        x => (long)x.RecommendationId,
                        x => ($"Recommendation on Story#{x.StoryId}", (string?)$"/story/{x.StoryId}", x.ActiveReportCount));
                    break;
                }
                case ReportedEntityType.Message:
                    // No navigable URL; messages are reviewed in-panel by moderators.
                    var msgIds = ids;
                    var msgs = await readDb.PrivateMessages
                        .Where(m => msgIds.Contains(m.MessageId))
                        .Select(m => new { m.MessageId, m.ConversationId })
                        .ToListAsync();
                    result[type] = msgs.ToDictionary(
                        x => x.MessageId,
                        x => ($"Private Message #{x.MessageId}", (string?)null, 0));
                    break;
            }
        }

        return result;
    }
}
