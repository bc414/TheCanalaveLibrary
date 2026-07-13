using System.Net;
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
/// exceptions so components behave identically on either side of the interface: 400 →
/// <see cref="RecommendationValidationException"/> (the server joins validation errors into one
/// message via <c>ProblemDetails.Detail</c>, so the client wraps it back into a single-element list
/// rather than re-splitting it — same pattern as <c>ClientCommentWriteService</c>), 401/403 →
/// <see cref="UnauthorizedAccessException"/> (message read through from <c>ProblemDetails.Detail</c>
/// when present — see RecommendationEndpoints' class summary on the shared
/// <c>InvalidOperationException</c> → 401 mapping also covering the Hidden-Gem/spotlight
/// reject-at-limit business rules alongside genuine "not signed in"), 404 →
/// <see cref="KeyNotFoundException"/>, 429 → <see cref="WriteRateLimitExceededException"/>
/// (<c>SubmitAsync</c>'s only throttled write; kind is always
/// <see cref="WriteActionKind.ContentCreate"/> here).
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

    /// <summary>Status-code → contract-exception translation (inverse of RecommendationEndpoints').</summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                string? detail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                throw new RecommendationValidationException(
                    [detail ?? "The recommendation failed validation."]);
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                string? authDetail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                throw new UnauthorizedAccessException(
                    authDetail ?? "This action requires an authenticated user.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException("Recommendation not found.");
            case HttpStatusCode.TooManyRequests:
                double? retryAfterSeconds = await ClientHttpHelpers.ReadRetryAfterSecondsAsync(response);
                throw new WriteRateLimitExceededException(
                    WriteActionKind.ContentCreate, TimeSpan.FromSeconds(retryAfterSeconds ?? 60));
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }
}
