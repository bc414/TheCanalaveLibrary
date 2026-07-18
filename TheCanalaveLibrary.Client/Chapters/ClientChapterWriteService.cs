using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IChapterWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerChapterWriteService : ServerChapterReadService. Auth rides the same-origin Identity
/// cookie — WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Translates ChapterEndpoints' status codes back into the service contract's typed exceptions —
/// the shared MA-008 shape (<see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/>); 400
/// reconstructs <see cref="ChapterValidationException"/>.
/// </para>
/// </summary>
public sealed class ClientChapterWriteService(HttpClient http)
    : ClientChapterReadService(http), IChapterWriteService
{
    public async Task<int> CreateChapterAsync(CreateChapterDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/chapters", dto);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task<long> AddAlternateVersionAsync(int chapterId, CreateChapterDto dto)
    {
        HttpResponseMessage response =
            await Http.PostAsJsonAsync($"api/chapters/{chapterId}/versions", dto);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<long>();
    }

    public async Task UpdateChapterContentAsync(UpdateChapterContentDto dto)
    {
        HttpResponseMessage response =
            await Http.PutAsJsonAsync($"api/chapters/content/{dto.ChapterContentId}", dto);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task SetPrimaryVersionAsync(int chapterId, long chapterContentId)
    {
        HttpResponseMessage response = await Http.PutAsync(
            $"api/chapters/{chapterId}/primary/{chapterContentId}", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task SetPublishedAsync(int chapterId, bool isPublished)
    {
        HttpResponseMessage response = await Http.PutAsync(
            $"api/chapters/{chapterId}/published?isPublished={isPublished}", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task MoveChapterAsync(int storyId, int fromNumber, int toNumber)
    {
        HttpResponseMessage response = await Http.PutAsync(
            $"api/chapters/{storyId}/move?fromNumber={fromNumber}&toNumber={toNumber}", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task DeleteChapterAsync(int chapterId)
    {
        HttpResponseMessage response = await Http.DeleteAsync($"api/chapters/{chapterId}");
        await ThrowIfWriteFailedAsync(response);
    }

    /// <summary>Status-code → contract-exception translation (inverse of ChapterEndpoints') — the
    /// shared MA-008 shape.</summary>
    private static Task ThrowIfWriteFailedAsync(HttpResponseMessage response) =>
        ClientHttpHelpers.ThrowIfWriteFailedAsync(response, msg => new ChapterValidationException([msg]));
}
