using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IUserProfileReadService"/> (Feature 21 — public profile
/// display). Read-only, no matching write service — see <see cref="IUserSettingsService"/> /
/// <c>UserSettingsEndpoints</c> for the self-edit surface. Public: profile display is a public page,
/// so neither route carries <c>RequireAuthorization()</c>.
/// <para>
/// <c>includePrivate</c> crosses the HTTP boundary as a plain query-string bool, exactly as
/// <c>UserStoryInteractionEndpoints.GetFavoriteStoryIdsAsync</c> already does for the same kind of
/// own-vs-other predicate — the caller (component/dispatcher) is trusted to pass
/// <c>viewerId == profileUserId</c> rather than the endpoint re-deriving it from
/// <c>IActiveUserContext</c> server-side. This mirrors existing precedent for this mechanical,
/// add-only sweep, not a new judgment call.
/// </para>
/// </summary>
public static class UserProfileEndpoints
{
    public static WebApplication MapUserProfileEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/user-profiles");

        // Nullable return is not an error condition (missing user / hidden-by-privacy are both a
        // contractual null per the interface doc) — 200 with a JSON null body, not a 404 Problem.
        group.MapGet("/{userId:int}", async (IUserProfileReadService profiles, int userId, bool includePrivate) =>
            Results.Json(await profiles.GetProfileHeaderAsync(userId, includePrivate)));

        group.MapGet("/{userId:int}/bio", async (IUserProfileReadService profiles, int userId) =>
            Results.Json(await profiles.GetProfileTextAsync(userId)));

        return app;
    }
}
