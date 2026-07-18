using System.Net;
using System.Net.Http.Json;
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
    /// <summary>Reads <c>ProblemDetails.Detail</c> off a 4xx response body. Null if absent —
    /// including a body-less response (e.g. the cookie handler's bare 401), which the strict
    /// <c>ReadFromJsonAsync</c> would reject as malformed JSON.</summary>
    public static async Task<string?> ReadProblemDetailAsync(HttpResponseMessage response) =>
        (await ReadNullableFromJsonAsync<ProblemPayload>(response.Content))?.Detail;

    /// <summary>
    /// The standard write-response translation — the inverse of the server's
    /// <c>EndpointHelpers.ExecuteWriteAsync</c>. 400 → <paramref name="validationFactory"/> over
    /// <c>ProblemDetails.Detail</c> (the server-side exception's user-facing message; the server
    /// joins each feature's error list into one string via <c>ex.Message</c>, so list-shaped
    /// exceptions reconstruct as a single-element list — per-item errors don't round-trip); 401 →
    /// <see cref="InvalidOperationException"/> carrying the detail through (the server maps the
    /// services' "…requires an authenticated user" InvalidOperationException to 401); 403 →
    /// <see cref="UnauthorizedAccessException"/>; 404 → <see cref="KeyNotFoundException"/>; 429 →
    /// <see cref="WriteRateLimitExceededException"/> reconstruction when
    /// <paramref name="rateLimitedAction"/> is set (write surfaces behind the write throttle —
    /// security.md); anything else → <c>EnsureSuccessStatusCode</c>. Services whose endpoint
    /// mapping deviates from this shape (e.g. Messaging's 403 → MessagingPermissionException,
    /// Groups' content-rating 403 disambiguation) keep their own private translation.
    /// </summary>
    public static async Task ThrowIfWriteFailedAsync(
        HttpResponseMessage response,
        Func<string, Exception> validationFactory,
        WriteActionKind? rateLimitedAction = null)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                string? detail = await ReadProblemDetailAsync(response);
                throw validationFactory(detail ?? "The request failed validation.");
            case HttpStatusCode.Unauthorized:
                string? authDetail = await ReadProblemDetailAsync(response);
                throw new InvalidOperationException(
                    authDetail ?? "This operation requires an authenticated user.");
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException("You don't have permission to perform this action.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException("The requested content was not found.");
            case HttpStatusCode.TooManyRequests when rateLimitedAction is not null:
                double? retryAfterSeconds = await ReadRetryAfterSecondsAsync(response);
                throw new WriteRateLimitExceededException(
                    rateLimitedAction.Value, TimeSpan.FromSeconds(retryAfterSeconds ?? 60));
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
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
    public static async Task<T?> ReadNullableFromJsonAsync<T>(HttpContent content)
    {
        string raw = await content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(raw) || raw == "null"
            ? default
            : JsonSerializer.Deserialize<T>(raw, JsonSerializerOptions.Web);
    }

    /// <summary>
    /// Reads the <c>retryAfterSeconds</c> extension a 429 <c>WriteRateLimitExceededException</c>
    /// carries (see <c>EndpointHelpers.ExecuteWriteAsync</c> on the server). Null if absent.
    /// </summary>
    public static async Task<double?> ReadRetryAfterSecondsAsync(HttpResponseMessage response)
    {
        ProblemPayload? problem = await response.Content.ReadFromJsonAsync<ProblemPayload>();
        return problem?.RetryAfterSeconds;
    }

    /// <summary>Just the ProblemDetails fields consumed client-side — MVC's type isn't referenced in WASM.</summary>
    private sealed record ProblemPayload(string? Detail, double? RetryAfterSeconds);
}
