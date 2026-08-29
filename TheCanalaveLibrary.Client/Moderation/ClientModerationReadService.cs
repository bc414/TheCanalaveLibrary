using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IModerationReadService"/>: HttpClient wrapper over
/// ModerationEndpoints (Server/Moderation/ModerationEndpoints.cs). Same DTOs, same method
/// contracts — only the transport differs (the Layer-5 body-swap).
/// </summary>
public class ClientModerationReadService(HttpClient http) : IModerationReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<ReportReasonDto[]> GetReportReasonsAsync() =>
        await Http.GetFromJsonAsync<ReportReasonDto[]>("api/moderation/report-reasons") ?? [];

    public async Task<ReportQueueItemDto[]> GetReportQueueAsync(bool includeResolved = false) =>
        await Http.GetFromJsonAsync<ReportQueueItemDto[]>(
            $"api/moderation/reports?includeResolved={includeResolved}") ?? [];

    public async Task<StorySubmissionQueueItemDto[]> GetPendingSubmissionsAsync() =>
        await Http.GetFromJsonAsync<StorySubmissionQueueItemDto[]>("api/moderation/submissions") ?? [];

    /// <summary>
    /// The endpoint answers 404 for an unknown user id (rather than a null body), so this maps that
    /// one status back to the interface's documented <c>null</c> instead of letting
    /// <c>GetFromJsonAsync</c> throw.
    /// </summary>
    public async Task<UserModerationHistoryDto?> GetUserModerationHistoryAsync(int userId)
    {
        HttpResponseMessage response = await Http.GetAsync($"api/moderation/users/{userId}/history");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserModerationHistoryDto>();
    }
}
