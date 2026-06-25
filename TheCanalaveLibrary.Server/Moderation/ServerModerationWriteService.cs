using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation of <see cref="IModerationWriteService"/>. Inherits the read
/// path via primary-constructor chaining (CQRS-lite with write-inherits-read).
///
/// <para><b>Target-type allow-set.</b> Only Story, User, Comment, BlogPost, Recommendation, and
/// Message may be reported. Any other type throws <see cref="InvalidOperationException"/>.
/// Messages have no <c>ActiveReportCount</c> column — <see cref="AdjustActiveReportCountAsync"/>
/// is a no-op for that type (per cross-cutting.md §"Moderation Model (settled WU34)").</para>
///
/// <para><b>Notifications are best-effort.</b> All <c>NotifyXxx</c> calls happen <em>after</em>
/// the primary <c>SaveChangesAsync</c> inside a <c>try/catch</c> that logs and swallows — a
/// notification failure never rolls back a moderation action.</para>
/// </summary>
public class ServerModerationWriteService(
    ReadOnlyApplicationDbContext readDb,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    INotificationWriteService notifications,
    ILogger<ServerModerationWriteService> logger)
    : ServerModerationReadService(readDb), IModerationWriteService
{
    // ── Allowed reportable entity types ──────────────────────────────────────────

    private static readonly HashSet<ReportedEntityType> AllowedReportTargets =
    [
        ReportedEntityType.Story,
        ReportedEntityType.User,
        ReportedEntityType.Comment,
        ReportedEntityType.BlogPost,
        ReportedEntityType.Recommendation,
        ReportedEntityType.Message,
    ];

    // ── Report submission (Feature 46) ────────────────────────────────────────────

    public async Task SubmitReportAsync(SubmitReportRequest request)
    {
        if (!AllowedReportTargets.Contains(request.EntityType))
            throw new InvalidOperationException($"Entity type '{request.EntityType}' cannot be reported.");

        int? reporterId = activeUser.UserId;

        var report = new Report
        {
            ReportedEntityType = request.EntityType,
            ReportedEntityId = request.EntityId,
            ReportReasonId = request.ReasonId,
            Notes = request.Notes,
            ReporterUserId = reporterId,
            ReportStatusId = ReportStatusEnum.Open,
            DateReported = DateTime.UtcNow,
        };

        writeDb.Reports.Add(report);
        await AdjustActiveReportCountAsync(request.EntityType, request.EntityId, +1);
        await writeDb.SaveChangesAsync();

        if (reporterId.HasValue)
        {
            try { await notifications.NotifyReportReceivedAsync(reporterId.Value, reporterId.Value); }
            catch (Exception ex) { logger.LogWarning(ex, "ReportReceived notification failed for reporter {UserId}", reporterId.Value); }
        }
    }

    // ── Moderator queue actions (Feature 47) ─────────────────────────────────────

    public async Task ClaimReportAsync(long reportId)
    {
        int modId = RequireModerator();
        await writeDb.Reports
            .Where(r => r.ReportId == reportId && r.ReportStatusId == ReportStatusEnum.Open)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.ReportStatusId, ReportStatusEnum.UnderReview)
                .SetProperty(r => r.ModeratorUserId, modId));
    }

    public async Task ResolveNoActionAsync(long reportId, string? actionNotes)
    {
        int modId = RequireModerator();

        Report report = await writeDb.Reports.SingleAsync(r => r.ReportId == reportId);
        int? reporterUserId = report.ReporterUserId;
        var entityType = report.ReportedEntityType;
        long entityId = report.ReportedEntityId;

        report.ReportStatusId = ReportStatusEnum.ResolvedNoAction;
        report.ModeratorUserId = modId;
        report.ActionTaken = actionNotes;
        report.DateResolved = DateTime.UtcNow;

        await AdjustActiveReportCountAsync(entityType, entityId, -1);
        await writeDb.SaveChangesAsync();

        try
        {
            if (reporterUserId.HasValue)
                await notifications.NotifyReportResolvedNoActionAsync(reporterUserId.Value, modId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ReportResolvedNoAction notification failed for report {ReportId}", reportId);
        }
    }

    public async Task ResolveWithRemovalAsync(long reportId, string removalReason, bool hardDelete = false)
    {
        int modId = RequireModerator();

        Report report = await writeDb.Reports.SingleAsync(r => r.ReportId == reportId);
        int? reporterUserId = report.ReporterUserId;
        var entityType = report.ReportedEntityType;
        long entityId = report.ReportedEntityId;

        int? contentAuthorId = hardDelete
            ? await ApplyHardDeleteAsync(entityType, entityId)
            : await ApplyRemovalAsync(entityType, entityId, removalReason);

        report.ReportStatusId = ReportStatusEnum.ResolvedActionTaken;
        report.ModeratorUserId = modId;
        report.ActionTaken = removalReason;
        report.DateResolved = DateTime.UtcNow;

        await AdjustActiveReportCountAsync(entityType, entityId, -1);
        await writeDb.SaveChangesAsync();

        try
        {
            if (reporterUserId.HasValue)
                await notifications.NotifyReportResolvedAsync(reporterUserId.Value, modId);
            if (contentAuthorId.HasValue)
                await notifications.NotifyContentRemovedAsync(contentAuthorId.Value, modId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Post-removal notifications failed for report {ReportId}", reportId);
        }
    }

    public async Task ApplyAccountActionAsync(long reportId, ModeratorActionType action,
        string reason, DateTime? suspendedUntilUtc = null)
    {
        int modId = RequireModerator();
        Report report = await writeDb.Reports.SingleAsync(r => r.ReportId == reportId);

        int targetUserId = report.ReportedEntityType == ReportedEntityType.User
            ? (int)report.ReportedEntityId
            : throw new InvalidOperationException("Account actions require the report target to be a User.");

        User targetUser = await writeDb.Users.SingleAsync(u => u.Id == targetUserId);

        AccountStatusEnum newStatus = action switch
        {
            ModeratorActionType.WarnUser => AccountStatusEnum.Warned,
            ModeratorActionType.SuspendUser => AccountStatusEnum.Suspended,
            ModeratorActionType.BanUser => AccountStatusEnum.Banned,
            _ => throw new InvalidOperationException($"Action '{action}' is not an account action.")
        };

        targetUser.AccountStatus = newStatus;
        if (newStatus == AccountStatusEnum.Suspended)
            targetUser.SuspendedUntilUtc = suspendedUntilUtc;

        report.ReportStatusId = ReportStatusEnum.ResolvedActionTaken;
        report.ModeratorUserId = modId;
        report.ActionTaken = reason;
        report.DateResolved = DateTime.UtcNow;

        await writeDb.SaveChangesAsync();

        try
        {
            Task notifyTask = action switch
            {
                ModeratorActionType.WarnUser    => notifications.NotifyAccountWarningAsync(targetUserId, modId),
                ModeratorActionType.SuspendUser => notifications.NotifyAccountSuspendedAsync(targetUserId, modId),
                ModeratorActionType.BanUser     => notifications.NotifyAccountBannedAsync(targetUserId, modId),
                _ => Task.CompletedTask
            };
            await notifyTask;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Account action notification failed for user {UserId}, report {ReportId}", targetUserId, reportId);
        }
    }

    // ── Submission approval (Feature 48) ─────────────────────────────────────────

    public async Task ApproveStoryAsync(int storyId)
    {
        int modId = RequireModerator();

        Story story = await writeDb.Stories
            .IgnoreQueryFilters()
            .Include(s => s.StoryDetail)
            .SingleAsync(s => s.StoryId == storyId);

        if (story.StoryStatusId != StoryStatusEnum.PendingApproval)
            throw new InvalidOperationException($"Story {storyId} is not pending approval (current status: {story.StoryStatusId}).");

        int? authorId = story.AuthorId;
        StoryStatusEnum approvedStatus = story.StoryDetail.PostApprovalStatus;
        story.StoryStatusId = approvedStatus;

        await writeDb.SaveChangesAsync();

        try
        {
            if (authorId.HasValue)
                await notifications.NotifyStoryApprovedAsync(authorId.Value, storyId, modId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "StoryApproved notification failed for story {StoryId}", storyId);
        }
    }

    public async Task RejectStoryAsync(int storyId, string reason)
    {
        int modId = RequireModerator();

        Story story = await writeDb.Stories
            .IgnoreQueryFilters()
            .SingleAsync(s => s.StoryId == storyId);

        if (story.StoryStatusId != StoryStatusEnum.PendingApproval)
            throw new InvalidOperationException($"Story {storyId} is not pending approval (current status: {story.StoryStatusId}).");

        int? authorId = story.AuthorId;
        story.StoryStatusId = StoryStatusEnum.Rejected;
        story.ModerationRemovalReason = reason;
        story.DateModeratedRemoved = DateTime.UtcNow;

        await writeDb.SaveChangesAsync();

        try
        {
            if (authorId.HasValue)
                await notifications.NotifyStoryRejectedAsync(authorId.Value, storyId, modId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "StoryRejected notification failed for story {StoryId}", storyId);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private int RequireModerator()
    {
        if (activeUser.UserId is not int id)
            throw new InvalidOperationException("Moderator action requires an authenticated user.");
        if (!activeUser.IsModerator && !activeUser.IsAdmin)
            throw new InvalidOperationException("Moderator action requires the Moderator or Admin role.");
        return id;
    }

    /// <summary>
    /// Atomically increments (positive delta) or decrements (negative delta) the
    /// <c>ActiveReportCount</c> column on the target entity. No-op for <c>Message</c>
    /// (PrivateMessage has no counter column).
    /// </summary>
    private async Task AdjustActiveReportCountAsync(ReportedEntityType type, long id, int delta)
    {
        switch (type)
        {
            case ReportedEntityType.Story:
                await writeDb.Stories.IgnoreQueryFilters()
                    .Where(s => s.StoryId == (int)id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActiveReportCount, x => x.ActiveReportCount + delta));
                break;
            case ReportedEntityType.User:
                await writeDb.Users
                    .Where(u => u.Id == (int)id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActiveReportCount, x => x.ActiveReportCount + delta));
                break;
            case ReportedEntityType.Comment:
                await writeDb.BaseComments.IgnoreQueryFilters()
                    .Where(c => c.CommentId == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActiveReportCount, x => x.ActiveReportCount + delta));
                break;
            case ReportedEntityType.BlogPost:
                await writeDb.BlogPosts.IgnoreQueryFilters()
                    .Where(b => b.BlogPostId == (int)id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActiveReportCount, x => x.ActiveReportCount + delta));
                break;
            case ReportedEntityType.Recommendation:
                await writeDb.Recommendations.IgnoreQueryFilters()
                    .Where(r => r.RecommendationId == (int)id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActiveReportCount, x => x.ActiveReportCount + delta));
                break;
            case ReportedEntityType.Message:
                // PrivateMessage has no ActiveReportCount column; skip per settled WU34 convention.
                break;
        }
    }

    /// <summary>
    /// Soft-hides the target entity by setting <c>IsHidden = true</c> and recording
    /// the removal metadata. Returns the content author's user id (for notification), or
    /// <c>null</c> when the author cannot be determined.
    /// </summary>
    private async Task<int?> ApplyRemovalAsync(ReportedEntityType type, long id, string reason)
    {
        var removedAt = DateTime.UtcNow;

        switch (type)
        {
            case ReportedEntityType.Story:
            {
                var story = await writeDb.Stories.IgnoreQueryFilters()
                    .SingleOrDefaultAsync(s => s.StoryId == (int)id);
                if (story is null) return null;
                story.IsHidden = true;
                story.DateModeratedRemoved = removedAt;
                story.ModerationRemovalReason = reason;
                return story.AuthorId;
            }
            case ReportedEntityType.Comment:
            {
                var comment = await writeDb.BaseComments.IgnoreQueryFilters()
                    .SingleOrDefaultAsync(c => c.CommentId == id);
                if (comment is null) return null;
                comment.IsHidden = true;
                comment.DateModeratedRemoved = removedAt;
                comment.ModerationRemovalReason = reason;
                return comment.UserId;
            }
            case ReportedEntityType.BlogPost:
            {
                var post = await writeDb.BlogPosts.IgnoreQueryFilters()
                    .SingleOrDefaultAsync(b => b.BlogPostId == (int)id);
                if (post is null) return null;
                post.IsHidden = true;
                post.DateModeratedRemoved = removedAt;
                post.ModerationRemovalReason = reason;
                return post.AuthorId;
            }
            case ReportedEntityType.Recommendation:
            {
                var rec = await writeDb.Recommendations.IgnoreQueryFilters()
                    .SingleOrDefaultAsync(r => r.RecommendationId == (int)id);
                if (rec is null) return null;
                rec.IsHidden = true;
                rec.DateModeratedRemoved = removedAt;
                rec.ModerationRemovalReason = reason;
                return rec.RecommenderId;
            }
            case ReportedEntityType.Message:
                // Messages have no soft-delete columns; use ApplyHardDeleteAsync for illegal content.
                return await ApplyHardDeleteAsync(type, id);

            case ReportedEntityType.User:
                // User removal is handled via ApplyAccountActionAsync (account status change).
                return null;

            default:
                throw new InvalidOperationException($"Removal is not supported for entity type '{type}'.");
        }
    }

    /// <summary>
    /// Hard-deletes the target entity (illegal content path — CSAM, piracy). Returns the author's
    /// user id before deletion when possible, or <c>null</c>.
    /// </summary>
    private async Task<int?> ApplyHardDeleteAsync(ReportedEntityType type, long id)
    {
        switch (type)
        {
            case ReportedEntityType.Story:
            {
                var story = await writeDb.Stories.IgnoreQueryFilters()
                    .SingleOrDefaultAsync(s => s.StoryId == (int)id);
                if (story is null) return null;
                int? authorId = story.AuthorId;
                writeDb.Stories.Remove(story);
                return authorId;
            }
            case ReportedEntityType.Comment:
            {
                var comment = await writeDb.BaseComments.IgnoreQueryFilters()
                    .SingleOrDefaultAsync(c => c.CommentId == id);
                if (comment is null) return null;
                int? userId = comment.UserId;
                writeDb.BaseComments.Remove(comment);
                return userId;
            }
            case ReportedEntityType.BlogPost:
            {
                var post = await writeDb.BlogPosts.IgnoreQueryFilters()
                    .SingleOrDefaultAsync(b => b.BlogPostId == (int)id);
                if (post is null) return null;
                int? authorId = post.AuthorId;
                writeDb.BlogPosts.Remove(post);
                return authorId;
            }
            case ReportedEntityType.Recommendation:
            {
                var rec = await writeDb.Recommendations.IgnoreQueryFilters()
                    .SingleOrDefaultAsync(r => r.RecommendationId == (int)id);
                if (rec is null) return null;
                int? recommender = rec.RecommenderId;
                writeDb.Recommendations.Remove(rec);
                return recommender;
            }
            case ReportedEntityType.Message:
            {
                var msg = await writeDb.PrivateMessages
                    .SingleOrDefaultAsync(m => m.MessageId == id);
                if (msg is null) return null;
                writeDb.PrivateMessages.Remove(msg);
                return null;
            }
            default:
                throw new InvalidOperationException($"Hard delete is not supported for entity type '{type}'.");
        }
    }
}
