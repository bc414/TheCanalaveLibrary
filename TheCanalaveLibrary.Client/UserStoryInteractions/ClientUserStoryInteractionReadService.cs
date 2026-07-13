using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IUserStoryInteractionReadService"/>: HttpClient wrapper over
/// UserStoryInteractionEndpoints (Server/UserStoryInteractions/UserStoryInteractionEndpoints.cs).
/// The whole cluster's endpoint group carries <c>RequireAuthorization()</c> (per-user data), so an
/// unauthenticated caller gets a body-less 401 from the cookie handler before any of these methods'
/// bodies matter.
/// </summary>
public class ClientUserStoryInteractionReadService(HttpClient http) : IUserStoryInteractionReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<UserStoryInteractionStateDto> GetStateAsync(int storyId) =>
        await Http.GetFromJsonAsync<UserStoryInteractionStateDto>($"api/user-story-interactions/{storyId}")
        ?? UserStoryInteractionStateDto.AllFalse(storyId);

    public async Task<IReadOnlyDictionary<int, UserStoryInteractionStateDto>> GetStatesByStoryIdsAsync(
        IReadOnlyList<int> storyIds)
    {
        if (storyIds.Count == 0) return new Dictionary<int, UserStoryInteractionStateDto>();

        string query = string.Join('&', storyIds.Select(id => $"storyIds={id}"));
        return await Http.GetFromJsonAsync<Dictionary<int, UserStoryInteractionStateDto>>(
            $"api/user-story-interactions/by-ids?{query}") ?? new Dictionary<int, UserStoryInteractionStateDto>();
    }

    public async Task<IReadOnlyList<int>> GetBookshelfStoryIdsAsync(BookshelfTab tab)
    {
        HttpResponseMessage response =
            await Http.GetAsync($"api/user-story-interactions/bookshelf?tab={(int)tab}");
        // Unlike the other read methods here, this one carries a real domain exception
        // (ArgumentOutOfRangeException for tabs not backed by UserStoryInteraction) that the
        // server translates to 400 — see ThrowIfFailedAsync below.
        await ThrowIfFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<List<int>>() ?? [];
    }

    public async Task<IReadOnlyList<int>> GetFavoriteStoryIdsAsync(int userId, bool includePrivate) =>
        await Http.GetFromJsonAsync<List<int>>(
            $"api/user-story-interactions/favorites/{userId}?includePrivate={includePrivate}") ?? [];

    /// <summary>
    /// Status→exception translation (inverse of UserStoryInteractionEndpoints' use of
    /// EndpointHelpers.ExecuteWriteAsync). Shared by the bookshelf read above and every write in
    /// the subclass — the mapping is identical everywhere in this cluster since it mints no
    /// dedicated ValidationException type: 400 → ArgumentOutOfRangeException (the exact type the
    /// server's GetBookshelfStoryIdsAsync throws), 401 → InvalidOperationException (mirrors
    /// IUserSettingsService's "requires an authenticated user" contract), 403 →
    /// UnauthorizedAccessException, 404 → KeyNotFoundException, per layer5-wasm.md's contract table.
    /// </summary>
    protected static async Task ThrowIfFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        string? detail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                throw new ArgumentOutOfRangeException(null, detail ?? "The request was rejected.");
            case HttpStatusCode.Unauthorized:
                throw new InvalidOperationException(
                    detail ?? "This operation requires an authenticated user.");
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException(detail ?? "This operation is not permitted.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException(detail ?? "Not found.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }
}
