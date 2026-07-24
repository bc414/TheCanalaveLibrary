using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ISpotlightSlotAllocator"/>: HttpClient wrapper over
/// SpotlightSlotAllocatorEndpoints (Global Flip — <c>ModSpotlightPage</c> injects the allocator
/// directly). Status→exception translation is the shared MA-008 shape
/// (<see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/>); 400 reconstructs
/// <see cref="SpotlightValidationException"/>.
/// </summary>
public sealed class ClientSpotlightSlotAllocator(HttpClient http) : ISpotlightSlotAllocator
{
    public async Task<int> GrantSlotAsync(int toUserId, SpotlightSlotSource source, Rating maxStoryRating = Rating.E)
    {
        HttpResponseMessage response = await http.PostAsync(
            $"api/spotlight-slots?toUserId={toUserId}&source={(int)source}&maxStoryRating={(int)maxStoryRating}",
            content: null);
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

    private static Task ThrowIfFailedAsync(HttpResponseMessage response) =>
        ClientHttpHelpers.ThrowIfWriteFailedAsync(response, msg => new SpotlightValidationException([msg]));
}
