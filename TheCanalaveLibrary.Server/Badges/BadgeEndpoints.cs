using Microsoft.AspNetCore.Mvc;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IBadgeReadService"/> / <see cref="IBadgeWriteService"/>
/// (Feature 50, WU36). Thin pass-throughs — no business logic here.
/// <para>
/// <b>Caller-vs-target is enforced here, at the HTTP boundary, not in the service.</b>
/// <c>ServerBadgeReadService</c>/<c>ServerBadgeWriteService</c> take an explicit <c>userId</c> and
/// trust it — that's correct in-process, because <c>ServerRecommendationWriteService</c> legitimately
/// calls <c>AwardAsync</c> server-internally to award the *other* user in a recommendation chain
/// (never the caller of whatever request triggered it). A blanket caller==target check inside the
/// service would break that path. Instead, every mapped route derives <c>userId</c> from
/// <see cref="IActiveUserContext"/> and never accepts it as a client-supplied parameter — same
/// pattern as <c>UserSettingsEndpoints</c> ("no userId parameter ever crosses HTTP"). This closes the
/// IDOR that let any authenticated caller pass an arbitrary <c>userId</c> to read another user's
/// hidden-badge curation view or overwrite another user's <c>DisplayOrder</c> (MA-601, fixed
/// 2026-07-17).
/// </para>
/// <para>
/// <b><c>AwardAsync</c> is deliberately unmapped.</b> Awards are earned, never user-initiated: the
/// only production caller is <c>ServerRecommendationWriteService</c>, server-internal, after its own
/// success-count check. Mapping an award route — even self-only — would let any authenticated WASM
/// caller mint any catalogue badge for themselves (Patron, Architect, …), a privilege-escalation
/// surface, not just dead code. Same decision and rationale as <c>NotificationEndpoints</c>'
/// unmapped generation methods; <c>ClientBadgeWriteService.AwardAsync</c> throws
/// <see cref="NotSupportedException"/> accordingly (MA-601 hardening, 2026-07-17).
/// </para>
/// <para>
/// <b>Status-code seam resolved (MA-611, 2026-07-18).</b> <c>SetDisplayOrderAsync</c>'s unowned-key
/// rejection (a requested display key the caller hasn't earned) is a business rule, not an auth
/// failure — it now throws <see cref="BadgeValidationException"/> (a <c>CanalaveValidationException</c>)
/// → <b>400</b> via the shared <see cref="EndpointHelpers.ExecuteWriteAsync"/>, the accurate status.
/// Only the <c>RequireUserId</c> auth-safety-net guard's <see cref="InvalidOperationException"/> still
/// maps to 401. <see cref="ClientBadgeWriteService"/> reconstructs <see cref="BadgeValidationException"/>
/// from the 400 body.
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
        // userId is the caller's own id (IActiveUserContext), never a client-supplied parameter.
        group.MapGet("/", (IBadgeReadService badges, IActiveUserContext activeUser) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await badges.GetMyBadgesForCurationAsync(RequireUserId(activeUser)))))
            .RequireAuthorization();

        // No /award route — AwardAsync is server-internal generation; see class doc.

        // Curation reorder. [FromBody] pins the bare List<string> to the JSON body — minimal APIs
        // otherwise bind a bare List<T>/T[] parameter from the query string (SeriesEndpoints'
        // "/order" route hits the same trap). No wrapper DTO minted (layer5-wasm.md "Avoid").
        // userId is the caller's own id, never client-supplied (MA-601).
        group.MapPut("/display-order",
                (IBadgeWriteService badges, IActiveUserContext activeUser, [FromBody] List<string> orderedVisibleKeys) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await badges.SetDisplayOrderAsync(RequireUserId(activeUser), orderedVisibleKeys);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        return app;
    }

    // Auth safety net, same idiom as ServerChapterWriteService.RequireAuthenticatedUser — in
    // practice RequireAuthorization() above already guarantees this, so the InvalidOperationException
    // path (mapped to 401 by EndpointHelpers.ExecuteWriteAsync) is a defense-in-depth backstop.
    private static int RequireUserId(IActiveUserContext activeUser) =>
        activeUser.UserId ?? throw new InvalidOperationException("This operation requires an authenticated user.");
}
