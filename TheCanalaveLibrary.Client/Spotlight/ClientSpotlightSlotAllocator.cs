using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ISpotlightSlotAllocator"/>: HttpClient wrapper over
/// SpotlightSlotAllocatorEndpoints (Global Flip — <c>ModSpotlightPage</c> injects the allocator
/// directly). Status→exception translation mirrors the endpoint's: 400 →
/// <see cref="SpotlightValidationException"/> (message from <c>ProblemDetails.Detail</c>),
/// 401/403 → <see cref="UnauthorizedAccessException"/>, 404 → <see cref="KeyNotFoundException"/>.
/// </summary>
public sealed class ClientSpotlightSlotAllocator(HttpClient http) : ISpotlightSlotAllocator
{
    public async Task<int> GrantSlotAsync(int toUserId, SpotlightSlotSource source)
    {
        HttpResponseMessage response = await http.PostAsync(
            $"api/spotlight-slots?toUserId={toUserId}&source={(int)source}", content: null);
        await ThrowIfFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task RevokeSlotAsync(int slotId)
    {
        HttpResponseMessage response = await http.DeleteAsync($"api/spotlight-slots/{slotId}");
        await ThrowIfFailedAsync(response);
    }

    public async Task<int> GetRemainingMonthlyGrantCapacityAsync() =>
        await http.GetFromJsonAsync<int>("api/spotlight-slots/remaining-capacity");

    public async Task<IReadOnlyList<SpotlightSlotAdminDto>> GetRecentGrantsAsync(int take = 50) =>
        await http.GetFromJsonAsync<IReadOnlyList<SpotlightSlotAdminDto>>(
            $"api/spotlight-slots/recent-grants?take={take}") ?? [];

    private static async Task ThrowIfFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                string? detail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                throw new SpotlightValidationException([detail ?? "The slot operation failed validation."]);
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException("This operation requires a moderator.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException("Slot not found.");
            default:
                response.EnsureSuccessStatusCode();
                return;
        }
    }
}
