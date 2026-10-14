using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ISavedTagSelectionReadService"/>: HttpClient wrapper over
/// SavedTagSelectionEndpoints (Server/Tags/SavedTagSelectionEndpoints.cs). Same DTOs, same method
/// contracts — only the transport differs (the Layer-5 body-swap). Of the four reads, the two
/// owner reads (<see cref="GetMySelectionsAsync"/>, <see cref="GetSelectionDetailAsync"/>) require the
/// same-origin Identity cookie server-side (RequireAuthorization()), which WASM's fetch-backed
/// HttpClient sends automatically. The two public reads (<c>/public/{userId}</c> and
/// <c>/permalink/{id}</c>) are anonymous-callable; the service enforces their visibility gates.
/// </summary>
public class ClientSavedTagSelectionReadService(HttpClient http) : ISavedTagSelectionReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<List<SavedTagSelectionSummaryDto>> GetMySelectionsAsync(SavedTagSelectionSortEnum sort) =>
        await Http.GetFromJsonAsync<List<SavedTagSelectionSummaryDto>>(
            $"api/saved-tag-selections?sort={(short)sort}") ?? [];

    public async Task<SavedTagSelectionDetailDto?> GetSelectionDetailAsync(int id) =>
        await Http.GetNullableFromJsonAsync<SavedTagSelectionDetailDto?>($"api/saved-tag-selections/{id}");

    public async Task<List<SavedTagSelectionDetailDto>> GetPublicSelectionsByUserAsync(int userId) =>
        await Http.GetFromJsonAsync<List<SavedTagSelectionDetailDto>>(
            $"api/saved-tag-selections/public/{userId}") ?? [];

    public async Task<SavedTagSelectionDetailDto?> GetPublicSelectionByIdAsync(int id) =>
        await Http.GetNullableFromJsonAsync<SavedTagSelectionDetailDto?>(
            $"api/saved-tag-selections/permalink/{id}");
}
