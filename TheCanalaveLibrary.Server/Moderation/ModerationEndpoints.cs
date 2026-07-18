using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IModerationReadService"/> / <see cref="IModerationWriteService"/>
/// (Features 46/47/48). Thin pass-throughs: no business logic here — validation and the mod/admin
/// gate for write actions live in the service (<c>ServerModerationWriteService.RequireModerator</c>,
/// the single enforcement point). Every write handler wraps in the shared
/// <see cref="EndpointHelpers.ExecuteWriteAsync"/> for exception→status translation
/// (layer5-wasm.md §"The Error-Translation Contract").
/// <para>
/// <b>Read auth.</b> <see cref="IModerationReadService.GetReportReasonsAsync"/> is
/// <c>RequireAuthorization()</c>-only (any signed-in user) — it feeds <c>ReportDialog</c>, which is
/// reachable from any authenticated viewer reporting content (StoryCard/UserCard/CommentItem/
/// BlogPostCard/recommendation cards/message threads), not just moderators; per
/// <c>ServerModerationWriteService.SubmitReportAsync</c>'s own comment, "report-form UI is
/// auth-gated." <see cref="IModerationReadService.GetReportQueueAsync"/> and
/// <see cref="IModerationReadService.GetPendingSubmissionsAsync"/> back the mod-only
/// <c>/mod/reports</c> and <c>/mod/submissions</c> pages, but — unlike every write method below —
/// <c>ServerModerationReadService</c> performs <em>no role check of its own</em> for these two reads;
/// today they're gated only at the page level (<c>[Authorize(Roles = "Moderator,Admin")]</c> on
/// <c>ModReportsPage</c>/<c>ModSubmissionsPage</c>). Per identity-and-authorization.md's "Endpoint-
/// level is the actual security boundary — it does not inherit from the page," these two reads carry
/// an inline role requirement here rather than the plain <c>RequireAuthorization()</c> floor: without
/// it, any authenticated non-mod caller could read the report queue / submission queue directly over
/// HTTP the moment a WASM page injects this client. The role requirement uses the named
/// <see cref="AuthorizationPolicies.RequireModerator"/> policy registered in <c>Program.cs</c>
/// (MA-702 fix, 2026-07-18 — replaces the earlier inline <c>AuthorizeAttribute</c> copies).
/// </para>
/// <para>
/// <b>Write auth.</b> Every mod-only write carries the same edge
/// <see cref="AuthorizationPolicies.RequireModerator"/> policy on top of the service's own
/// <c>RequireModerator()</c> gate — defense in depth matching the SpotlightSlotAllocator/
/// SiteDailyStat siblings (MA-702). The service gate remains the enforcement point of record: a
/// signed-in non-mod who somehow reaches the service gets <see cref="UnauthorizedAccessException"/>
/// → 403 (MA-701 fix); the unauthenticated branch throws <see cref="InvalidOperationException"/> →
/// 401 via <see cref="EndpointHelpers.ExecuteWriteAsync"/>'s auth-safety-net case. <b>Known
/// EndpointHelpers mismatch (flagged, deferred):</b> <c>SubmitReportAsync</c>'s target-type
/// allow-set guard, <c>ApplyAccountActionAsync</c>'s "target must be a User" guard, and
/// <c>ApproveStoryAsync</c>/<c>RejectStoryAsync</c>'s "not pending approval" guards all also throw
/// <see cref="InvalidOperationException"/> for genuine business-rule reasons, not because the caller
/// is unauthenticated — but the shared helper maps every <see cref="InvalidOperationException"/> to
/// 401 uniformly. The message still crosses via <c>ProblemDetails.Detail</c>.
/// </para>
/// </summary>
public static class ModerationEndpoints
{

    public static WebApplication MapModerationEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/moderation");

        // ── Reads ──────────────────────────────────────────────────────────────────

        group.MapGet("/report-reasons", async (IModerationReadService moderation) =>
                Results.Ok(await moderation.GetReportReasonsAsync()))
            .RequireAuthorization();

        // Mod-only — see class doc: the service performs no role check for this read.
        // includeResolved has no lambda default (unlike the interface's own default) — the client
        // impl always sends it explicitly, mirroring PollEndpoints' "/" (bool includeArchived, no
        // default) convention elsewhere in this codebase.
        group.MapGet("/reports",
                async (IModerationReadService moderation, bool includeResolved) =>
                    Results.Ok(await moderation.GetReportQueueAsync(includeResolved)))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        // Mod-only — see class doc: the service performs no role check for this read.
        group.MapGet("/submissions", async (IModerationReadService moderation) =>
                Results.Ok(await moderation.GetPendingSubmissionsAsync()))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        // ── Writes ─────────────────────────────────────────────────────────────────

        // Report submission (Feature 46): any authenticated user. SubmitReportRequest is a request
        // object → POST-with-body per layer5-wasm.md's non-scalar-parameter rule.
        group.MapPost("/reports", (IModerationWriteService moderation, SubmitReportRequest request) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await moderation.SubmitReportAsync(request);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        // Moderator queue actions (Feature 47) — edge RequireModerator policy + service gate (MA-702).

        group.MapPost("/reports/{reportId:long}/claim",
                (IModerationWriteService moderation, long reportId) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await moderation.ClaimReportAsync(reportId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        // actionNotes is a nullable string — minimal API treats nullable parameters as optional
        // automatically, no lambda default needed (unlike the non-nullable bools above/below).
        group.MapPost("/reports/{reportId:long}/resolve-no-action",
                (IModerationWriteService moderation, long reportId, string? actionNotes) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
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
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await moderation.ResolveWithRemovalAsync(reportId, removalReason, hardDelete);
                        return Results.NoContent();
                    }))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        // suspendedUntilUtc is a nullable DateTime — automatically optional, no lambda default needed.
        group.MapPost("/reports/{reportId:long}/account-action",
                (IModerationWriteService moderation, long reportId, ModeratorActionType action,
                        string reason, DateTime? suspendedUntilUtc) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await moderation.ApplyAccountActionAsync(reportId, action, reason, suspendedUntilUtc);
                        return Results.NoContent();
                    }))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        // Submission approval (Feature 48) — edge RequireModerator policy + service gate (MA-702).

        group.MapPost("/submissions/{storyId:int}/approve",
                (IModerationWriteService moderation, int storyId) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await moderation.ApproveStoryAsync(storyId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        group.MapPost("/submissions/{storyId:int}/reject",
                (IModerationWriteService moderation, int storyId, string reason) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await moderation.RejectStoryAsync(storyId, reason);
                        return Results.NoContent();
                    }))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        return app;
    }
}
