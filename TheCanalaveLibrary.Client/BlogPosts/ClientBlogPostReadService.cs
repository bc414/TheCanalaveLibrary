using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IBlogPostReadService"/>: HttpClient wrapper over
/// Server/BlogPosts/BlogPostEndpoints.cs. Same DTOs, same method contracts — only the transport
/// differs (the Layer-5 body-swap). <see cref="GetByAuthorAsync"/>/<see cref="GetByGroupAsync"/>
/// translate through <see cref="PagedResult{T}"/> at the HTTP boundary only (layer5-wasm.md
/// §"Paged results") — the tuple shape the interface expects is unchanged.
/// </summary>
public class ClientBlogPostReadService(HttpClient http) : IBlogPostReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<BlogPostDto?> GetByIdAsync(int blogPostId) =>
        await Http.GetNullableFromJsonAsync<BlogPostDto?>($"api/blog-posts/{blogPostId}");

    public async Task<(BlogPostListingDto[] Items, int TotalCount)> GetByAuthorAsync(
        int authorId, int page, int pageSize, bool includeUnpublished = false)
    {
        PagedResult<BlogPostListingDto> result = (await Http.GetFromJsonAsync<PagedResult<BlogPostListingDto>>(
            $"api/blog-posts/by-author/{authorId}?page={page}&pageSize={pageSize}&includeUnpublished={includeUnpublished}"))!;
        return (result.Items, result.TotalCount);
    }

    public async Task<BlogPostEditDto?> GetForEditAsync(int blogPostId) =>
        await Http.GetNullableFromJsonAsync<BlogPostEditDto?>($"api/blog-posts/{blogPostId}/edit");

    public async Task<(BlogPostListingDto[] Items, int TotalCount)> GetByGroupAsync(
        int groupId, int page, int pageSize)
    {
        PagedResult<BlogPostListingDto> result = (await Http.GetFromJsonAsync<PagedResult<BlogPostListingDto>>(
            $"api/blog-posts/by-group/{groupId}?page={page}&pageSize={pageSize}"))!;
        return (result.Items, result.TotalCount);
    }
}
