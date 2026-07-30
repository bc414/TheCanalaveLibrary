using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IAccountStatusReadService"/> (WU-AccountEnforcement).
/// Self-referential by design, same trust shape as <see cref="UserActivityEndpoints"/> and
/// <c>IUserSettingsService</c>: no id in the route or body, the caller's own status only,
/// resolved server-side from <see cref="IActiveUserContext.UserId"/>. Deliberately a plain JSON
/// GET, not a form-POST/redirect like <c>ContentGateEndpoints</c> — the value is display-only, so
/// there is nothing to reissue in the cookie and no need for a document round-trip.
/// </summary>
public static class AccountStatusEndpoints
{
    public static WebApplication MapAccountStatusEndpoints(this WebApplication app)
    {
        app.MapGet("/api/account-status", async (IAccountStatusReadService accountStatus) =>
                Results.Ok(await accountStatus.GetMyAccountStatusAsync()))
            .RequireAuthorization();

        return app;
    }
}
