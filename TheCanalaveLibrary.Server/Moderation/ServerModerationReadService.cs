using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation of <see cref="IModerationReadService"/>.
/// Uses <see cref="ReadOnlyApplicationDbContext"/> (no-tracking). All methods require the caller
/// to enforce moderator/admin role gating at the page or endpoint level — this service does not
/// re-check roles.
///
/// <para><b>Polymorphic target resolution</b> in <see cref="GetReportQueueAsync"/> follows the
/// same two-pass batch-enrichment pattern as <c>GetNotificationsAsync</c> (WU33): one query per
/// distinct <c>ReportedEntityType</c> present on the page, never N+1.</para>
/// </summary>
public class ServerModerationReadService(
    ReadOnlyApplicationDbContext readDb) : IModerationReadService
{
    // ── Expose ReadDb for the derived write service ───────────────────────────────

    protected ReadOnlyApplicationDbContext ReadDb { get; } = readDb;

    // ── Report reasons ────────────────────────────────────────────────────────────

    public async Task<ReportReasonDto[]> GetReportReasonsAsync() =>
        await ReadDb.ReportReasons
            .OrderBy(r => r.ReportReasonId)
            .Select(r => new ReportReasonDto(r.ReportReasonId, r.ReasonName, r.Description))
            .ToArrayAsync();

    // ── Report queue ─────────────────────────────────────────────────────────────

    public async Task<ReportQueueItemDto[]> GetReportQueueAsync(bool includeResolved = false)
    {
        // Step 1: materialize all matching reports with reason name + reporter username.
        var q = ReadDb.Reports
            .IgnoreQueryFilters() // ModeratedVisibility doesn't apply to the Report table itself
            .Where(r => includeResolved ||
                        r.ReportStatusId == ReportStatusEnum.Open ||
                        r.ReportStatusId == ReportStatusEnum.UnderReview);

        var rows = await (
            from r in q
            join rr in ReadDb.ReportReasons on r.ReportReasonId equals rr.ReportReasonId
            join reporter in ReadDb.Users on r.ReporterUserId equals reporter.Id into reporters
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
        var typesPresent = rows.Select(r => r.ReportedEntityType).Distinct().ToList();
        var entityIds = rows.ToDictionary(r => r.ReportId, r => r.ReportedEntityId);
        var labelMap = await BatchLoadTargetsAsync(typesPresent, rows
            .GroupBy(r => r.ReportedEntityType)
            .ToDictionary(g => g.Key, g => g.Select(r => r.ReportedEntityId).Distinct().ToList()));

        // Step 3: stitch.
        return [..rows
            .Select(r =>
            {
                var (label, url, reportCount) =
                    labelMap.TryGetValue(r.ReportedEntityType, out var dict) &&
                    dict.TryGetValue(r.ReportedEntityId, out var t)
                        ? t : ($"[{r.ReportedEntityType} #{r.ReportedEntityId}]", null, 0);

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

    public async Task<StorySubmissionQueueItemDto[]> GetPendingSubmissionsAsync() =>
        await (
            from s in ReadDb.Stories
                .IgnoreQueryFilters()
                .Where(s => s.StoryStatusId == StoryStatusEnum.PendingApproval)
            join sl in ReadDb.StoryListings on s.StoryId equals sl.StoryId
            join sd in ReadDb.StoryDetails on s.StoryId equals sd.StoryId
            join author in ReadDb.Users on s.AuthorId equals author.Id into authors
            from a in authors.DefaultIfEmpty()
            orderby s.PublishedDate
            select new StorySubmissionQueueItemDto(
                s.StoryId,
                sl.StoryTitle,
                a.UserName ?? "[deleted]",
                s.Rating,
                s.PublishedDate,
                sd.PostApprovalStatus,
                ReadDb.StoryImports.Any(si => si.StoryId == s.StoryId))
        ).ToArrayAsync();

    // ── Private: two-pass batch target resolution ─────────────────────────────────

    /// <summary>
    /// Returns one dictionary per <see cref="ReportedEntityType"/> present in
    /// <paramref name="idsPerType"/>, each mapping <c>entityId → (label, url, activeReportCount)</c>.
    /// </summary>
    private async Task<Dictionary<ReportedEntityType, Dictionary<long, (string Label, string? Url, int Count)>>>
        BatchLoadTargetsAsync(
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
                        from s in ReadDb.Stories.IgnoreQueryFilters()
                            .Where(s => intIds.Contains(s.StoryId))
                        join sl in ReadDb.StoryListings on s.StoryId equals sl.StoryId
                        join sd in ReadDb.StoryDetails on s.StoryId equals sd.StoryId
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
                    var data = await ReadDb.Users
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
                    var data = await ReadDb.BaseComments
                        .IgnoreQueryFilters()
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
                    var data = await ReadDb.BlogPosts
                        .IgnoreQueryFilters()
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
                    var data = await ReadDb.Recommendations
                        .IgnoreQueryFilters()
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
                    var msgs = await ReadDb.PrivateMessages
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
