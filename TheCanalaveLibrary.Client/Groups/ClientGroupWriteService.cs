using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IGroupWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerGroupWriteService : ServerGroupReadService. Auth rides the same-origin Identity cookie —
/// WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Translates GroupEndpoints' status codes back into the service contract's typed exceptions so
/// components behave identically on either side of the interface: 400 →
/// <see cref="GroupValidationException"/> (message from ProblemDetails.Detail, wrapped as a
/// single-element errors list — the server already joins the original error list into one string
/// before it crosses the HTTP boundary, see <c>GroupValidationException</c>'s own constructor and
/// <c>EndpointHelpers.ExecuteWriteAsync</c>), 401 → <see cref="UnauthorizedAccessException"/> (not
/// signed in), 404 → <see cref="KeyNotFoundException"/>.
/// </para>
/// <para>
/// 403 is ambiguous server-side — <c>EndpointHelpers.ExecuteWriteAsync</c> maps both the plain
/// member/admin gate (<see cref="UnauthorizedAccessException"/>, no <c>ProblemDetails.Detail</c>)
/// and the content-rating waterfall (<see cref="ContentRatingExceededException"/>, <c>Detail</c>
/// carries the message) to the same status code. The client disambiguates the same way the server
/// distinguishes them: a non-null <c>Detail</c> means the rating ceiling was exceeded; an empty body
/// means a plain authorization failure.
/// </para>
/// </summary>
public sealed class ClientGroupWriteService(HttpClient http)
    : ClientGroupReadService(http), IGroupWriteService
{
    public async Task<int> CreateGroupAsync(CreateGroupDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/groups", dto);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task UpdateGroupAsync(UpdateGroupDto dto)
    {
        HttpResponseMessage response = await Http.PutAsJsonAsync($"api/groups/{dto.GroupId}", dto);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task JoinAsync(int groupId)
    {
        HttpResponseMessage response = await Http.PostAsync($"api/groups/{groupId}/join", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task LeaveAsync(int groupId)
    {
        HttpResponseMessage response = await Http.PostAsync($"api/groups/{groupId}/leave", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task AddStoryAsync(AddGroupStoryDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/groups/stories", dto);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task RemoveStoryAsync(int groupStoryId)
    {
        HttpResponseMessage response = await Http.DeleteAsync($"api/groups/stories/{groupStoryId}");
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task AssignStoryToFolderAsync(int groupStoryId, int groupFolderId)
    {
        HttpResponseMessage response = await Http.PutAsync(
            $"api/groups/stories/{groupStoryId}/folder/{groupFolderId}", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task UnassignStoryFromFolderAsync(int groupStoryId, int groupFolderId)
    {
        HttpResponseMessage response = await Http.DeleteAsync(
            $"api/groups/stories/{groupStoryId}/folder/{groupFolderId}");
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task<int> CreateFolderAsync(CreateFolderDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/groups/folders", dto);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task RenameFolderAsync(int groupFolderId, string newName)
    {
        HttpResponseMessage response =
            await Http.PutAsJsonAsync($"api/groups/folders/{groupFolderId}/name", newName);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task DeleteFolderAsync(int groupFolderId)
    {
        HttpResponseMessage response = await Http.DeleteAsync($"api/groups/folders/{groupFolderId}");
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task ReorderFolderAsync(int groupFolderId, int newSortOrder)
    {
        HttpResponseMessage response = await Http.PutAsync(
            $"api/groups/folders/{groupFolderId}/sort-order?newSortOrder={newSortOrder}", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    /// <summary>Status-code → contract-exception translation (inverse of GroupEndpoints').</summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                string? validationDetail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                throw new GroupValidationException([validationDetail ?? "The group failed validation."]);
            case HttpStatusCode.Unauthorized:
                throw new UnauthorizedAccessException("This action requires an authenticated user.");
            case HttpStatusCode.Forbidden:
                // Disambiguate the two 403 causes EndpointHelpers maps identically in status code —
                // see class summary. A populated Detail is the content-rating waterfall message;
                // an empty body is the plain member/admin gate.
                string? forbiddenDetail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                if (forbiddenDetail is not null)
                    throw new ContentRatingExceededException(forbiddenDetail);
                throw new UnauthorizedAccessException(
                    "You must be a Member or Admin of this group to perform this action.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException("Group not found.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }
}
