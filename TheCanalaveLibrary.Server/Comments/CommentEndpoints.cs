using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="ICommentReadService"/> / <see cref="ICommentWriteService"/>.
/// Thin pass-throughs: no business logic here — validation, sanitization, and the author-only
/// ownership checks live in the service (single enforcement point). The endpoint's only added job
/// is exception→status translation, via the shared <see cref="EndpointHelpers.ExecuteWriteAsync"/>
/// (layer5-wasm.md §"The Error-Translation Contract").
/// <para>
/// Comments are polymorphic across four contexts (chapter/blog-post/group/profile-wall), each with
/// its own dedicated read+post method pair on the service interfaces (no shared discriminator/enum
/// — see <c>ICommentReadService</c>'s doc comment). Routes mirror that per-context split with a
/// sub-path segment per context, all under the mechanically-pluralized <c>/api/comments</c> route
/// (layer5-wasm.md's route table only lists exceptions to mechanical pluralization; Comments isn't
/// one).
/// </para>
/// <para>
/// Reads are public — mirrors the public chapter/blog-post/group/profile pages that host comment
/// sections; visibility of the parent content is enforced wherever that content is fetched, not
/// here. Writes (post/edit/delete/like) require an authenticated user: the service throws
/// <see cref="InvalidOperationException"/> for unauthenticated callers (translated to 401 by
/// <see cref="EndpointHelpers.ExecuteWriteAsync"/>); <c>RequireAuthorization()</c> is added as
/// defense-in-depth so the cookie handler's own 401 (Program.cs <c>OnRedirectToLogin</c>) wins the
/// race first in the normal case.
/// </para>
/// </summary>
public static class CommentEndpoints
{
    public static WebApplication MapCommentEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/comments");

        // ── Reads (public — mirror the public chapter/blog-post/group/profile pages) ──

        group.MapGet("/chapter/{chapterId:int}",
            async (ICommentReadService comments, int chapterId, int page, int pageSize) =>
                Results.Ok(await comments.GetChapterCommentsAsync(chapterId, page, pageSize)));

        group.MapGet("/blog-post/{blogPostId:int}",
            async (ICommentReadService comments, int blogPostId, int page, int pageSize) =>
                Results.Ok(await comments.GetBlogPostCommentsAsync(blogPostId, page, pageSize)));

        group.MapGet("/group/{groupId:int}",
            async (ICommentReadService comments, int groupId, int page, int pageSize) =>
                Results.Ok(await comments.GetGroupCommentsAsync(groupId, page, pageSize)));

        group.MapGet("/profile/{profileUserId:int}",
            async (ICommentReadService comments, int profileUserId, int page, int pageSize) =>
                Results.Ok(await comments.GetUserProfileCommentsAsync(profileUserId, page, pageSize)));

        // ── Writes (any authenticated user — author-only edit/delete enforced in the service) ──

        group.MapPost("/chapter", (ICommentWriteService comments, PostChapterCommentDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await comments.PostChapterCommentAsync(dto))))
            .RequireAuthorization();

        group.MapPost("/blog-post", (ICommentWriteService comments, PostBlogPostCommentDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await comments.PostBlogPostCommentAsync(dto))))
            .RequireAuthorization();

        group.MapPost("/group", (ICommentWriteService comments, PostGroupCommentDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await comments.PostGroupCommentAsync(dto))))
            .RequireAuthorization();

        group.MapPost("/profile", (ICommentWriteService comments, PostUserProfileCommentDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await comments.PostUserProfileCommentAsync(dto))))
            .RequireAuthorization();

        group.MapPut("/{commentId:long}", (ICommentWriteService comments, long commentId, UpdateCommentDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    commentId != dto.CommentId
                        ? Results.Problem(detail: "Route commentId does not match body CommentId.",
                            statusCode: StatusCodes.Status400BadRequest)
                        : await EditAndReturnNoContentAsync(comments, dto)))
            .RequireAuthorization();

        group.MapDelete("/{commentId:long}", (ICommentWriteService comments, long commentId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await comments.DeleteCommentAsync(commentId);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapPost("/{commentId:long}/like", (ICommentWriteService comments, long commentId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await comments.ToggleLikeAsync(commentId))))
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> EditAndReturnNoContentAsync(ICommentWriteService comments, UpdateCommentDto dto)
    {
        await comments.EditCommentAsync(dto);
        return Results.NoContent();
    }
}
