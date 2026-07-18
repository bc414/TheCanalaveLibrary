using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IRecommendationWriteService"/>. Inherits the read impl (CQRS-lite),
/// mirroring ServerRecommendationWriteService : ServerRecommendationReadService. Auth rides the
/// same-origin Identity cookie — WASM's fetch-backed HttpClient sends it automatically for
/// same-origin requests.
/// <para>
/// Translates RecommendationEndpoints' status codes back into the service contract's typed
/// exceptions so components behave identically on either side of the interface — the shared
/// MA-008 shape (<see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/>); 400 reconstructs
/// <see cref="RecommendationValidationException"/> and 429 reconstructs
/// <see cref="WriteRateLimitExceededException"/> with <see cref="WriteActionKind.ContentCreate"/>
/// (<c>SubmitAsync</c>'s only throttled write). The shared shape's 401 →
/// <see cref="InvalidOperationException"/> arm carries <c>ProblemDetails.Detail</c> through — see
/// RecommendationEndpoints' class summary on that mapping also covering the Hidden-Gem/spotlight
/// reject-at-limit business rules alongside genuine "not signed in".
/// </para>
/// </summary>
public sealed class ClientRecommendationWriteService(HttpClient http)
    : ClientRecommendationReadService(http), IRecommendationWriteService
{
    public async Task<int> SubmitAsync(RecommendationSubmitDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/recommendations", dto);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task EditAsync(UpdateRecommendationDto dto)
    {
        HttpResponseMessage response =
            await Http.PutAsJsonAsync($"api/recommendations/{dto.RecommendationId}", dto);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task DeleteAsync(int recommendationId)
    {
        HttpResponseMessage response = await Http.DeleteAsync($"api/recommendations/{recommendationId}");
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task<RecommendationLikeResultDto> ToggleLikeAsync(int recommendationId)
    {
        HttpResponseMessage response =
            await Http.PostAsync($"api/recommendations/{recommendationId}/like", content: null);
        await ThrowIfWriteFailedAsync(response);
        return (await response.Content.ReadFromJsonAsync<RecommendationLikeResultDto>())!;
    }

    public async Task SetHiddenGemAsync(int recommendationId, bool isHiddenGem)
    {
        HttpResponseMessage response = await Http.PutAsync(
            $"api/recommendations/{recommendationId}/hidden-gem?isHiddenGem={(isHiddenGem ? "true" : "false")}",
            content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task SetHighlightedByAuthorAsync(int recommendationId, bool isHighlighted)
    {
        HttpResponseMessage response = await Http.PutAsync(
            $"api/recommendations/{recommendationId}/spotlight?isHighlighted={(isHighlighted ? "true" : "false")}",
            content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task RecordSuccessAsync(int recommendationId)
    {
        HttpResponseMessage response =
            await Http.PostAsync($"api/recommendations/{recommendationId}/success", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task RecordAttributionSourceAsync(int storyId, int recommendationId)
    {
        HttpResponseMessage response = await Http.PostAsync(
            $"api/recommendations/attribution/{storyId}/{recommendationId}", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    /// <summary>Status-code → contract-exception translation (inverse of RecommendationEndpoints') —
    /// the shared MA-008 shape, including the 429 write-throttle reconstruction.</summary>
    private static Task ThrowIfWriteFailedAsync(HttpResponseMessage response) =>
        ClientHttpHelpers.ThrowIfWriteFailedAsync(response,
            msg => new RecommendationValidationException([msg]), WriteActionKind.ContentCreate);
}
