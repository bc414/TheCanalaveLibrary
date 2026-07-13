using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IChapterReadMarkWriteService"/>. Write-only interface with no matching
/// <c>*ReadService</c> — same "one class, no base/subclass split" shape layer5-wasm.md's
/// §"Client Service Implementations" prescribes for read-only interfaces, mirrored here for the
/// write-only case (nothing to separate a read-only consumer from). Auth rides the same-origin
/// Identity cookie.
/// </summary>
public sealed class ClientChapterReadMarkWriteService(HttpClient http) : IChapterReadMarkWriteService
{
    public async Task SetChapterReadAsync(int chapterId, bool isRead)
    {
        HttpResponseMessage response = await http.PutAsync(
            $"api/chapter-read-marks/{chapterId}?isRead={isRead}", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task SetAllChaptersReadAsync(int storyId, bool isRead)
    {
        HttpResponseMessage response = await http.PutAsync(
            $"api/chapter-read-marks/story/{storyId}?isRead={isRead}", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    /// <summary>
    /// Status-code → contract-exception translation. The interface declares only
    /// <see cref="KeyNotFoundException"/> and <see cref="InvalidOperationException"/> (anonymous
    /// caller) — no <see cref="UnauthorizedAccessException"/> — so both 401 and 403 map to the
    /// latter, matching the interface's actual exception surface.
    /// </summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new InvalidOperationException("This operation requires an authenticated user.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException("Chapter or story not found.");
            default:
                response.EnsureSuccessStatusCode();
                return;
        }
    }
}
