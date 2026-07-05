using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ITagReadService"/>: HttpClient wrapper over TagEndpoints
/// (Server/Tags/TagEndpoints.cs). Same DTOs, same method contracts — only the transport differs
/// (the Layer-5 body-swap). The four GetAll{Type}TagsAsync convenience wrappers delegate to
/// GetTagsByTypeAsync locally, mirroring the server impl's own delegation.
/// </summary>
public class ClientTagReadService(HttpClient http) : ITagReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<List<TagDirectoryGroupDto>> GetTagDirectoryAsync() =>
        await Http.GetFromJsonAsync<List<TagDirectoryGroupDto>>("api/tags/directory") ?? [];

    public async Task<List<TagDropDownDTO>> GetTagsByTypeAsync(TagTypeEnum type) =>
        await Http.GetFromJsonAsync<List<TagDropDownDTO>>($"api/tags?type={(short)type}") ?? [];

    public async Task<List<TagChipDto>> SearchTagChipsAsync(TagTypeEnum type, string term)
    {
        // Mirror the server impl's blank-term short-circuit — no round trip for a no-op.
        if (string.IsNullOrWhiteSpace(term)) return [];

        return await Http.GetFromJsonAsync<List<TagChipDto>>(
            $"api/tags/chips/search?type={(short)type}&term={Uri.EscapeDataString(term)}") ?? [];
    }

    public async Task<List<TagChipDto>> GetTagChipsByIdsAsync(IReadOnlyList<int> tagIds)
    {
        if (tagIds.Count == 0) return [];

        string query = string.Join('&', tagIds.Select(id => $"ids={id}"));
        return await Http.GetFromJsonAsync<List<TagChipDto>>($"api/tags/chips/by-ids?{query}") ?? [];
    }

    public Task<List<TagDropDownDTO>> GetAllCharacterTagsAsync() =>
        GetTagsByTypeAsync(TagTypeEnum.Character);

    public Task<List<TagDropDownDTO>> GetAllSettingTagsAsync() =>
        GetTagsByTypeAsync(TagTypeEnum.Setting);

    public Task<List<TagDropDownDTO>> GetAllGenreTagsAsync() =>
        GetTagsByTypeAsync(TagTypeEnum.Genre);

    public Task<List<TagDropDownDTO>> GetAllContentWarningTagsAsync() =>
        GetTagsByTypeAsync(TagTypeEnum.ContentWarning);
}
