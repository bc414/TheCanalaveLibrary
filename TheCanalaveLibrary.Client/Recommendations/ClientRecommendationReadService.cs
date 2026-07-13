using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IRecommendationReadService"/>: HttpClient wrapper over
/// RecommendationEndpoints (Server/Recommendations/RecommendationEndpoints.cs). Same DTOs, same
/// method contracts — only the transport differs (the Layer-5 body-swap).
/// </summary>
public class ClientRecommendationReadService(HttpClient http) : IRecommendationReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<List<RecommendationDto>> GetForStoryAsync(int storyId) =>
        await Http.GetFromJsonAsync<List<RecommendationDto>>($"api/recommendations/story/{storyId}") ?? [];

    public Task<RecommendationDto?> GetByIdAsync(int recommendationId) =>
        Http.GetNullableFromJsonAsync<RecommendationDto?>($"api/recommendations/{recommendationId}");

    public async Task<IReadOnlyList<int>> GetRecommendedStoryIdsAsync() =>
        await Http.GetFromJsonAsync<List<int>>("api/recommendations/mine/recommended-story-ids") ?? [];

    public async Task<IReadOnlyList<int>> GetHiddenGemStoryIdsAsync() =>
        await Http.GetFromJsonAsync<List<int>>("api/recommendations/mine/hidden-gem-story-ids") ?? [];

    public Task<int?> GetHelpfulPromptRecommendationIdAsync(int storyId) =>
        Http.GetNullableFromJsonAsync<int?>($"api/recommendations/helpful-prompt/{storyId}");

    public async Task<IReadOnlyList<int>> GetRecommendedStoryIdsByUserAsync(int userId) =>
        await Http.GetFromJsonAsync<List<int>>($"api/recommendations/by-user/{userId}/story-ids") ?? [];
}
