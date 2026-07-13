using Microsoft.AspNetCore.Http;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IUserSettingsService"/> (Features 20 + 3, self-referential
/// read+write — spec's sanctioned CQRS-lite exception, see the interface's own doc comment and
/// <c>layer2-services.md</c> §"Self-Referential Editing Exception"). One endpoints class for the
/// whole interface — there is no read/write split to mirror. Every method resolves the current user
/// from the cookie server-side (<c>IActiveUserContext</c> inside the service); no <c>userId</c>
/// parameter ever crosses HTTP.
/// <para>
/// Auth: <c>RequireAuthorization()</c> on every route, defense-in-depth alongside the service's own
/// <c>RequireCurrentUserId</c> guard (throws <see cref="InvalidOperationException"/>, already mapped
/// to 401 by the shared <see cref="EndpointHelpers.ExecuteWriteAsync"/>). Every write — and the read,
/// wrapped in <see cref="EndpointHelpers.ExecuteWriteAsync"/> solely for that exception→status
/// translation, same as <c>UserStoryInteractionEndpoints.GetBookshelfStoryIdsAsync</c> — goes through
/// the shared helper.
/// </para>
/// <para>
/// <b>Known EndpointHelpers mismatch (flagged, not fixed here — out of scope for this add-only
/// pass):</b> <c>UpdateAuthorSettingsAsync</c>'s pinned-story ownership/visibility guard also throws
/// <see cref="InvalidOperationException"/> for a genuine business-rule reason, not because the caller
/// is unauthenticated — <see cref="EndpointHelpers.ExecuteWriteAsync"/> still maps it to 401
/// uniformly. The message survives via <c>ProblemDetails.Detail</c>, but the status itself is
/// semantically off (400 would be more accurate). Same shape as the flagged mismatch in
/// <c>FollowingEndpoints</c>.
/// </para>
/// <para>
/// <c>UploadProfilePictureAsync</c> is the multipart case (layer5-wasm.md §"Streams and multipart"),
/// mirroring <c>StoryEndpoints</c>' <c>UploadCoverArtAsync</c> pattern: <c>IFormFile</c> bound via
/// minimal-API form binding, <c>DisableAntiforgery()</c> because this is a stateless
/// cookie-authenticated API call, not a Razor form post (Program.cs's global
/// <c>UseAntiforgery()</c> would otherwise require a token).
/// </para>
/// </summary>
public static class UserSettingsEndpoints
{
    public static WebApplication MapUserSettingsEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/user-settings");

        // ── Read ──

        group.MapGet("/", (IUserSettingsService settings) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await settings.GetMySettingsAsync())))
            .RequireAuthorization();

        // ── Writes — JSON sub-forms ──

        group.MapPut("/profile", (IUserSettingsService settings, UpdateProfileDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await settings.UpdateProfileAsync(dto);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapPut("/reader", (IUserSettingsService settings, ReaderSettingsDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await settings.UpdateReaderSettingsAsync(dto);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapPut("/privacy", (IUserSettingsService settings, PrivacySettingsDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await settings.UpdatePrivacySettingsAsync(dto);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapPut("/author", (IUserSettingsService settings, AuthorSettingsDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await settings.UpdateAuthorSettingsAsync(dto);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        // Three hot scalar columns, not a JSON group — GET-bindable scalars query-bind even on a
        // PUT (same pattern as FollowingEndpoints' SetReceiveAlertsAsync ?receiveAlerts=...).
        group.MapPut("/appearance", (
                IUserSettingsService settings, int themeId, bool prefersAnimated, bool prefersDataSaver) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await settings.UpdateAppearanceAsync(themeId, prefersAnimated, prefersDataSaver);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        // ── Write — multipart (layer5-wasm.md §"Streams and multipart") ──
        // DisableAntiforgery(): this is a stateless API call authenticated by the same-origin
        // Identity cookie, not a Razor form post, so the global UseAntiforgery() middleware's
        // token requirement is correctly bypassed here (mirrors StoryEndpoints' cover-art upload).
        group.MapPost("/profile-picture", (IUserSettingsService settings, IFormFile file) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await settings.UploadProfilePictureAsync(
                        file.OpenReadStream(), file.ContentType))))
            .RequireAuthorization()
            .DisableAntiforgery();

        return app;
    }
}
