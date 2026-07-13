using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
/// <c>fetch()</c> path, so it completes rather than deadlocking on the single UI thread — it still
/// blocks that thread for the round-trip's duration, a real, flagged cost (see this sweep's final
/// report), not swept under the rug. See <c>ContentImportEndpoints</c>' doc comment for why
/// <c>Resplit</c> could not instead be resolved with zero network round-trip (it depends on the
/// server-only <c>IHtmlSanitizationService</c>).
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
    /// Synchronous by interface contract; see the class doc comment for how that's satisfied without
    /// a block-on-async anti-pattern.
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

        using Stream body = response.Content.ReadAsStream();
        return JsonSerializer.Deserialize<List<ImportedChapterDraft>>(body)!;
    }

    private static MultipartFormDataContent BuildFileForm(Stream file, string fileName)
    {
        MultipartFormDataContent form = new();
        StreamContent streamContent = new(file);
        form.Add(streamContent, "file", fileName);
        return form;
    }

    /// <summary>Status-code → contract-exception translation (inverse of ContentImportEndpoints').</summary>
    private static async Task ThrowIfFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                string? detail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                throw new ImportException(detail ?? "This file couldn't be imported.");
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException("Importing content requires an authenticated user.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }

    /// <summary>
    /// Synchronous counterpart to <see cref="ThrowIfFailedAsync"/>, used only by <see cref="Resplit"/>
    /// (see class doc comment for why that method can't await). Reads the response body via the
    /// synchronous <see cref="JsonSerializer"/> overload instead of
    /// <c>ClientHttpHelpers.ReadProblemDetailAsync</c> (async-only shared plumbing) — duplicating the
    /// one-field <c>ProblemPayload</c> shape locally rather than adding a sync variant to the shared
    /// helper for this single caller.
    /// <para>
    /// Maps 401/403 to <see cref="InvalidOperationException"/>, not
    /// <see cref="UnauthorizedAccessException"/> — <c>Resplit</c>'s own business-rule guard ("re-split
    /// requires a parsed document") is thrown as <see cref="InvalidOperationException"/> server-side
    /// and gets uniformly (mis)mapped to 401 by <c>EndpointHelpers.ExecuteWriteAsync</c>'s documented,
    /// out-of-scope mismatch (see <c>ContentImportEndpoints</c>' doc comment) — this preserves that
    /// exception type and the message, same as <c>ClientUserSettingsService.ThrowIfFailedAsync</c>'s
    /// analogous case, instead of collapsing it into a generic "you're not logged in" exception.
    /// </para>
    /// </summary>
    private static void ThrowIfFailedSync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                throw new ImportException(ReadDetailSync(response) ?? "This document couldn't be re-split.");
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new InvalidOperationException(
                    ReadDetailSync(response) ?? "This operation requires an authenticated user.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }

    private static string? ReadDetailSync(HttpResponseMessage response)
    {
        using Stream body = response.Content.ReadAsStream();
        return JsonSerializer.Deserialize<ProblemPayload>(body)?.Detail;
    }

    /// <summary>Sync-path twin of ClientHttpHelpers' private ProblemPayload — see ReadDetailSync.</summary>
    private sealed record ProblemPayload(string? Detail);
}
