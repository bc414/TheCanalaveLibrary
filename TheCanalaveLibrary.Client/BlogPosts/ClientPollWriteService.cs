using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IPollWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerPollWriteService : ServerPollReadService. Auth rides the same-origin Identity cookie —
/// WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Translates PollEndpoints' status codes back into the service contract's typed exceptions so
/// components behave identically on either side of the interface: 400 →
/// <see cref="PollValidationException"/> (message from ProblemDetails.Detail), 401/403 →
/// <see cref="UnauthorizedAccessException"/>, 404 → <see cref="KeyNotFoundException"/>.
/// </para>
/// </summary>
public sealed class ClientPollWriteService(HttpClient http) : ClientPollReadService(http), IPollWriteService
{
    public async Task<int> CreateSitePollAsync(PollEditDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/polls/site", dto);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task<int> CreateBlogPostPollAsync(int blogPostId, PollEditDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync($"api/polls/blog-post/{blogPostId}", dto);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task UpdatePollAsync(int pollId, PollEditDto dto)
    {
        HttpResponseMessage response = await Http.PutAsJsonAsync($"api/polls/{pollId}", dto);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task ClosePollAsync(int pollId)
    {
        HttpResponseMessage response = await Http.PostAsync($"api/polls/{pollId}/close", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task SetSitePollArchivedAsync(int pollId, bool archived)
    {
        HttpResponseMessage response =
            await Http.PostAsync($"api/polls/{pollId}/archive?archived={archived}", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task DeletePollAsync(int pollId)
    {
        HttpResponseMessage response = await Http.DeleteAsync($"api/polls/{pollId}");
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task<PollDto> VoteAsync(int pollId, int[] optionIds, bool voteAnonymously)
    {
        // optionIds may be empty (retract all votes) — build the query without a stray leading
        // '&' in that case.
        string optionIdsQuery = string.Concat(optionIds.Select(id => $"optionIds={id}&"));
        HttpResponseMessage response = await Http.PostAsync(
            $"api/polls/{pollId}/vote?{optionIdsQuery}voteAnonymously={voteAnonymously}", content: null);
        await ThrowIfWriteFailedAsync(response);
        return (await response.Content.ReadFromJsonAsync<PollDto>())!;
    }

    /// <summary>Status-code → contract-exception translation (inverse of PollEndpoints').</summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                string? detail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                throw new PollValidationException(detail ?? "The poll failed validation.");
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException("This action requires an authenticated user.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException("Poll not found.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }
}
