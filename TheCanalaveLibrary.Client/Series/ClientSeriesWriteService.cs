using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ISeriesWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerSeriesWriteService : ServerSeriesReadService. Auth rides the same-origin Identity cookie —
/// WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Translates SeriesEndpoints' status codes back into the service contract's typed exceptions so
/// components behave identically on either side of the interface: 400 →
/// <see cref="SeriesValidationException"/> (message from ProblemDetails.Detail, wrapped as a
/// single-element errors list — the server already joins the original error list into one string
/// before it crosses the HTTP boundary, see EndpointHelpers.ExecuteWriteAsync), 401/403 →
/// <see cref="UnauthorizedAccessException"/>, 404 → <see cref="KeyNotFoundException"/>.
/// </para>
/// </summary>
public sealed class ClientSeriesWriteService(HttpClient http)
    : ClientSeriesReadService(http), ISeriesWriteService
{
    public async Task<int> CreateSeriesAsync(CreateSeriesDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/series", dto);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task UpdateSeriesAsync(UpdateSeriesDto dto)
    {
        HttpResponseMessage response = await Http.PutAsJsonAsync($"api/series/{dto.SeriesId}", dto);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task DeleteSeriesAsync(int seriesId)
    {
        HttpResponseMessage response = await Http.DeleteAsync($"api/series/{seriesId}");
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task AddStoryAsync(int seriesId, int storyId)
    {
        HttpResponseMessage response = await Http.PostAsync($"api/series/{seriesId}/stories/{storyId}", null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task RemoveStoryAsync(int seriesId, int storyId)
    {
        HttpResponseMessage response = await Http.DeleteAsync($"api/series/{seriesId}/stories/{storyId}");
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task ReorderAsync(int seriesId, IReadOnlyList<int> orderedStoryIds)
    {
        HttpResponseMessage response =
            await Http.PutAsJsonAsync($"api/series/{seriesId}/order", orderedStoryIds);
        await ThrowIfWriteFailedAsync(response);
    }

    /// <summary>Status-code → contract-exception translation (inverse of SeriesEndpoints').</summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                string? detail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                throw new SeriesValidationException([detail ?? "The series failed validation."]);
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException("You must be the owner of this series.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException("Series not found.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }
}
