using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IChapterWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerChapterWriteService : ServerChapterReadService. Auth rides the same-origin Identity
/// cookie — WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Translates ChapterEndpoints' status codes back into the service contract's typed exceptions:
/// 400 → <see cref="ChapterValidationException"/> (message from ProblemDetails.Detail — the
/// endpoint's shared error-translation joins the service's error list into one string via
/// <c>ex.Message</c>, so the client reconstructs a single-element list; exact per-item errors
/// don't round-trip, matching every other <c>{Feature}ValidationException</c> client translation
/// in this codebase), 401/403 → <see cref="UnauthorizedAccessException"/>,
/// 404 → <see cref="KeyNotFoundException"/>.
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

    /// <summary>Status-code → contract-exception translation (inverse of ChapterEndpoints').</summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                string? detail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                throw new ChapterValidationException([detail ?? "The chapter failed validation."]);
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException("This operation requires the story's author.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException("Chapter not found.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }
}
