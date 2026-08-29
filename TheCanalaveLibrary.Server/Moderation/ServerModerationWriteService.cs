using Microsoft.AspNetCore.Identity;
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
/// <para><b>IModeratableContent.</b> Story, Comment, BlogPost, and Recommendation implement
/// <see cref="IModeratableContent"/>; their soft-remove and hard-delete operations are handled
/// through shared interface code after a single per-type load. User and Message stay explicitly
/// special-cased: User uses ApplyAccountActionAsync; Message goes straight to hard-delete with no
/// takedown columns.</para>
///
/// <para><b>Content-rating filter.</b> The write context (<c>writeDb</c>) is never filtered — every
/// action here already acts on ground truth regardless of rating, and always has. The read side
/// (<see cref="ServerModerationReadService"/>) now matches: review/entity-load reads bypass
/// <c>IsTakenDown</c> and ContentRating/GroupAudience alike, since moderation review is a work
/// surface exempt from the reviewer's personal ShowMatureContent setting (settled 2026-07-18,
/// supersedes the 2026-06-26 "mirrors browsing" framing — see <c>content-safety.md</c>
/// §"Moderator review surfaces are work surfaces").</para>
///
/// <para><b>Notifications are best-effort.</b> All <c>NotifyXxx</c> calls happen <em>after</em>
/// the primary <c>SaveChangesAsync</c> inside a <c>try/catch</c> that logs and swallows — a
/// notification failure never rolls back a moderation action.</para>
/// </summary>
public class ServerModerationWriteService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    INotificationWriteService notifications,
    IWriteRateLimitService rateLimit,
    UserManager<User> userManager,
    ILogger<ServerModerationWriteService> logger)
    : ServerModerationReadService(readDbFactory), IModerationWriteService
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
        // Reports may be anonymous (nullable reporter) — throttle only the authenticated axis;
        // report-form UI is auth-gated, so this covers every real path (security.md).
        if (reporterId is int throttleUserId)
            rateLimit.EnsureAllowed(WriteActionKind.Report, throttleUserId);

        // Kind (g), settled 2026-07-26: the target must EXIST always, and must be VISIBLE to the
        // reporter EXCEPT when the only thing hiding it is a takedown. Rationale: this method had no
        // existence check at all, so the queue could be flooded with reports against ids that never
        // existed while AdjustActiveReportCountAsync bumped ActiveReportCount on drafts. The takedown
        // exemption keeps a good-faith report legitimate when content is removed between the moment a
        // user opens the report form and the moment they submit it.
        await RequireReportableTargetAsync(request.EntityType, request.EntityId);

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
        AccountStatusEnum newStatus = ToAccountStatus(action);

        Report report = await writeDb.Reports.SingleAsync(r => r.ReportId == reportId);

        // The user acted on is RESOLVED from the report, not assumed to be its target: warning over
        // a reported story means warning that story's author. The WU34 rule this replaces
        // (User-targeted reports only) threw for every report the app can actually produce, since
        // only Story and Comment have report entry points — see layer2-services.md §"Account
        // actions — target resolution and the report-as-audit-record rule".
        int targetUserId = await ResolveActionTargetUserIdAsync(report);
        User targetUser = await writeDb.Users.SingleAsync(u => u.Id == targetUserId);

        report.ReportStatusId = ReportStatusEnum.ResolvedActionTaken;
        report.ModeratorUserId = modId;
        report.ActionTaken = reason;
        report.DateResolved = DateTime.UtcNow;

        // This IS a resolve path, so it decrements like its two siblings do. Omitting it leaked the
        // counter upward forever — and that counter is what the mod-triage sort orders on.
        await AdjustActiveReportCountAsync(report.ReportedEntityType, report.ReportedEntityId, -1);

        await ApplyStatusAndNotifyAsync(targetUser, action, newStatus, modId, suspendedUntilUtc);
    }

    public async Task ApplyAccountActionToUserAsync(int targetUserId, short reasonId,
        ModeratorActionType action, string reason, DateTime? suspendedUntilUtc = null)
    {
        int modId = RequireModerator();
        AccountStatusEnum newStatus = ToAccountStatus(action);

        if (targetUserId == modId)
            throw new ModerationValidationException(["You can't apply an account action to yourself."]);

        User targetUser = await writeDb.Users.SingleOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new KeyNotFoundException($"User {targetUserId} was not found.");

        if (!await writeDb.ReportReasons.AnyAsync(rr => rr.ReportReasonId == reasonId))
            throw new ModerationValidationException(["Choose a reason for this action."]);

        // The Report row IS the audit record — there is no separate moderation-action table. A
        // moderator acting without a member report files one, and ReporterUserId == ModeratorUserId
        // is what marks it moderator-initiated.
        DateTime now = DateTime.UtcNow;
        writeDb.Reports.Add(new Report
        {
            ReportedEntityType = ReportedEntityType.User,
            ReportedEntityId = targetUserId,
            ReportReasonId = reasonId,
            Notes = reason,
            ReporterUserId = modId,
            ReportStatusId = ReportStatusEnum.ResolvedActionTaken,
            ModeratorUserId = modId,
            ActionTaken = reason,
            DateReported = now,
            DateResolved = now,
        });

        // No AdjustActiveReportCountAsync call: the row opens and resolves in one step, so the
        // +1/-1 pair every other path makes would cancel out.
        await ApplyStatusAndNotifyAsync(targetUser, action, newStatus, modId, suspendedUntilUtc);
    }

    // ── Submission approval (Feature 48) ─────────────────────────────────────────

    public async Task ApproveStoryAsync(int storyId)
    {
        int modId = RequireModerator();

        Story story = await writeDb.Stories
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
            .SingleAsync(s => s.StoryId == storyId);

        if (story.StoryStatusId != StoryStatusEnum.PendingApproval)
            throw new InvalidOperationException($"Story {storyId} is not pending approval (current status: {story.StoryStatusId}).");

        int? authorId = story.AuthorId;
        story.StoryStatusId = StoryStatusEnum.Rejected;
        story.TakedownReason = reason;
        story.TakedownDate = DateTime.UtcNow;

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

    /// <summary>
    /// Maps the three account actions onto their resulting <see cref="AccountStatusEnum"/>. Called
    /// at the top of both public entry points so an invalid action throws before anything mutates.
    /// </summary>
    private static AccountStatusEnum ToAccountStatus(ModeratorActionType action) => action switch
    {
        ModeratorActionType.WarnUser => AccountStatusEnum.Warned,
        ModeratorActionType.SuspendUser => AccountStatusEnum.Suspended,
        ModeratorActionType.BanUser => AccountStatusEnum.Banned,
        _ => throw new InvalidOperationException($"Action '{action}' is not an account action.")
    };

    /// <summary>
    /// Which user an account action lands on, derived from the report: a User report acts on that
    /// user; a content report acts on the content's author; a Message report acts on its sender.
    /// <para>Throws <see cref="ModerationValidationException"/> — a user-facing type — when no
    /// account can be resolved, so the moderator is told why instead of getting the generic
    /// error.</para>
    /// </summary>
    private async Task<int> ResolveActionTargetUserIdAsync(Report report)
    {
        int? targetUserId = report.ReportedEntityType switch
        {
            ReportedEntityType.User => (int)report.ReportedEntityId,

            // PrivateMessage.SenderUserId is SetNull on account deletion, hence nullable here.
            ReportedEntityType.Message => (await writeDb.PrivateMessages
                .SingleOrDefaultAsync(m => m.MessageId == report.ReportedEntityId))?.SenderUserId,

            // Story / Comment / BlogPost / Recommendation — all IModeratableContent.
            _ => (await LoadModeratableAsync(report.ReportedEntityType, report.ReportedEntityId))
                ?.AuthorUserId,
        };

        return targetUserId ?? throw new ModerationValidationException(
        [
            "There's no account to act on for this report — the content is anonymous, or its " +
            "author's account has been deleted. Remove the content instead."
        ]);
    }

    /// <summary>
    /// The shared tail of both account-action entry points: set the status, persist, kill live
    /// sessions where the action warrants it, then notify best-effort. Extracted at
    /// WU-UserModeration so the report-driven and moderator-initiated paths cannot drift apart.
    /// </summary>
    private async Task ApplyStatusAndNotifyAsync(User targetUser, ModeratorActionType action,
        AccountStatusEnum newStatus, int modId, DateTime? suspendedUntilUtc)
    {
        targetUser.AccountStatus = newStatus;
        if (newStatus == AccountStatusEnum.Suspended)
            targetUser.SuspendedUntilUtc = suspendedUntilUtc;

        await writeDb.SaveChangesAsync();

        // Kill any already-open session for Suspend/Ban (not Warn — a warning must not log the
        // user out) — WU38a. IdentityRevalidatingAuthenticationStateProvider re-checks the
        // security stamp every 30 minutes; a mismatch ends the live circuit, and the next sign-in
        // attempt is blocked by CanalaveSignInManager.CanSignInAsync.
        if (newStatus is AccountStatusEnum.Suspended or AccountStatusEnum.Banned)
            await userManager.UpdateSecurityStampAsync(targetUser);

        try
        {
            Task notifyTask = action switch
            {
                ModeratorActionType.WarnUser    => notifications.NotifyAccountWarningAsync(targetUser.Id, modId),
                ModeratorActionType.SuspendUser => notifications.NotifyAccountSuspendedAsync(targetUser.Id, modId),
                ModeratorActionType.BanUser     => notifications.NotifyAccountBannedAsync(targetUser.Id, modId),
                _ => Task.CompletedTask
            };
            await notifyTask;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Account action notification failed for user {UserId}", targetUser.Id);
        }
    }

    private int RequireModerator()
    {
        if (activeUser.UserId is not int id)
            throw new InvalidOperationException("Moderator action requires an authenticated user.");
        // UnauthorizedAccessException, not InvalidOperationException: a signed-in non-mod is
        // authenticated-but-forbidden → 403 via EndpointHelpers, matching Spotlight/SiteSettings/
        // Poll's identical role gates (MA-123/MA-701 — this side was the wrong one).
        if (!activeUser.IsModerator && !activeUser.IsAdmin)
            throw new UnauthorizedAccessException("Moderator action requires the Moderator or Admin role.");
        return id;
    }

    /// <summary>
    /// Loads the <see cref="IModeratableContent"/> entity for the given type and id from the
    /// write context (unfiltered — sees ground truth regardless of takedown/rating state).
    /// ContentRating and GroupAudience do not apply on the write context; a moderator acting on
    /// a taken-down or rating-gated entity acts on ground truth, not the public view.
    /// Returns <c>null</c> if the entity doesn't exist.
    /// Message and User are not IModeratableContent — callers handle them directly.
    /// </summary>
    private async Task<IModeratableContent?> LoadModeratableAsync(ReportedEntityType type, long id)
    {
        switch (type)
        {
            case ReportedEntityType.Story:
                return await writeDb.Stories
                    .SingleOrDefaultAsync(s => s.StoryId == (int)id);

            case ReportedEntityType.Comment:
                return await writeDb.BaseComments
                    .SingleOrDefaultAsync(c => c.CommentId == id);

            case ReportedEntityType.BlogPost:
                return await writeDb.BlogPosts
                    .SingleOrDefaultAsync(b => b.BlogPostId == (int)id);

            case ReportedEntityType.Recommendation:
                return await writeDb.Recommendations
                    .SingleOrDefaultAsync(r => r.RecommendationId == (int)id);

            default:
                throw new InvalidOperationException($"LoadModeratableAsync does not handle '{type}' — use the direct path.");
        }
    }

    /// <summary>
    /// Atomically increments (positive delta) or decrements (negative delta) the
    /// <c>ActiveReportCount</c> column on the target entity. No-op for <c>Message</c>
    /// (PrivateMessage has no counter column).
    /// Uses ExecuteUpdateAsync (set-based, no load) — does not go through IModeratableContent.
    /// Write context is unfiltered — taken-down content gets its counter adjusted correctly.
    /// </summary>
    /// <summary>
    /// Kind (g) for report submission (settled 2026-07-26). Two rules, deliberately different:
    /// <list type="bullet">
    /// <item><b>Existence is always required</b> — checked on the unfiltered write context, so a
    /// taken-down target still counts as existing.</item>
    /// <item><b>Visibility is required except for takedown</b> — a reporter who legitimately opened
    /// content that a moderator removed a moment later must still be able to file. Everything else
    /// (draft/unpublished, non-public story status, rating without consent, Private profile,
    /// M-audience group) must be refused.</item>
    /// </list>
    /// Throws <see cref="KeyNotFoundException"/> either way so a hidden target and an absent one are
    /// indistinguishable. <c>Message</c> is participant-scoped and validated by its own path.
    /// </summary>
    private async Task RequireReportableTargetAsync(ReportedEntityType type, long id)
    {
        KeyNotFoundException NotFound() =>
            new($"The reported {type} could not be found.");

        // Existence on the unfiltered write context — a taken-down row is still a real row.
        bool exists = type switch
        {
            ReportedEntityType.Story => await writeDb.Stories.AnyAsync(s => s.StoryId == (int)id),
            ReportedEntityType.User => await writeDb.Users.AnyAsync(u => u.Id == (int)id),
            ReportedEntityType.Comment => await writeDb.BaseComments.AnyAsync(c => c.CommentId == id),
            ReportedEntityType.BlogPost => await writeDb.BlogPosts.AnyAsync(b => b.BlogPostId == (int)id),
            ReportedEntityType.Recommendation =>
                await writeDb.Recommendations.AnyAsync(r => r.RecommendationId == (int)id),
            ReportedEntityType.Message => await writeDb.PrivateMessages.AnyAsync(m => m.MessageId == id),
            _ => false,
        };
        if (!exists) throw NotFound();

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // Takedown exemption: if the row is gone from the read context purely because IsTakenDown is
        // set, the report is still legitimate. Probing with that one filter lifted separates
        // "removed by a moderator" from every other reason the target might be hidden.
        bool takenDownOnly = type switch
        {
            ReportedEntityType.Story => await readDb.Stories
                .IgnoreQueryFilters(["IsTakenDown"]).AnyAsync(s => s.StoryId == (int)id && s.IsTakenDown),
            ReportedEntityType.Comment => await readDb.BaseComments
                .IgnoreQueryFilters(["IsTakenDown"]).AnyAsync(c => c.CommentId == id && c.IsTakenDown),
            ReportedEntityType.BlogPost => await readDb.BlogPosts
                .IgnoreQueryFilters(["IsTakenDown"]).AnyAsync(b => b.BlogPostId == (int)id && b.IsTakenDown),
            ReportedEntityType.Recommendation => await readDb.Recommendations
                .IgnoreQueryFilters(["IsTakenDown"]).AnyAsync(r => r.RecommendationId == (int)id && r.IsTakenDown),
            _ => false,
        };
        if (takenDownOnly) return;

        bool visible = type switch
        {
            ReportedEntityType.Story =>
                await StoryVisibilityGuard.IsStoryVisibleAsync(readDb, activeUser, (int)id),
            ReportedEntityType.BlogPost =>
                await BlogPostVisibilityGuard.IsBlogPostVisibleAsync(readDb, activeUser, (int)id),
            ReportedEntityType.User =>
                await ProfileVisibilityGuard.IsProfileVisibleAsync(readDb, activeUser, (int)id),
            ReportedEntityType.Comment => await readDb.BaseComments.AnyAsync(c => c.CommentId == id),
            ReportedEntityType.Recommendation =>
                await readDb.Recommendations.AnyAsync(r => r.RecommendationId == (int)id),
            // Messages are participant-scoped, not content-visibility-scoped; existence above is the
            // check, and a non-participant cannot learn a message id through any read path.
            ReportedEntityType.Message => true,
            _ => false,
        };
        if (!visible) throw NotFound();
    }

    private async Task AdjustActiveReportCountAsync(ReportedEntityType type, long id, int delta)
    {
        switch (type)
        {
            case ReportedEntityType.Story:
                await writeDb.Stories
                    .Where(s => s.StoryId == (int)id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActiveReportCount, x => x.ActiveReportCount + delta));
                break;
            case ReportedEntityType.User:
                await writeDb.Users
                    .Where(u => u.Id == (int)id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActiveReportCount, x => x.ActiveReportCount + delta));
                break;
            case ReportedEntityType.Comment:
                await writeDb.BaseComments
                    .Where(c => c.CommentId == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActiveReportCount, x => x.ActiveReportCount + delta));
                break;
            case ReportedEntityType.BlogPost:
                await writeDb.BlogPosts
                    .Where(b => b.BlogPostId == (int)id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActiveReportCount, x => x.ActiveReportCount + delta));
                break;
            case ReportedEntityType.Recommendation:
                await writeDb.Recommendations
                    .Where(r => r.RecommendationId == (int)id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActiveReportCount, x => x.ActiveReportCount + delta));
                break;
            case ReportedEntityType.Message:
                // PrivateMessage has no ActiveReportCount column; skip per settled WU34 convention.
                break;
        }
    }

    /// <summary>
    /// Soft-removes the target by setting <c>IsTakenDown = true</c> and recording removal metadata
    /// via <see cref="IModeratableContent"/>. Returns the content author's user id, or <c>null</c>
    /// when the entity doesn't exist or the moderator's rating filter excludes it.
    /// Messages have no takedown columns — falls through to <see cref="ApplyHardDeleteAsync"/>.
    /// User removal is handled via <see cref="ApplyAccountActionAsync"/> (account status change).
    /// </summary>
    private async Task<int?> ApplyRemovalAsync(ReportedEntityType type, long id, string reason)
    {
        if (type == ReportedEntityType.Message)
            return await ApplyHardDeleteAsync(type, id);

        if (type == ReportedEntityType.User)
            return null; // User removal is handled via ApplyAccountActionAsync

        IModeratableContent? entity = await LoadModeratableAsync(type, id);
        if (entity is null) return null;

        entity.IsTakenDown = true;
        entity.TakedownDate = DateTime.UtcNow;
        entity.TakedownReason = reason;
        return entity.AuthorUserId;
    }

    /// <summary>
    /// Hard-deletes the target entity (illegal content path — CSAM, piracy). Returns the author's
    /// user id before deletion when possible, or <c>null</c>.
    /// </summary>
    private async Task<int?> ApplyHardDeleteAsync(ReportedEntityType type, long id)
    {
        if (type is ReportedEntityType.Story or ReportedEntityType.Comment
            or ReportedEntityType.BlogPost or ReportedEntityType.Recommendation)
        {
            IModeratableContent? entity = await LoadModeratableAsync(type, id);
            if (entity is null) return null;
            int? authorId = entity.AuthorUserId;
            writeDb.Remove((object)entity);
            return authorId;
        }

        if (type == ReportedEntityType.Message)
        {
            var msg = await writeDb.PrivateMessages
                .SingleOrDefaultAsync(m => m.MessageId == id);
            if (msg is null) return null;
            writeDb.PrivateMessages.Remove(msg);
            return null;
        }

        throw new InvalidOperationException($"Hard delete is not supported for entity type '{type}'.");
    }
}
