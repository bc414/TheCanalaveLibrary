using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IBlogPostWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerBlogPostWriteService : ServerBlogPostReadService. Auth rides the same-origin Identity
/// cookie — WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Translates BlogPostEndpoints' status codes back into the service contract's typed exceptions so
/// components behave identically on either side of the interface: 400 →
/// <see cref="BlogPostValidationException"/> (message from ProblemDetails.Detail — the server joins
/// validation errors into one message, so the client wraps it back into a single-element list
/// rather than re-splitting it), 401/403 → <see cref="UnauthorizedAccessException"/>, 404 →
/// <see cref="KeyNotFoundException"/>.
/// </para>
/// </summary>
public sealed class ClientBlogPostWriteService(HttpClient http)
    : ClientBlogPostReadService(http), IBlogPostWriteService
{
    public async Task<int> CreateProfileBlogPostAsync(CreateProfileBlogPostDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/blog-posts", dto);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task UpdateBlogPostAsync(UpdateBlogPostDto dto)
    {
        HttpResponseMessage response = await Http.PutAsJsonAsync($"api/blog-posts/{dto.BlogPostId}", dto);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task DeleteBlogPostAsync(int blogPostId)
    {
        HttpResponseMessage response = await Http.DeleteAsync($"api/blog-posts/{blogPostId}");
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task<BlogPostLikeResultDto> ToggleLikeAsync(int blogPostId)
    {
        HttpResponseMessage response = await Http.PostAsync($"api/blog-posts/{blogPostId}/like", content: null);
        await ThrowIfWriteFailedAsync(response);
        return (await response.Content.ReadFromJsonAsync<BlogPostLikeResultDto>())!;
    }

    public async Task<int> CreateGroupBlogPostAsync(CreateGroupBlogPostDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/blog-posts/group", dto);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    /// <summary>Status-code → contract-exception translation (inverse of BlogPostEndpoints').</summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                string? detail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                throw new BlogPostValidationException([detail ?? "The blog post failed validation."]);
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException("This action requires an authenticated user.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException("Blog post not found.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }
}
