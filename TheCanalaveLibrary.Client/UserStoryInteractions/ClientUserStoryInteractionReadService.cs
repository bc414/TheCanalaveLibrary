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

    // includePrivate is NOT sent over the wire — the server derives it from the auth cookie
    // (hidden favorites are owner-only; MA-602 pattern, endpoint-authz sweep 2026-07-18). The
    // parameter survives for interface parity with the server impl.
    public async Task<IReadOnlyList<int>> GetFavoriteStoryIdsAsync(int userId, bool includePrivate) =>
        await Http.GetFromJsonAsync<List<int>>(
            $"api/user-story-interactions/favorites/{userId}") ?? [];

    /// <summary>
    /// Status→exception translation (inverse of UserStoryInteractionEndpoints' use of
    /// EndpointHelpers.ExecuteAsync). Shared by the bookshelf read above and every write in the
    /// subclass — the mapping is identical everywhere in this cluster since it mints no dedicated
    /// ValidationException type, so this delegates to the shared
    /// <see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/> with an <c>ArgumentOutOfRangeException</c>
    /// factory (the exact type the server's <c>GetBookshelfStoryIdsAsync</c> throws for 400).
    /// 401/403/404 are the shared helper's standard arms (WU-ErrorHandling2, 2026-07-30 —
    /// previously its own private 401 → <c>InvalidOperationException</c> arm, predating
    /// <see cref="SessionExpiredException"/> — this closes tracker item D5's "shipped behavior
    /// change" for this cluster).
    /// </summary>
    protected static Task ThrowIfFailedAsync(HttpResponseMessage response) =>
        ClientHttpHelpers.ThrowIfWriteFailedAsync(
            response, detail => new ArgumentOutOfRangeException(null, detail));
}
