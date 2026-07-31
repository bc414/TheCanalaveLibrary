using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IChapterReadMarkWriteService"/>. Write-only interface with no matching
/// <c>*ReadService</c> — same "one class, no base/subclass split" shape layer5-wasm.md's
/// §"Client Service Implementations" prescribes for read-only interfaces, mirrored here for the
/// write-only case (nothing to separate a read-only consumer from). Auth rides the same-origin
/// Identity cookie. Both methods can only ever 401/404/500 — the service never throws a
/// validation exception — so this delegates entirely to the shared
/// <see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/> (WU-ErrorHandling2, 2026-07-30; the
/// unreachable 400 arm is never exercised). Previously collapsed 401/403 into one
/// <see cref="InvalidOperationException"/>, predating <see cref="SessionExpiredException"/>.
/// </summary>
public sealed class ClientChapterReadMarkWriteService(HttpClient http) : IChapterReadMarkWriteService
{
    public async Task SetChapterReadAsync(int chapterId, bool isRead)
    {
        HttpResponseMessage response = await http.PutAsync(
            $"api/chapter-read-marks/{chapterId}?isRead={isRead}", content: null);
        await ClientHttpHelpers.ThrowIfWriteFailedAsync(
            response, detail => new InvalidOperationException(detail));
    }

    public async Task SetAllChaptersReadAsync(int storyId, bool isRead)
    {
        HttpResponseMessage response = await http.PutAsync(
            $"api/chapter-read-marks/story/{storyId}?isRead={isRead}", content: null);
        await ClientHttpHelpers.ThrowIfWriteFailedAsync(
            response, detail => new InvalidOperationException(detail));
    }
}
