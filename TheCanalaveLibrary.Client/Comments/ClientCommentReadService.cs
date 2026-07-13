using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ICommentReadService"/>: HttpClient wrapper over CommentEndpoints
/// (Server/Comments/CommentEndpoints.cs). Same DTOs, same method contracts — only the transport
/// differs (the Layer-5 body-swap). One GET per comment context (chapter/blog-post/group/profile),
/// mirroring the service interface's per-context method pattern.
/// </summary>
public class ClientCommentReadService(HttpClient http) : ICommentReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<CommentPageDto> GetChapterCommentsAsync(int chapterId, int page, int pageSize) =>
        (await Http.GetFromJsonAsync<CommentPageDto>(
            $"api/comments/chapter/{chapterId}?page={page}&pageSize={pageSize}"))!;

    public async Task<CommentPageDto> GetBlogPostCommentsAsync(int blogPostId, int page, int pageSize) =>
        (await Http.GetFromJsonAsync<CommentPageDto>(
            $"api/comments/blog-post/{blogPostId}?page={page}&pageSize={pageSize}"))!;

    public async Task<CommentPageDto> GetGroupCommentsAsync(int groupId, int page, int pageSize) =>
        (await Http.GetFromJsonAsync<CommentPageDto>(
            $"api/comments/group/{groupId}?page={page}&pageSize={pageSize}"))!;

    public async Task<CommentPageDto> GetUserProfileCommentsAsync(int profileUserId, int page, int pageSize) =>
        (await Http.GetFromJsonAsync<CommentPageDto>(
            $"api/comments/profile/{profileUserId}?page={page}&pageSize={pageSize}"))!;
}
