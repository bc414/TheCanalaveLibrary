using Microsoft.AspNetCore.Mvc;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IBadgeReadService"/> / <see cref="IBadgeWriteService"/>
/// (Feature 50, WU36). Thin pass-throughs — no business logic here.
/// <para>
/// <b>Auth posture is a floor, not a full mirror of caller intent.</b> Unlike Tags,
/// <c>ServerBadgeReadService</c>/<c>ServerBadgeWriteService</c> carry <em>no</em> ownership or
/// mod/admin check of their own — every method takes an explicit <c>userId</c> and trusts it.
/// In-process, that's safe: the only two callers are <c>SettingsPage.razor</c> (passes
/// <c>ActiveUser.UserId</c>, the caller's own id) and <c>ServerRecommendationWriteService</c>
/// (server-internal, awards to the *other* user in a recommendation chain — never the caller).
/// Neither shape is expressible as a service-level check the way <c>RequireMod()</c> is for Tags,
/// so <c>RequireAuthorization()</c> is applied here as the floor per this sweep's explicit
/// instruction, same as <c>ReadingProgressEndpoints</c>/<c>ChapterReadMarkEndpoints</c>. Once these
/// routes are actually reachable (WASM flip), a caller could pass an arbitrary <c>userId</c> to
/// read another user's hidden-badge curation view, self-award any catalogue badge via
/// <c>/award</c>, or overwrite another user's <c>DisplayOrder</c> via <c>/display-order</c> — none
/// of that is caught today because the service is the single enforcement point and the service
/// doesn't enforce it. Flagged for the eventual browser debug wave rather than resolved here
/// (mechanical add-without-verify pass, layer5-wasm.md "Rollout Strategy").
/// </para>
/// <para>
/// <b><see cref="InvalidOperationException"/> double meaning.</b> The shared
/// <see cref="EndpointHelpers.ExecuteWriteAsync"/> table maps every <c>InvalidOperationException</c>
/// to 401 under the "...requires an authenticated user" auth-safety-net assumption
/// (layer5-wasm.md "The Error-Translation Contract"). <c>SetDisplayOrderAsync</c> throws the same
/// exception type for an unrelated reason — a requested key the caller hasn't earned — so that
/// case surfaces as 401 instead of the more accurate 400. The message text still round-trips
/// verbatim via <c>ProblemDetails.Detail</c> (<see cref="ClientBadgeWriteService"/> reads it rather
/// than hardcoding the auth string), so no information is lost, but the status code is misleading.
/// Not resolved here — resolving it means either giving <c>IBadgeWriteService</c> a distinct
/// exception type for this case (Layer 2 change) or special-casing it in the shared helper (affects
/// every other cluster's mapping), both out of scope for an add-only Layer-5 sweep.
/// </para>
/// </summary>
public static class BadgeEndpoints
{
    public static WebApplication MapBadgeEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/badges");

        // Not public: GetMyBadgesForCurationAsync returns ALL earned badges, including hidden ones
        // (DisplayOrder == 0) — a self-curation view, not the public-profile subset (that's a
        // separate projection: UserCardDto.Badges via ServerUserProfileReadService et al., which
        // already filters to DisplayOrder > 0 and isn't touched by this interface at all).
        group.MapGet("/", async (IBadgeReadService badges, int userId) =>
                Results.Ok(await badges.GetMyBadgesForCurationAsync(userId)))
            .RequireAuthorization();

        // Grant. See class doc — any authenticated caller can award any badge to any userId today;
        // the service performs no caller-vs-target check.
        group.MapPost("/award", (IBadgeWriteService badges, int userId, string badgeKey) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await badges.AwardAsync(userId, badgeKey))))
            .RequireAuthorization();

        // Curation reorder. [FromBody] pins the bare List<string> to the JSON body — minimal APIs
        // otherwise bind a bare List<T>/T[] parameter from the query string (SeriesEndpoints'
        // "/order" route hits the same trap). No wrapper DTO minted (layer5-wasm.md "Avoid");
        // userId binds from the query string alongside the JSON-body list.
        group.MapPut("/display-order",
                (IBadgeWriteService badges, int userId, [FromBody] List<string> orderedVisibleKeys) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await badges.SetDisplayOrderAsync(userId, orderedVisibleKeys);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        return app;
    }
}
