using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IPollReadService"/>: HttpClient wrapper over
/// Server/BlogPosts/PollEndpoints.cs (Feature 37). Same DTOs, same method contracts — only the
/// transport differs (the Layer-5 body-swap). The viewer-relative projection (results visibility,
/// voter-name blanking) is computed server-side either way; the client never re-derives it.
/// </summary>
public class ClientPollReadService(HttpClient http) : IPollReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<PollDto[]> GetSitePollsAsync(bool includeArchived) =>
        await Http.GetFromJsonAsync<PollDto[]>($"api/polls?includeArchived={includeArchived}") ?? [];

    public async Task<PollDto[]> GetPollsForBlogPostAsync(int blogPostId) =>
        await Http.GetFromJsonAsync<PollDto[]>($"api/polls/by-blog-post/{blogPostId}") ?? [];

    public async Task<PollDto?> GetPollAsync(int pollId) =>
        await Http.GetNullableFromJsonAsync<PollDto?>($"api/polls/{pollId}");
}
