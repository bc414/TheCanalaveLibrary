using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IChapterReadService"/>: HttpClient wrapper over ChapterEndpoints
/// (Server/Chapters/ChapterEndpoints.cs). Same DTOs, same method contracts — only the transport
/// differs (the Layer-5 body-swap).
/// </summary>
public class ClientChapterReadService(HttpClient http) : IChapterReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<GatedMetadataDto?> GetChapterGateAsync(
        int storyId,
        int chapterNumber,
        int? versionOrder = null)
    {
        string query = versionOrder.HasValue ? $"?versionOrder={versionOrder.Value}" : string.Empty;
        return await Http.GetNullableFromJsonAsync<GatedMetadataDto?>(
            $"api/chapters/{storyId}/{chapterNumber}/gate{query}");
    }

    public async Task<ChapterReadingDto?> GetChapterForReadingAsync(
        int storyId,
        int chapterNumber,
        int? versionOrder = null)
    {
        string query = versionOrder.HasValue ? $"?versionOrder={versionOrder.Value}" : string.Empty;
        return await Http.GetNullableFromJsonAsync<ChapterReadingDto?>(
            $"api/chapters/{storyId}/{chapterNumber}{query}");
    }

    public async Task<IReadOnlyList<ChapterTocEntryDto>> GetChapterTocAsync(int storyId) =>
        await Http.GetFromJsonAsync<List<ChapterTocEntryDto>>($"api/chapters/{storyId}/toc") ?? [];

    public async Task<IReadOnlyList<ChapterVersionDto>> GetChapterVersionsAsync(
        int storyId, int chapterNumber) =>
        await Http.GetFromJsonAsync<List<ChapterVersionDto>>(
            $"api/chapters/{storyId}/{chapterNumber}/versions") ?? [];

    public async Task<IReadOnlyList<ChapterListEntryDto>> GetChapterListAsync(int storyId) =>
        await Http.GetFromJsonAsync<List<ChapterListEntryDto>>($"api/chapters/{storyId}/list") ?? [];

    public async Task<DateTime?> GetViewerLastInteractionUtcAsync(int storyId) =>
        await Http.GetNullableFromJsonAsync<DateTime?>($"api/chapters/{storyId}/last-interaction");

    public async Task<ChapterReadingDto?> GetChapterForEditAsync(long chapterContentId)
    {
        // 401 → SessionExpiredException, 403 → UnauthorizedAccessException (the server's author
        // gate), delegated to the shared read translator (WU-ErrorHandling2, 2026-07-30).
        using HttpResponseMessage response = await Http.GetAsync($"api/chapters/edit/{chapterContentId}");
        await ClientHttpHelpers.ThrowIfReadFailedAsync(response);
        return await ClientHttpHelpers.ReadNullableFromJsonAsync<ChapterReadingDto?>(response.Content);
    }

    public async Task<IReadOnlyList<ChapterExportDto>> GetChaptersForExportAsync(int storyId) =>
        await Http.GetFromJsonAsync<List<ChapterExportDto>>($"api/chapters/{storyId}/export") ?? [];
}
