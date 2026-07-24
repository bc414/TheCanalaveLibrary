using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IUserProfileReadService"/> (Feature 21 — public profile
/// display). Read-only, no matching write service — see <see cref="IUserSettingsService"/> /
/// <c>UserSettingsEndpoints</c> for the self-edit surface. Public: profile display is a public page,
/// so neither route carries <c>RequireAuthorization()</c>.
/// <para>
/// <c>includePrivate</c> is derived server-side as <c>IActiveUserContext.UserId == userId</c> —
/// never accepted as a client-supplied query parameter. The public-vs-private split is a server
/// decision: a client-trusted bool would let any caller (including anonymous) pass
/// <c>includePrivate=true</c> for an arbitrary <paramref name="userId" /> and read that user's
/// private stats/last-seen (MA-602, fixed 2026-07-17).
/// </para>
/// </summary>
public static class UserProfileEndpoints
{
    public static WebApplication MapUserProfileEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/user-profiles");

        // Nullable return is not an error condition (missing user / hidden-by-privacy are both a
        // contractual null per the interface doc) — 200 with a JSON null body, not a 404 Problem.
        group.MapGet("/{userId:int}", async (IUserProfileReadService profiles, IActiveUserContext activeUser, int userId) =>
            Results.Json(await profiles.GetProfileHeaderAsync(userId, includePrivate: activeUser.UserId == userId)));

        group.MapGet("/{userId:int}/bio", async (IUserProfileReadService profiles, int userId) =>
            Results.Json(await profiles.GetProfileTextAsync(userId)));

        // Why-is-it-hidden read for the page's honest empty states (WU-AccessGate Phase 1).
        // Deliberately public: it reveals only the visibility MODE, which the rendered page states
        // in prose anyway ("This profile is private." / sign-in prompt).
        group.MapGet("/{userId:int}/access-state", async (IUserProfileReadService profiles, int userId) =>
            Results.Ok(await profiles.GetProfileAccessStateAsync(userId)));

        return app;
    }
}
