using System.Net.Http.Json;
using System.Text.Json;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// Shared plumbing for every <c>Client{Feature}WriteService</c>'s status→exception translation
/// (layer5-wasm.md §"The Error-Translation Contract"). Exception <em>construction</em> stays
/// per-feature — the ~13 <c>{Feature}ValidationException</c> types don't share a constructor shape
/// (some take <c>string message</c>, others <c>List&lt;string&gt;</c>/<c>IReadOnlyList&lt;string&gt;
/// errors</c>) — but detail-extraction off the response body is identical everywhere, so it's
/// centralized here instead of each client class re-declaring its own private
/// <c>ProblemPayload</c> record.
/// </summary>
internal static class ClientHttpHelpers
{
    /// <summary>Reads <c>ProblemDetails.Detail</c> off a 4xx response body. Null if absent.</summary>
    public static async Task<string?> ReadProblemDetailAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemPayload>())?.Detail;

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
