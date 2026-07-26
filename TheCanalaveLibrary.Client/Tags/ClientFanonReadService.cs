using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IFanonReadService"/>: HttpClient wrapper over FanonEndpoints
/// (Server/Tags/FanonEndpoints.cs). Same DTOs, same contracts — the Layer-5 body-swap.
/// </summary>
public class ClientFanonReadService(HttpClient http) : IFanonReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<IReadOnlyList<FanonGroupDto>> GetGroupsAsync(TagTypeEnum axis, string? search, int page, int pageSize)
    {
        string url = $"api/fanon/groups?axis={(short)axis}&page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search)) url += $"&search={Uri.EscapeDataString(search)}";
        return await Http.GetFromJsonAsync<List<FanonGroupDto>>(url) ?? [];
    }

    public async Task<int> GetGroupCountAsync(TagTypeEnum axis, string? search)
    {
        string url = $"api/fanon/groups/count?axis={(short)axis}";
        if (!string.IsNullOrWhiteSpace(search)) url += $"&search={Uri.EscapeDataString(search)}";
        return await Http.GetFromJsonAsync<int>(url);
    }

    public async Task<FanonGroupStoriesDto> GetGroupStoriesAsync(TagTypeEnum axis, int baseTagId, string name) =>
        await Http.GetFromJsonAsync<FanonGroupStoriesDto>(
            $"api/fanon/groups/stories?axis={(short)axis}&baseTagId={baseTagId}&name={Uri.EscapeDataString(name)}")
        ?? new FanonGroupStoriesDto([], 0);

    public async Task<IReadOnlyList<FanonTagDto>> GetEstablishedFanonTagsAsync() =>
        await Http.GetFromJsonAsync<List<FanonTagDto>>("api/fanon/established") ?? [];

    public async Task<TagAdoptionPageDto?> GetMyAdoptionPageAsync(int targetTagId)
    {
        HttpResponseMessage response = await Http.GetAsync($"api/fanon/my-adoptions/{targetTagId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await ClientHttpHelpers.ThrowIfWriteFailedAsync(response, msg => new TagValidationException(msg));
        return await ClientHttpHelpers.ReadNullableFromJsonAsync<TagAdoptionPageDto>(response.Content);
    }

    public async Task<IReadOnlyList<MyTagAdoptionSummaryDto>> GetMyAdoptionIndexAsync()
    {
        HttpResponseMessage response = await Http.GetAsync("api/fanon/my-adoptions");
        await ClientHttpHelpers.ThrowIfWriteFailedAsync(response, msg => new TagValidationException(msg));
        return await response.Content.ReadFromJsonAsync<List<MyTagAdoptionSummaryDto>>() ?? [];
    }

    public async Task<TagChipDto?> FindOfficialTagByNameAsync(TagTypeEnum axis, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return await Http.GetNullableFromJsonAsync<TagChipDto>(
            $"api/fanon/official-name?axis={(short)axis}&name={Uri.EscapeDataString(name)}");
    }
}
