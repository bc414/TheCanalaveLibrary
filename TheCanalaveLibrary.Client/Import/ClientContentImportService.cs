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
/// <b><c>ResplitAsync</c> is an HTTP round-trip like the other three methods</b> (see
/// <c>ContentImportEndpoints</c>' doc comment for why it cannot run client-side: it re-sanitizes every
/// draft, and the sanitizer is server-only). Until the WU-ParityAndRemaining browser pass (2026-10-13)
/// the interface method was synchronous and this class blocked on the request with
/// <c>SendAsync(...).GetAwaiter().GetResult()</c>, on the belief that the WASM HTTP handler falls back
/// to a synchronous request. It does not: the single-threaded WASM runtime refuses the wait with
/// <see cref="PlatformNotSupportedException"/> ("Cannot wait on monitors on this runtime"), and every
/// re-split crashed the story editor. A client method never blocks on a task (layer5-wasm.md
/// §"Client Service Implementations").
/// </para>
/// <para>
/// Every JSON read goes through <c>ClientHttpHelpers</c> (web JSON options). The re-split's earlier
/// hand-rolled read deserialized with the case-sensitive default options, so the server's camelCase
/// <c>title</c>/<c>html</c> never bound and every draft came back blank (service audit §2.7.4;
/// WU-ParityAndRemaining).
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
    /// Layer-5 body-swap, via the transport-only <see cref="ResplitRequest"/> envelope.
    /// </summary>
    public async Task<IReadOnlyList<ImportedChapterDraft>> ResplitAsync(ImportParseResult parsed, SplitStrategy strategy)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync(
            "api/content-import/resplit", new ResplitRequest(parsed, strategy));
        await ThrowIfResplitFailedAsync(response);
        return await ClientHttpHelpers.ReadNullableFromJsonAsync<List<ImportedChapterDraft>>(response.Content) ?? [];
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
    /// The re-split's own status translation. It differs from <see cref="ThrowIfFailedAsync"/> in one
    /// arm: 401/403 map to <see cref="InvalidOperationException"/>, not
    /// <see cref="SessionExpiredException"/>/<see cref="UnauthorizedAccessException"/>.
    /// <para>
    /// <c>ResplitAsync</c>'s own business-rule guard ("re-split requires a parsed document") is thrown
    /// as <see cref="InvalidOperationException"/> server-side and gets uniformly (mis)mapped to 401 by
    /// <c>EndpointHelpers.ExecuteAsync</c>'s documented, out-of-scope mismatch (see
    /// <c>ContentImportEndpoints</c>' doc comment) — this preserves that exception type and the real
    /// business message instead of the standard table's generic session-expired text, which would be
    /// actively wrong for this specific mis-mapped case.
    /// </para>
    /// </summary>
    private static async Task ThrowIfResplitFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                throw new ImportException(
                    await ClientHttpHelpers.ReadProblemDetailAsync(response) ?? "This document couldn't be re-split.");
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new InvalidOperationException(
                    await ClientHttpHelpers.ReadProblemDetailAsync(response)
                    ?? "This operation requires an authenticated user.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }
}
