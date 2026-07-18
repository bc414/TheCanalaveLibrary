using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IContentImportService"/> (Feature 63, chapter import) — one
/// endpoints class for the whole interface, no read/write split (single-purpose parsing service,
/// mirroring the interface's own shape, same as <c>UserSettingsEndpoints</c>' self-referential
/// case). Every route is authenticated: importing content is an authoring action, only reachable
/// from the authenticated chapter-import UI.
/// <para>
/// All four handlers wrap in the shared <see cref="EndpointHelpers.ExecuteWriteAsync"/> even though
/// three of the four are reads-that-can-throw — same precedent as
/// <c>UserStoryInteractionEndpoints.GetBookshelfStoryIdsAsync</c> and <c>UserSettingsEndpoints</c>'
/// read: a "read" that still needs exception translation gets the write wrapper.
/// <see cref="ImportException"/> (thrown by all four methods on unreadable/oversized/malformed
/// input) maps to 400 via the shared helper's validation-exception type match — its message is
/// presentation-safe UX copy (see the exception's own doc comment) and survives verbatim through
/// <c>ProblemDetails.Detail</c>.
/// </para>
/// <para>
/// <c>ParseSingleAsync</c>/<c>ParseDocumentAsync</c>/<c>ParseEpubAsync</c> are the multipart cases
/// (layer5-wasm.md §"Streams and multipart"): <c>IFormFile</c> bound via minimal-API form binding,
/// <c>DisableAntiforgery()</c> because these are stateless cookie-authenticated API calls, not Razor
/// form posts (Program.cs's global <c>UseAntiforgery()</c> would otherwise require a token) — mirrors
/// <c>StoryEndpoints</c>' cover-art upload and <c>UserSettingsEndpoints</c>' profile-picture upload.
/// <c>format</c> rides the query string alongside the multipart body (same combination shape as
/// <c>UserSettingsEndpoints</c>' <c>/appearance</c> scalars): minimal-API form binding only claims
/// <c>IFormFile</c>-typed parameters, so the plain <see cref="ImportFormat"/> enum parameter falls
/// back to its normal query-string binding source with no extra attribute needed.
/// </para>
/// <para>
/// <c>Resplit</c> is synchronous and pure in-memory per the interface's own doc comment ("no
/// re-upload") — but it is NOT dependency-free: <c>ServerContentImportService.Resplit</c> calls
/// <c>SplitToDrafts</c> → <c>CreateDraft</c>, which re-sanitizes every draft through the
/// constructor-injected <c>IHtmlSanitizationService</c> (the class's other injected dependency,
/// <c>ILogger</c>, isn't used on this path, but the sanitizer is). Since
/// <c>IHtmlSanitizationService</c> is structurally server-only (layer5-wasm.md §"Avoid" —
/// sanitization must run server-side, never client-implemented), <c>Resplit</c> could NOT have been
/// called directly client-side with zero network round-trip even though its own doc comment reads
/// as if it were pure in-memory recomputation; the standard HTTP endpoint below is the only correct
/// shape, not just the consistent one. It still gets one for the same
/// <see cref="ResplitRequest"/>-envelope reason as any other two-parameter JSON-body write (the
/// method can't bind two complex parameters from one POST body otherwise).
/// </para>
/// <para>
/// <b>Known EndpointHelpers mismatch (flagged, not fixed here — out of scope for this add-only
/// pass):</b> when <see cref="ImportParseResult.NormalizedHtml"/> is null (EPUB results),
/// <c>Resplit</c> throws <see cref="InvalidOperationException"/> for a genuine business-rule reason
/// ("re-split requires a parsed document; EPUB chapters are spine-defined"), not because the caller
/// is unauthenticated — <see cref="EndpointHelpers.ExecuteWriteAsync"/> still maps it to 401
/// uniformly. Same shape as the flagged mismatch in <c>UserSettingsEndpoints</c> and
/// <c>FollowingEndpoints</c>; the message survives via <c>ProblemDetails.Detail</c>.
/// </para>
/// </summary>
public static class ContentImportEndpoints
{
    public static WebApplication MapContentImportEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/content-import");

        // "ImportParse" (BB-02/MA-309, 2026-07-18): a server-wide concurrency limiter on the three
        // file-parse routes — Mammoth/EPUB/AngleSharp work is the app's most expensive
        // attacker-influenced compute and parse-then-discard never reaches the commit-side
        // IWriteRateLimitService token bucket. Registered in Program.cs; selection by COST per
        // security.md "Write & Expensive-Operation Throttling". /resplit stays unthrottled — it
        // re-splits an already-parsed result (bounded by ImportLimits, no file decode).

        group.MapPost("/single", (IContentImportService import, IFormFile file, ImportFormat format) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await import.ParseSingleAsync(
                        file.OpenReadStream(), file.FileName, format))))
            .RequireAuthorization()
            .RequireRateLimiting("ImportParse")
            .DisableAntiforgery();

        group.MapPost("/document", (IContentImportService import, IFormFile file, ImportFormat format) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await import.ParseDocumentAsync(
                        file.OpenReadStream(), file.FileName, format))))
            .RequireAuthorization()
            .RequireRateLimiting("ImportParse")
            .DisableAntiforgery();

        group.MapPost("/epub", (IContentImportService import, IFormFile file) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await import.ParseEpubAsync(file.OpenReadStream()))))
            .RequireAuthorization()
            .RequireRateLimiting("ImportParse")
            .DisableAntiforgery();

        // Resplit itself is synchronous (see class doc comment) — the async lambda exists purely to
        // satisfy ExecuteWriteAsync's Func<Task<IResult>> shape. Any exception Resplit throws still
        // surfaces during the synchronous evaluation inside the awaited call and is caught by the
        // shared helper exactly as if it had been thrown from an awaited async method.
        group.MapPost("/resplit", (IContentImportService import, ResplitRequest request) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    await Task.FromResult(
                        Results.Ok(import.Resplit(request.Parsed, request.Strategy)))))
            .RequireAuthorization();

        return app;
    }
}
