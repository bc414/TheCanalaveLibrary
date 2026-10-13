using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IContentImportService"/>: HttpClient wrapper over ContentImportEndpoints
/// (Server/Import/ContentImportEndpoints.cs). One class implementing the whole interface directly —
/// single-purpose parsing service, no read/write split to mirror (same shape as
/// <c>ClientUserSettingsService</c>'s self-referential case). Auth rides the same-origin Identity
/// cookie — WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// <b><c>Resplit</c> is the one method here that isn't <c>async Task</c></b> — it mirrors the
/// interface's synchronous signature (<c>IReadOnlyList&lt;ImportedChapterDraft&gt; Resplit(...)</c>,
/// unchanged per the Layer-5 body-swap axiom). A synchronous interface member wrapping an inherently
/// asynchronous HTTP call can't use the async-await idiom the other three methods use.
/// <b>Deliberately NOT <see cref="HttpClient.Send(HttpRequestMessage)"/></b> — the .NET 5+
/// genuinely-synchronous send API — because that overload is attributed
/// <c>[UnsupportedOSPlatform("browser")]</c> (confirmed by a CA1416 build warning when first tried
/// here) and throws <see cref="PlatformNotSupportedException"/> under the WASM HTTP handler, which
/// has no synchronous transport. Instead this blocks on the normal async path via
/// <c>SendAsync(...).GetAwaiter().GetResult()</c> — discouraged in general .NET (classic
/// sync-over-async deadlock risk) but the one supported way to get a synchronous HTTP result out of
/// Blazor WASM's <c>HttpClient</c>: the WASM runtime's HTTP handler detects a blocking wait and
/// falls back to a genuinely-synchronous <c>XMLHttpRequest</c> instead of the Promise-based
/// <c>fetch()</c> path, so it completes rather than deadlocking on the single UI thread. It still
/// blocks that thread for the round-trip's duration, a real, accepted cost. See
/// <c>ContentImportEndpoints</c>' doc comment for why <c>Resplit</c> could not instead be resolved
/// with zero network round-trip (it depends on the server-only <c>IHtmlSanitizationService</c>).
/// </para>
/// <para>
/// <b>The sync path reads JSON through <c>ClientHttpHelpers</c>' sync helpers</b>
/// (<c>ReadNullableFromJson</c>, <c>ReadProblemDetail</c>), which share the async helpers' web options
/// and <c>ProblemPayload</c>. Its earlier hand-rolled copy deserialized with the case-sensitive default
/// options, so the server's camelCase <c>title</c>/<c>html</c> never bound and every re-split draft came
/// back blank on WASM (service audit §2.7.4; fixed WU-ParityAndRemaining, 2026-10-13 — layer5-wasm.md
/// §"Client Service Implementations").
/// </para>
/// </summary>
public sealed class ClientContentImportService(HttpClient http) : IContentImportService
{
    private HttpClient Http { get; } = http;

    /// <summary>
    /// Multipart upload (layer5-wasm.md §"Streams and multipart") — builds a
    /// <see cref="MultipartFormDataContent"/> with a <see cref="StreamContent"/> part; the endpoint
    /// reads it back via <c>IFormFile</c>. Mirrors <c>ClientStoryWriteService.UploadCoverArtAsync</c>.
    /// </summary>
    public async Task<ImportedChapterDraft> ParseSingleAsync(Stream file, string fileName, ImportFormat format)
    {
        using MultipartFormDataContent form = BuildFileForm(file, fileName);
        HttpResponseMessage response = await Http.PostAsync(
            $"api/content-import/single?format={format}", form);
        await ThrowIfFailedAsync(response);
        return (await response.Content.ReadFromJsonAsync<ImportedChapterDraft>())!;
    }

    public async Task<ImportParseResult> ParseDocumentAsync(Stream file, string fileName, ImportFormat format)
    {
        using MultipartFormDataContent form = BuildFileForm(file, fileName);
        HttpResponseMessage response = await Http.PostAsync(
            $"api/content-import/document?format={format}", form);
        await ThrowIfFailedAsync(response);
        return (await response.Content.ReadFromJsonAsync<ImportParseResult>())!;
    }

    public async Task<ImportParseResult> ParseEpubAsync(Stream file)
    {
        using MultipartFormDataContent form = new();
        using StreamContent streamContent = new(file);
        form.Add(streamContent, "file", "upload.epub");

        HttpResponseMessage response = await Http.PostAsync("api/content-import/epub", form);
        await ThrowIfFailedAsync(response);
        return (await response.Content.ReadFromJsonAsync<ImportParseResult>())!;
    }

    /// <summary>
    /// Pure in-memory recompute per the interface's own doc comment ("no re-upload" — of the
    /// original file, not of a network call) — still crosses the HTTP boundary like every other
    /// Layer-5 body-swap in this sweep, via the transport-only <see cref="ResplitRequest"/> envelope.
    /// Synchronous by interface contract; see the class doc comment for how that's satisfied.
    /// </summary>
    public IReadOnlyList<ImportedChapterDraft> Resplit(ImportParseResult parsed, SplitStrategy strategy)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "api/content-import/resplit")
        {
            Content = JsonContent.Create(new ResplitRequest(parsed, strategy))
        };
        // SendAsync(...).GetAwaiter().GetResult(), not HttpClient.Send(...) — see class doc comment.
        using HttpResponseMessage response = Http.SendAsync(request).GetAwaiter().GetResult();
        ThrowIfFailedSync(response);

        return ClientHttpHelpers.ReadNullableFromJson<List<ImportedChapterDraft>>(response.Content) ?? [];
    }

    private static MultipartFormDataContent BuildFileForm(Stream file, string fileName)
    {
        MultipartFormDataContent form = new();
        StreamContent streamContent = new(file);
        form.Add(streamContent, "file", fileName);
        return form;
    }

    private static Task ThrowIfFailedAsync(HttpResponseMessage response) =>
        ClientHttpHelpers.ThrowIfWriteFailedAsync(response, errs => new ImportException(string.Join(" ", errs)));

    /// <summary>
    /// Synchronous counterpart to <see cref="ThrowIfFailedAsync"/>, used only by <see cref="Resplit"/>
    /// (see class doc comment for why that method can't await). The status switch is its own, because
    /// the shared translator is async-only; the body is read through the shared sync helper
    /// (<c>ClientHttpHelpers.ReadProblemDetail</c>), so the JSON options and payload shape are the
    /// async path's.
    /// <para>
    /// Maps 401/403 to <see cref="InvalidOperationException"/>, not
    /// <see cref="SessionExpiredException"/>/<see cref="UnauthorizedAccessException"/> —
    /// <c>Resplit</c>'s own business-rule guard ("re-split requires a parsed document") is thrown as
    /// <see cref="InvalidOperationException"/> server-side and gets uniformly (mis)mapped to 401 by
    /// <c>EndpointHelpers.ExecuteAsync</c>'s documented, out-of-scope mismatch (see
    /// <c>ContentImportEndpoints</c>' doc comment) — this preserves that exception type and the real
    /// business message instead of the standard table's generic session-expired text, which would be
    /// actively wrong for this specific mis-mapped case.
    /// </para>
    /// </summary>
    private static void ThrowIfFailedSync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                throw new ImportException(
                    ClientHttpHelpers.ReadProblemDetail(response) ?? "This document couldn't be re-split.");
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new InvalidOperationException(
                    ClientHttpHelpers.ReadProblemDetail(response) ?? "This operation requires an authenticated user.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }
}
