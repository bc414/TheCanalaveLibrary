using System.Net;
using System.Text.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// Shared plumbing for every <c>Client{Feature}WriteService</c>'s status→exception translation
/// (layer5-wasm.md §"The Error-Translation Contract"). <see cref="ThrowIfWriteFailedAsync"/> is
/// the single copy of the standard status→exception shape — exception <em>construction</em> stays
/// per-feature (catch sites and <c>ExceptionPresenter</c> handle the family through the shared
/// <c>CanalaveValidationException</c> base — MA-008 — but the concrete type reconstructed is still
/// feature-named), so callers pass a factory. Detail-extraction off the response body is likewise
/// centralized here instead of each client class re-declaring its own private
/// <c>ProblemPayload</c> record.
/// </summary>
internal static class ClientHttpHelpers
{
    /// <summary>The fallback error line when a 400 carries neither <c>errors</c> nor a
    /// <c>detail</c>.</summary>
    private const string GenericValidationMessage = "The request failed validation.";

    /// <summary>Reads <c>ProblemDetails.Detail</c> off a 4xx response body. Null if absent —
    /// including a body-less response (e.g. the cookie handler's bare 401), which the strict
    /// <c>ReadFromJsonAsync</c> would reject as malformed JSON.</summary>
    public static async Task<string?> ReadProblemDetailAsync(HttpResponseMessage response) =>
        (await ReadNullableFromJsonAsync<ProblemPayload>(response.Content))?.Detail;

    /// <summary>
    /// The synchronous twin of <see cref="ReadProblemDetailAsync"/>, for the one client method whose
    /// interface signature is synchronous (<c>ClientContentImportService.Resplit</c>). It shares
    /// <see cref="ProblemPayload"/> and the web JSON options with the async path, so the two cannot
    /// drift (the hand-rolled sync copy read with case-sensitive defaults and never bound
    /// <c>detail</c> — WU-ParityAndRemaining, 2026-10-13).
    /// </summary>
    public static string? ReadProblemDetail(HttpResponseMessage response) =>
        ReadNullableFromJson<ProblemPayload>(response.Content)?.Detail;

    /// <summary>
    /// The standard write-response translation — the inverse of the server's
    /// <c>EndpointHelpers.ExecuteAsync</c>. 400 → <paramref name="validationFactory"/> over the body's
    /// <c>errors</c> extension (the server-side <c>CanalaveValidationException.Errors</c> list, line for
    /// line — WU-ParityAndRemaining, 2026-10-13), or <c>[detail]</c> when the body has no usable
    /// <c>errors</c> array (a non-validation 400 such as <c>ArgumentException</c>, or a shape this
    /// client doesn't know — see <see cref="ReadValidationErrorsAsync"/>); 401 →
    /// <see cref="SessionExpiredException"/> (the cookie handler's bare 401 and the server's
    /// "…requires an authenticated user" arm both land here — a session signal, not an
    /// authorization denial; error-handling.md §"The API error envelope"); 403 →
    /// <see cref="UnauthorizedAccessException"/>; 404 → <see cref="KeyNotFoundException"/>; 429 →
    /// <see cref="WriteRateLimitExceededException"/>, rebuilt from the body's
    /// <c>writeActionKind</c>/<c>retryAfterSeconds</c> extensions for every write behind the service
    /// throttle (security.md §"Write Throttling"), and <c>EnsureSuccessStatusCode</c> for any other
    /// 429 (an edge limiter's plain-text body); anything else (an unhandled 5xx carrying the
    /// <c>ApiExceptionHandler</c> envelope) → <see cref="ServerFaultException"/> with the server's
    /// <c>traceId</c>, falling back to <c>EnsureSuccessStatusCode</c> for a genuinely
    /// non-<c>ProblemDetails</c> response (e.g. a network-layer failure with no body at all).
    /// Services whose endpoint mapping deviates from this shape (e.g. Messaging's 403 →
    /// MessagingPermissionException, Groups' content-rating 403 disambiguation) keep their own
    /// private translation for that one arm and delegate everything else here.
    /// </summary>
    /// <param name="response">The response to translate; a success status returns without reading it.</param>
    /// <param name="validationFactory">Builds the feature's validation exception from the error
    /// lines (never empty). List-ctor families pass them through (<c>errs => new
    /// XValidationException([.. errs])</c>); single-message types join them
    /// (<c>errs => new TagValidationException(string.Join(" ", errs))</c>).</param>
    public static async Task ThrowIfWriteFailedAsync(
        HttpResponseMessage response,
        Func<IReadOnlyList<string>, Exception> validationFactory)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                throw validationFactory(await ReadValidationErrorsAsync(response));
            case HttpStatusCode.Unauthorized:
                throw new SessionExpiredException();
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException("You don't have permission to perform this action.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException("The requested content was not found.");
            case HttpStatusCode.TooManyRequests:
                await ThrowRateLimitedAsync(response);
                return;
            default:
                await ThrowServerFaultOrEnsureSuccessAsync(response);
                return;
        }
    }

    /// <summary>
    /// The read-side twin of <see cref="ThrowIfWriteFailedAsync"/> — same table minus the
    /// 429/validation-factory arms, for read services whose server counterpart can throw but has
    /// no per-feature validation type of its own (a read endpoint's 400 comes from a shared/base
    /// exception, e.g. <c>ArgumentException</c>, so there is no feature-specific factory to call —
    /// the <see cref="ProblemPayload.Detail"/> is surfaced as a plain
    /// <see cref="InvalidOperationException"/> for the caller's catch site to translate via
    /// <c>ExceptionPresenter</c>). Added at WU-ErrorHandling2 (2026-07-30) — see layer5-wasm.md
    /// §"The Error-Translation Contract".
    /// </summary>
    public static async Task ThrowIfReadFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                string? detail = await ReadProblemDetailAsync(response);
                throw new InvalidOperationException(detail ?? "The request failed validation.");
            case HttpStatusCode.Unauthorized:
                throw new SessionExpiredException();
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException("You don't have permission to perform this action.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException("The requested content was not found.");
            default:
                await ThrowServerFaultOrEnsureSuccessAsync(response);
                return;
        }
    }

    /// <summary>
    /// The validation 400's error lines: the body's <c>errors</c> extension when it is a non-empty
    /// array of strings, else <c>[detail]</c>, else a generic line — never empty. The read is
    /// tolerant: <c>errors</c> arrives as a raw <see cref="JsonElement"/>, so any other shape (the
    /// framework's <c>ValidationProblemDetails</c> field → messages dictionary, a number, an array
    /// holding a non-string) falls back to the detail instead of throwing a <see cref="JsonException"/>,
    /// and so does a body that is not JSON at all.
    /// </summary>
    private static async Task<IReadOnlyList<string>> ReadValidationErrorsAsync(HttpResponseMessage response)
    {
        ProblemPayload? problem = await ReadProblemTolerantlyAsync(response);
        if (problem?.Errors is { ValueKind: JsonValueKind.Array } errors
            && errors.GetArrayLength() > 0
            && errors.EnumerateArray().All(e => e.ValueKind == JsonValueKind.String))
        {
            return errors.EnumerateArray().Select(e => e.GetString()!).ToList();
        }
        return [problem?.Detail ?? GenericValidationMessage];
    }

    /// <summary>Shared 5xx fall-through for both translators — reconstructs
    /// <see cref="ServerFaultException"/> off the <c>ApiExceptionHandler</c> envelope's
    /// <c>traceId</c> extension when present; otherwise falls back to
    /// <c>EnsureSuccessStatusCode</c> (a genuinely bodyless failure, e.g. a network error).</summary>
    private static async Task ThrowServerFaultOrEnsureSuccessAsync(HttpResponseMessage response)
    {
        ProblemPayload? problem = await ReadNullableFromJsonAsync<ProblemPayload>(response.Content);
        if (problem is null)
        {
            response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
            return;
        }
        throw new ServerFaultException(problem.TraceId);
    }

    /// <summary>
    /// GET + deserialize for endpoints whose service contract returns a NULLABLE value
    /// (<c>Task&lt;T?&gt;</c>). ASP.NET's <c>HttpResultsHelper</c> writes an EMPTY 200 body for a
    /// null result value — under <c>Results.Ok(null)</c> AND <c>Results.Json(null)</c> alike — and
    /// <c>GetFromJsonAsync</c> throws <c>JsonException: ExpectedJsonTokens</c> on an empty body
    /// (found live in the Global Flip browser wave: every StoryPage render for a viewer with no
    /// read history crashed on <c>GetViewerLastInteractionUtcAsync</c>). This maps empty → null and
    /// otherwise deserializes with the same web defaults <c>GetFromJsonAsync</c> uses. Use it for
    /// every nullable-returning read; keep plain <c>GetFromJsonAsync</c> for non-nullable ones.
    /// </summary>
    public static async Task<T?> GetNullableFromJsonAsync<T>(this HttpClient http, string url)
    {
        using HttpResponseMessage response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await ReadNullableFromJsonAsync<T>(response.Content);
    }

    /// <summary>Empty-body-tolerant twin of <c>ReadFromJsonAsync</c> — see
    /// <see cref="GetNullableFromJsonAsync{T}"/> for why this exists. Also correct for 204s and for
    /// write responses whose contract returns a nullable (e.g. <c>ITagWriteService.UpdateTagAsync</c>'s
    /// <c>string?</c>).</summary>
    public static async Task<T?> ReadNullableFromJsonAsync<T>(HttpContent content) =>
        DeserializeNullable<T>(await content.ReadAsStringAsync());

    /// <summary>
    /// The synchronous twin of <see cref="ReadNullableFromJsonAsync{T}"/> (empty body → default, web
    /// JSON options), for the one synchronous client method (<c>ClientContentImportService.Resplit</c>).
    /// Every client JSON read — sync or async — goes through <see cref="DeserializeNullable{T}"/>, so
    /// none can fall back to the case-sensitive default options (layer5-wasm.md §"Client Service
    /// Implementations").
    /// </summary>
    public static T? ReadNullableFromJson<T>(HttpContent content)
    {
        using Stream body = content.ReadAsStream();
        using StreamReader reader = new(body);
        return DeserializeNullable<T>(reader.ReadToEnd());
    }

    private static T? DeserializeNullable<T>(string raw) =>
        string.IsNullOrWhiteSpace(raw) || raw == "null"
            ? default
            : JsonSerializer.Deserialize<T>(raw, JsonSerializerOptions.Web);

    /// <summary>
    /// The 429 arm (WU-ThrottleCoverage, 2026-10-13). The service throttle's 429 is a
    /// <c>ProblemDetails</c> whose <c>writeActionKind</c>/<c>retryAfterSeconds</c> extensions
    /// (<c>EndpointHelpers.ExecuteAsync</c>) rebuild <see cref="WriteRateLimitExceededException"/>
    /// with no help from the calling client class — before this, only callers that named their kind
    /// got the "wait N seconds" message and every other throttled write fell through to
    /// <see cref="ServerFaultException"/>. Any other 429 — an edge limiter's plain-text body
    /// (<c>TagWrites</c>, <c>ImportParse</c>, <c>ExportGenerate</c>), or JSON naming no known kind —
    /// becomes <c>EnsureSuccessStatusCode</c>'s <see cref="HttpRequestException"/> (429). The body is
    /// read tolerantly: the strict reader would turn the edge limiters' plain text into a
    /// <see cref="JsonException"/>.
    /// </summary>
    private static async Task ThrowRateLimitedAsync(HttpResponseMessage response)
    {
        ProblemPayload? problem = await ReadProblemTolerantlyAsync(response);
        if (Enum.TryParse(problem?.WriteActionKind, ignoreCase: false, out WriteActionKind kind)
            && Enum.IsDefined(kind))
        {
            throw new WriteRateLimitExceededException(
                kind, TimeSpan.FromSeconds(problem!.RetryAfterSeconds ?? 60));
        }

        response.EnsureSuccessStatusCode(); // throws HttpRequestException carrying the 429
    }

    /// <summary>
    /// <see cref="ReadNullableFromJsonAsync{T}"/> for a body that may not be JSON at all. A
    /// malformed body is translated to "no payload" (null), and the caller then surfaces the status
    /// itself, so nothing is swallowed: the failure still reaches the user as the status's exception.
    /// </summary>
    private static async Task<ProblemPayload?> ReadProblemTolerantlyAsync(HttpResponseMessage response)
    {
        try
        {
            return await ReadNullableFromJsonAsync<ProblemPayload>(response.Content);
        }
        catch (JsonException)
        {
            return null; // translated: the caller throws for the status (see summary)
        }
    }

    /// <summary>Just the ProblemDetails fields consumed client-side — MVC's type isn't referenced
    /// in WASM. <c>TraceId</c> is the <c>ApiExceptionHandler</c> envelope's extension
    /// (WU-ErrorHandling2, 2026-07-30), present only on the unhandled-5xx path;
    /// <c>RetryAfterSeconds</c> and <c>WriteActionKind</c> are the service throttle's 429
    /// extensions (<c>EndpointHelpers</c>); <c>Errors</c> is the validation 400's error list
    /// (WU-ParityAndRemaining), kept as a raw <see cref="JsonElement"/> so an unexpected shape never
    /// fails the read (<see cref="ReadValidationErrorsAsync"/>).</summary>
    private sealed record ProblemPayload(
        string? Detail, double? RetryAfterSeconds, string? TraceId, string? WriteActionKind,
        JsonElement? Errors);
}
