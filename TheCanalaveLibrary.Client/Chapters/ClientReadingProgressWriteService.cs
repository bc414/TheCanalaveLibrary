using System.Globalization;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IReadingProgressWriteService"/>. Buffered-signal ping (layer5-wasm.md
/// §"API Endpoint Organization" "Buffered-signal ping endpoints"): fire-and-forget, no typed
/// exception translation — the server body throws nothing (in-process buffer merge only), so a
/// non-success status surfaces as a bare <see cref="HttpRequestException"/> via
/// <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>, mirroring the "fast and dumb" shape.
/// </summary>
public sealed class ClientReadingProgressWriteService(HttpClient http) : IReadingProgressWriteService
{
    public async Task RecordProgressAsync(int chapterId, float progress)
    {
        // Invariant-culture formatting for the float — browser locale must not affect the decimal
        // separator ASP.NET Core's query-string binder expects.
        string progressText = progress.ToString(CultureInfo.InvariantCulture);
        HttpResponseMessage response = await http.PostAsync(
            $"api/reading-progress?chapterId={chapterId}&progress={progressText}", content: null);
        response.EnsureSuccessStatusCode();
    }
}
