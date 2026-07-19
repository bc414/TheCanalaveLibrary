using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation of <see cref="IModerationReadService"/>.
/// Uses <see cref="ReadOnlyApplicationDbContext"/> (no-tracking). All methods require the caller
/// to enforce moderator/admin role gating at the page or endpoint level — this service does not
/// re-check roles.
///
/// <para><b>Filter contract (settled 2026-07-18, supersedes 2026-06-26).</b> Review/entity loads
/// bypass <c>IsTakenDown</c> <em>and</em> ContentRating/GroupAudience — moderation review surfaces
/// are work surfaces, exempt from the personal comfort filter that gates ordinary browsing. A
/// moderator sees every open report and every pending submission regardless of their own
/// ShowMatureContent setting; the action path (<see cref="ServerModerationWriteService"/>) was
/// already unfiltered ground truth, so scoping only the read side produced an incoherent middle
/// state rather than a real access boundary. See <c>content-safety.md</c> §"Moderator review
/// surfaces are work surfaces".</para>
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
        // ContentRating/GroupAudience are bypassed here (see BatchLoadTargetsAsync) — the report
        // queue is a work surface, not scoped by the moderator's personal rating preference.
        var typesPresent = rows.Select(r => r.ReportedEntityType).Distinct().ToList();
        var labelMap = await BatchLoadTargetsAsync(readDb, typesPresent, rows
            .GroupBy(r => r.ReportedEntityType)
            .ToDictionary(g => g.Key, g => g.Select(r => r.ReportedEntityId).Distinct().ToList()));

        // Step 3: stitch — drop report rows whose target no longer materializes (e.g. hard-deleted
        // between report submission and queue load). Not a rating-based drop — see Step 2.
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
                // elevated read: pending submissions are a work surface, not scoped by the
                // moderator's personal ContentRating/ShowMatureContent preference.
                .IgnoreQueryFilters(["IsTakenDown", "ContentRating"])
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
    /// <c>IsTakenDown</c> and, for <see cref="ReportedEntityType.Story"/>, <c>ContentRating</c> are
    /// bypassed — the report queue is a moderator work surface, not scoped by the reviewer's personal
    /// rating preference (settled 2026-07-18; see <c>content-safety.md</c> §"Moderator review
    /// surfaces are work surfaces"). Every other target type here already carries no rating filter
    /// (Recommendation/BlogPost/Comment/User/Message), so this keeps all six arms consistent.
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
                        from s in readDb.Stories
                            // elevated read: mod queue is a work surface — sees taken-down content
                            // and is not scoped by the moderator's personal ContentRating preference.
                            .IgnoreQueryFilters(["IsTakenDown", "ContentRating"])
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
