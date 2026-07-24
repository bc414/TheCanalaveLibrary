using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IGroupReadService"/>: HttpClient wrapper over GroupEndpoints
/// (Server/Groups/GroupEndpoints.cs). Same DTOs, same method contracts — only the transport differs
/// (the Layer-5 body-swap). <see cref="GetListingsAsync"/>/<see cref="GetMembersAsync"/> deconstruct
/// the <see cref="PagedResult{T}"/> envelope back to the tuple shape the interface expects
/// (layer5-wasm.md §"Paged results").
/// </summary>
public class ClientGroupReadService(HttpClient http) : IGroupReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<(GroupCardDto[] Items, int TotalCount)> GetListingsAsync(int page, int pageSize)
    {
        PagedResult<GroupCardDto> result = (await Http.GetFromJsonAsync<PagedResult<GroupCardDto>>(
            $"api/groups?page={page}&pageSize={pageSize}"))!;
        return (result.Items, result.TotalCount);
    }

    public async Task<GroupDetailDto?> GetByIdAsync(int groupId) =>
        await Http.GetNullableFromJsonAsync<GroupDetailDto?>($"api/groups/{groupId}");

    public async Task<GatedMetadataDto?> GetGroupGateAsync(int groupId) =>
        await Http.GetNullableFromJsonAsync<GatedMetadataDto?>($"api/groups/{groupId}/gate");

    public async Task<GroupRole?> GetCurrentUserRoleAsync(int groupId) =>
        await Http.GetNullableFromJsonAsync<GroupRole?>($"api/groups/{groupId}/role");

    public async Task<(GroupMemberDto[] Members, int TotalCount)> GetMembersAsync(
        int groupId, int page, int pageSize)
    {
        PagedResult<GroupMemberDto> result = (await Http.GetFromJsonAsync<PagedResult<GroupMemberDto>>(
            $"api/groups/{groupId}/members?page={page}&pageSize={pageSize}"))!;
        return (result.Items, result.TotalCount);
    }
}
