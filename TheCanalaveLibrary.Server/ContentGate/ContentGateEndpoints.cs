using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Consent endpoints for the mature-content gate (Feature 66, WU-AccessGate; content-safety.md
/// §"The Three-Plane Access Model"). All three are FULL-DOCUMENT flows (plain form POST /
/// forceLoad GET → redirect) by architectural necessity, not preference: a circuit's viewer
/// context is frozen after first read (ServerActiveUserContext caches per scope), and the auth
/// cookie / prefs cookie can only be (re)issued on a real HTTP response — so every consent
/// action ends in a redirect that rebuilds the world with the new state. Forms carry
/// <c>&lt;AntiforgeryToken/&gt;</c> (Logout-form precedent); form-binding minimal APIs validate
/// it automatically. <c>LocalRedirect</c> supplies the open-redirect guard.
/// </summary>
public static class ContentGateEndpoints
{
    public static WebApplication MapContentGateEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/content-gate");

        // "View this story" / "View this group" / view-this-post — durable per-item consent.
        // Signed-in → UserContentReveal row (cross-device, revocable in /settings);
        // anonymous → prefs-cookie reveal list (180d sliding, LRU-capped).
        group.MapPost("/reveal", async (
            HttpContext http,
            IActiveUserContext activeUser,
            ApplicationDbContext writeDb,
            [FromForm] RevealedEntityType entityType,
            [FromForm] int entityId,
            [FromForm] string returnUrl) =>
        {
            if (activeUser.UserId is int userId)
            {
                bool exists = await writeDb.UserContentReveals.FindAsync(userId, entityType, entityId) is not null;
                if (!exists)
                {
                    writeDb.UserContentReveals.Add(new UserContentReveal
                    {
                        UserId = userId,
                        EntityType = entityType,
                        EntityId = entityId,
                        DateRevealed = DateTime.UtcNow,
                    });
                    await writeDb.SaveChangesAsync();
                }
            }
            else
            {
                AnonPrefs prefs = AnonPrefs.Read(http.Request);
                prefs.AddReveal(entityType, entityId);
                prefs.Append(http.Response);
            }

            return TypedResults.LocalRedirect(Sanitize(returnUrl));
        });

        // "Always show mature content" (and the settings toggle's transport): flips the global
        // Discovery-plane setting. Signed-in → DB write + RefreshSignInAsync so the claim is
        // reissued IMMEDIATELY (closes MA-605 — no more works-at-next-login); anonymous → the
        // prefs-cookie mature flag.
        group.MapPost("/mature", async (
            HttpContext http,
            IActiveUserContext activeUser,
            [FromServices] UserManager<User> userManager,
            [FromServices] SignInManager<User> signInManager,
            [FromForm] bool enable,
            [FromForm] string returnUrl) =>
        {
            if (activeUser.IsAuthenticated)
            {
                User? user = await userManager.GetUserAsync(http.User);
                if (user is not null)
                {
                    user.ShowMatureContent = enable;
                    await userManager.UpdateAsync(user);
                    await signInManager.RefreshSignInAsync(user);
                }
            }
            else
            {
                AnonPrefs prefs = AnonPrefs.Read(http.Request);
                prefs.Mature = enable;
                prefs.Append(http.Response);
            }

            return TypedResults.LocalRedirect(Sanitize(returnUrl));
        });

        // Claim refresh only (no state change here — the interactive settings form has already
        // saved through IUserSettingsService): reissues the auth cookie from current DB state so
        // a ShowMatureContent change takes effect without re-login, then returns. Idempotent;
        // safe on GET (comparable to a sliding session refresh). Used by PrivacySettingsForm via
        // forceLoad navigation after a save that changed the mature flag.
        group.MapGet("/refresh-claims", async (
                HttpContext http,
                [FromServices] UserManager<User> userManager,
                [FromServices] SignInManager<User> signInManager,
                string returnUrl) =>
            {
                User? user = await userManager.GetUserAsync(http.User);
                if (user is not null)
                    await signInManager.RefreshSignInAsync(user);
                return TypedResults.LocalRedirect(Sanitize(returnUrl));
            })
            .RequireAuthorization();

        // ── Reveal management (the /settings "Mature content you've revealed" section) ──
        // JSON API (not form/redirect) — consumed by the interactive settings page; removing a
        // reveal doesn't change the CURRENT page's data, so no full-document trip is needed.

        RouteGroupBuilder api = app.MapGroup("/api/content-gate").RequireAuthorization();

        api.MapGet("/reveals", async (IContentRevealService reveals) =>
            Results.Ok(await reveals.GetMyRevealsAsync()));

        api.MapDelete("/reveals/{entityType:int}/{entityId:int}", (
                IContentRevealService reveals, int entityType, int entityId) =>
            EndpointHelpers.ExecuteWriteAsync(async () =>
            {
                await reveals.RemoveAsync((RevealedEntityType)entityType, entityId);
                return Results.NoContent();
            }));

        return app;
    }

    /// <summary>
    /// LocalRedirect already rejects absolute/protocol-relative URLs; this additionally maps
    /// empty input to home so a missing field degrades gracefully instead of 500ing.
    /// </summary>
    private static string Sanitize(string? returnUrl) =>
        string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/') || returnUrl.StartsWith("//")
            ? "/"
            : returnUrl;
}
