using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ICommentWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerCommentWriteService : ServerCommentReadService. Auth rides the same-origin Identity
/// cookie — WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Translates CommentEndpoints' status codes back into the service contract's typed exceptions so
/// components behave identically on either side of the interface: 400 → CommentValidationException
/// (errors from ProblemDetails.Detail — the server joins the validation errors into one message,
/// so the client wraps it back into a single-element list rather than re-splitting it), 401/403 →
/// UnauthorizedAccessException, 404 → KeyNotFoundException, 429 →
/// WriteRateLimitExceededException (kind is always <see cref="WriteActionKind.Comment"/> here — the
/// only action kind this service's writes ever throttle on; retry-after seconds comes off the
/// ProblemDetails extensions via <see cref="ClientHttpHelpers.ReadRetryAfterSecondsAsync"/>).
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

    /// <summary>Status-code → contract-exception translation (inverse of CommentEndpoints').</summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                string? detail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                throw new CommentValidationException([detail ?? "The comment failed validation."]);
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException("This action requires an authenticated user.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException("Comment not found.");
            case HttpStatusCode.TooManyRequests:
                double? retryAfterSeconds = await ClientHttpHelpers.ReadRetryAfterSecondsAsync(response);
                throw new WriteRateLimitExceededException(
                    WriteActionKind.Comment, TimeSpan.FromSeconds(retryAfterSeconds ?? 60));
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }
}
