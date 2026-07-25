using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IExternalVerificationReadService"/> /
/// <see cref="IExternalVerificationWriteService"/> (Feature 53, WU39). Thin pass-throughs: no
/// business logic here — validation and the mod/admin gate live in the service
/// (<c>ServerExternalVerificationWriteService.RequireModerator</c>, the enforcement point of
/// record). Every write handler wraps in <see cref="EndpointHelpers.ExecuteWriteAsync"/> for
/// exception→status translation, mirroring <c>ModerationEndpoints</c>.
///
/// <para><b>Read auth.</b> The author-facing reads (platforms, my-accounts) are
/// <c>RequireAuthorization()</c>-only — any signed-in user's own data. The two moderator-queue
/// reads carry the named <see cref="AuthorizationPolicies.RequireModerator"/> policy here — like
/// <c>ModerationEndpoints</c>' <c>/reports</c> and <c>/submissions</c>, the service performs no
/// role check of its own for these reads (today gated only at the page level), so the endpoint is
/// the actual security boundary (identity-and-authorization.md).</para>
///
/// <para><b>Write auth.</b> Every mod-only write carries the same edge policy on top of the
/// service's own <c>RequireModerator()</c> gate — defense in depth (MA-702 pattern). The service
/// gate remains authoritative: a signed-in non-mod who somehow reaches it gets
/// <see cref="UnauthorizedAccessException"/> → 403; unauthenticated throws
/// <see cref="InvalidOperationException"/> → 401 via <c>ExecuteWriteAsync</c>'s auth-safety-net
/// case (same known EndpointHelpers 401-vs-400 business-rule mismatch as <c>ModerationEndpoints</c>
/// — e.g. "Verify your X account first" also maps to 401, not 400).</para>
/// </summary>
public static class ExternalVerificationEndpoints
{
    public static WebApplication MapExternalVerificationEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/external-verification");

        // ── Reads — author's own data ─────────────────────────────────────────────

        group.MapGet("/platforms", async (IExternalVerificationReadService svc) =>
                Results.Ok(await svc.GetVerificationPlatformsAsync()))
            .RequireAuthorization();

        group.MapGet("/my-accounts", async (IExternalVerificationReadService svc) =>
                Results.Ok(await svc.GetMyExternalAccountsAsync()))
            .RequireAuthorization();

        // ── Reads — moderator queues (mod-only; service performs no role check itself) ──

        group.MapGet("/pending-accounts", async (IExternalVerificationReadService svc) =>
                Results.Ok(await svc.GetPendingAccountVerificationsAsync()))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        group.MapGet("/pending-links", async (IExternalVerificationReadService svc) =>
                Results.Ok(await svc.GetPendingLinkVerificationsAsync()))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        // ── Writes — author, account tier ─────────────────────────────────────────

        group.MapPost("/my-code", (IExternalVerificationWriteService svc) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await svc.EnsureMyVerificationCodeAsync())))
            .RequireAuthorization();

        // AddExternalAccountRequest is a request object → POST-with-body per layer5-wasm.md's
        // non-scalar-parameter rule.
        group.MapPost("/accounts", (IExternalVerificationWriteService svc, AddExternalAccountRequest request) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await svc.SubmitAccountForVerificationAsync(request);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        // ── Writes — author, per-link tier ────────────────────────────────────────

        group.MapPost("/links/{storyExternalLinkId:int}/request",
                (IExternalVerificationWriteService svc, int storyExternalLinkId) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await svc.RequestLinkVerificationAsync(storyExternalLinkId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        // ── Writes — moderator, account tier ──────────────────────────────────────

        group.MapPost("/accounts/{userExternalIdentityId:int}/approve",
                (IExternalVerificationWriteService svc, int userExternalIdentityId) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await svc.ApproveAccountVerificationAsync(userExternalIdentityId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        group.MapPost("/accounts/{userExternalIdentityId:int}/reject",
                (IExternalVerificationWriteService svc, int userExternalIdentityId, string reason) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await svc.RejectAccountVerificationAsync(userExternalIdentityId, reason);
                        return Results.NoContent();
                    }))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        // ── Writes — moderator, per-link tier ─────────────────────────────────────

        group.MapPost("/links/{storyExternalLinkId:int}/approve",
                (IExternalVerificationWriteService svc, int storyExternalLinkId) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await svc.ApproveLinkVerificationAsync(storyExternalLinkId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        group.MapPost("/links/{storyExternalLinkId:int}/reject",
                (IExternalVerificationWriteService svc, int storyExternalLinkId, string reason) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await svc.RejectLinkVerificationAsync(storyExternalLinkId, reason);
                        return Results.NoContent();
                    }))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        return app;
    }
}
