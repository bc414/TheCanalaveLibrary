using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ICommentWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerCommentWriteService : ServerCommentReadService. Auth rides the same-origin Identity
/// cookie — WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Translates CommentEndpoints' status codes back into the service contract's typed exceptions so
/// components behave identically on either side of the interface — the shared MA-008 shape
/// (<see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/>); 400 reconstructs
/// <see cref="CommentValidationException"/> and 429 reconstructs WriteRateLimitExceededException
/// with <see cref="WriteActionKind.Comment"/> — the only action kind this service's writes ever
/// throttle on.
/// </para>
/// </summary>
public sealed class ClientCommentWriteService(HttpClient http)
    : ClientCommentReadService(http), ICommentWriteService
{
    public async Task<long> PostChapterCommentAsync(PostChapterCommentDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/comments/chapter", dto);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<long>();
    }

    public async Task<long> PostBlogPostCommentAsync(PostBlogPostCommentDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/comments/blog-post", dto);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<long>();
    }

    public async Task<long> PostGroupCommentAsync(PostGroupCommentDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/comments/group", dto);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<long>();
    }

    public async Task<long> PostUserProfileCommentAsync(PostUserProfileCommentDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/comments/profile", dto);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<long>();
    }

    public async Task EditCommentAsync(UpdateCommentDto dto)
    {
        HttpResponseMessage response = await Http.PutAsJsonAsync($"api/comments/{dto.CommentId}", dto);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task DeleteCommentAsync(long commentId)
    {
        HttpResponseMessage response = await Http.DeleteAsync($"api/comments/{commentId}");
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task<CommentLikeResultDto> ToggleLikeAsync(long commentId)
    {
        HttpResponseMessage response = await Http.PostAsync($"api/comments/{commentId}/like", content: null);
        await ThrowIfWriteFailedAsync(response);
        return (await response.Content.ReadFromJsonAsync<CommentLikeResultDto>())!;
    }

    /// <summary>Status-code → contract-exception translation (inverse of CommentEndpoints') — the
    /// shared MA-008 shape, including the 429 write-throttle reconstruction.</summary>
    private static Task ThrowIfWriteFailedAsync(HttpResponseMessage response) =>
        ClientHttpHelpers.ThrowIfWriteFailedAsync(response,
            msg => new CommentValidationException([msg]), WriteActionKind.Comment);
}
