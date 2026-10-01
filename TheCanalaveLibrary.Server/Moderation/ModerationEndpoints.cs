using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IReportSubmissionService"/>, <see cref="IModerationReadService"/>
/// and <see cref="IModerationWriteService"/> (Features 46/47/48). Thin pass-throughs: no business logic
/// here — validation and the mod/admin gate live in the service (the shared
/// <c>ActiveUser.RequireModerator()</c>, the enforcement point of record). Every handler whose service
/// can throw — write or read — wraps in the shared <see cref="EndpointHelpers.ExecuteAsync"/> for
/// exception→status translation (layer5-wasm.md §"The Error-Translation Contract").
/// <para>
/// <b>Submission auth.</b> <c>/report-reasons</c> and <c>POST /reports</c> inject the member-facing
/// <see cref="IReportSubmissionService"/> (owner ruling D9's split) and are
/// <c>RequireAuthorization()</c>-only: <c>ReportDialog</c> is reachable from any signed-in viewer
/// reporting content, not just moderators.
/// </para>
/// <para>
/// <b>Mod-only auth — defense in depth on reads and writes alike (D9, WU-ModerationIntegrity
/// 2026-09-30).</b> Every queue read and every moderator write carries the named
/// <see cref="AuthorizationPolicies.RequireModerator"/> policy at the edge (MA-702, 2026-07-18), and the
/// service gates each one itself: a signed-in non-mod who reaches it gets
/// <see cref="UnauthorizedAccessException"/> → 403, an anonymous caller
/// <see cref="InvalidOperationException"/> → 401. The service gate is what protects the SSR circuit,
/// where no endpoint exists.
/// </para>
/// <para>
/// <b>Status mapping.</b> Business rules throw <see cref="ModerationValidationException"/> → 400 (the
/// allow-set, a duplicate open report, an already-resolved report, the account-status transition table,
/// approve/reject guards); an unknown report, story or user id <see cref="KeyNotFoundException"/> → 404.
/// The 401-instead-of-404/400 mismatch this class once recorded is fixed at the throw sites; the
/// <see cref="EndpointHelpers"/> table is unchanged (<c>InvalidOperationException → 401</c> stays the
/// auth safety net — layer2-services.md §"Moderation Services" → "Exception translation").
/// </para>
/// </summary>
public static class ModerationEndpoints
{

    public static WebApplication MapModerationEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/moderation");

        // ── Reads ──────────────────────────────────────────────────────────────────

        group.MapGet("/report-reasons", async (IReportSubmissionService submission) =>
                Results.Ok(await submission.GetReportReasonsAsync()))
            .RequireAuthorization();

        // Mod-only reads: edge policy + the service's own RequireModerator() (D9), so each wraps in
        // ExecuteAsync. includeResolved has no lambda default (unlike the interface's own default) — the
        // client impl always sends it explicitly, mirroring PollEndpoints' "/" (bool includeArchived, no
        // default) convention elsewhere in this codebase.
        group.MapGet("/reports",
                (IModerationReadService moderation, bool includeResolved) =>
                    EndpointHelpers.ExecuteAsync(async () =>
                        Results.Ok(await moderation.GetReportQueueAsync(includeResolved))))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        group.MapGet("/submissions", (IModerationReadService moderation) =>
                EndpointHelpers.ExecuteAsync(async () =>
                    Results.Ok(await moderation.GetPendingSubmissionsAsync())))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        // 404 rather than a null body when the user id doesn't exist, so the client impl's
        // ProblemDetails translation turns it into the standard not-found message. Results.Problem,
        // not Results.NotFound(): the bodied-result rule (EndpointHelpers' KeyNotFound arm).
        group.MapGet("/users/{userId:int}/history",
                (IModerationReadService moderation, int userId) =>
                    EndpointHelpers.ExecuteAsync(async () =>
                        await moderation.GetUserModerationHistoryAsync(userId) is { } history
                            ? Results.Ok(history)
                            : Results.Problem(statusCode: StatusCodes.Status404NotFound)))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        // ── Writes ─────────────────────────────────────────────────────────────────

        // Report submission (Feature 46): any authenticated user. SubmitReportRequest is a request
        // object → POST-with-body per layer5-wasm.md's non-scalar-parameter rule.
        group.MapPost("/reports", (IReportSubmissionService submission, SubmitReportRequest request) =>
                EndpointHelpers.ExecuteAsync(async () =>
                {
                    await submission.SubmitReportAsync(request);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        // Moderator queue actions (Feature 47) — edge RequireModerator policy + service gate (MA-702).

        group.MapPost("/reports/{reportId:long}/claim",
                (IModerationWriteService moderation, long reportId) =>
                    EndpointHelpers.ExecuteAsync(async () =>
                    {
                        await moderation.ClaimReportAsync(reportId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        // actionNotes is a nullable string — minimal API treats nullable parameters as optional
        // automatically, no lambda default needed (unlike the non-nullable bools above/below).
        group.MapPost("/reports/{reportId:long}/resolve-no-action",
                (IModerationWriteService moderation, long reportId, string? actionNotes) =>
                    EndpointHelpers.ExecuteAsync(async () =>
                    {
                        await moderation.ResolveNoActionAsync(reportId, actionNotes);
                        return Results.NoContent();
                    }))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        // hardDelete has no lambda default — the client impl always sends it explicitly (see
        // includeResolved comment above).
        group.MapPost("/reports/{reportId:long}/resolve-removal",
                (IModerationWriteService moderation, long reportId, string removalReason,
                        bool hardDelete) =>
                    EndpointHelpers.ExecuteAsync(async () =>
                    {
                        await moderation.ResolveWithRemovalAsync(reportId, removalReason, hardDelete);
                        return Results.NoContent();
                    }))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        // suspendedUntilUtc is a nullable DateTime — automatically optional, no lambda default needed.
        group.MapPost("/reports/{reportId:long}/account-action",
                (IModerationWriteService moderation, long reportId, ModeratorActionType action,
                        string reason, DateTime? suspendedUntilUtc) =>
                    EndpointHelpers.ExecuteAsync(async () =>
                    {
                        await moderation.ApplyAccountActionAsync(reportId, action, reason, suspendedUntilUtc);
                        return Results.NoContent();
                    }))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        // Moderator-initiated account action — no pre-existing report (WU-UserModeration). The
        // service files the audit Report row itself; reasonId is one of the seeded ReportReasons.
        group.MapPost("/users/{userId:int}/account-action",
                (IModerationWriteService moderation, int userId, short reasonId,
                        ModeratorActionType action, string reason, DateTime? suspendedUntilUtc) =>
                    EndpointHelpers.ExecuteAsync(async () =>
                    {
                        await moderation.ApplyAccountActionToUserAsync(
                            userId, reasonId, action, reason, suspendedUntilUtc);
                        return Results.NoContent();
                    }))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        // Reinstate (service §2.1.3, WU-ModerationIntegrity) — the only path back to Active and out of a
        // ban; files its own audit Report row. `reason` rides the query like the other moderator reasons
        // (tracker D9 records that shape for all of them).
        group.MapPost("/users/{userId:int}/reinstate",
                (IModerationWriteService moderation, int userId, string reason) =>
                    EndpointHelpers.ExecuteAsync(async () =>
                    {
                        await moderation.ReinstateUserAsync(userId, reason);
                        return Results.NoContent();
                    }))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        // Story-approval trust revoke/restore (WU-StoryLifecycle, D1) — moderator-initiated, files
        // its own audit Report row like the account action above. `enabled` is non-nullable with no
        // lambda default: the client impl always sends it explicitly.
        group.MapPost("/users/{userId:int}/auto-approve",
                (IModerationWriteService moderation, int userId, bool enabled, short reasonId, string reason) =>
                    EndpointHelpers.ExecuteAsync(async () =>
                    {
                        await moderation.SetCanAutoApproveAsync(userId, enabled, reasonId, reason);
                        return Results.NoContent();
                    }))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        // Submission approval (Feature 48) — edge RequireModerator policy + service gate (MA-702).

        group.MapPost("/submissions/{storyId:int}/approve",
                (IModerationWriteService moderation, int storyId) =>
                    EndpointHelpers.ExecuteAsync(async () =>
                    {
                        await moderation.ApproveStoryAsync(storyId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        group.MapPost("/submissions/{storyId:int}/reject",
                (IModerationWriteService moderation, int storyId, string reason) =>
                    EndpointHelpers.ExecuteAsync(async () =>
                    {
                        await moderation.RejectStoryAsync(storyId, reason);
                        return Results.NoContent();
                    }))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        return app;
    }
}
