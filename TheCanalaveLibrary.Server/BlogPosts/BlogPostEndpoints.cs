using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IBlogPostReadService"/> / <see cref="IBlogPostWriteService"/>.
/// Thin pass-throughs: no business logic here — validation, sanitization, and the author-only
/// ownership checks live in the service (single enforcement point). Every write handler wraps in
/// the shared <see cref="EndpointHelpers.ExecuteWriteAsync"/> for exception→status translation
/// (layer5-wasm.md §"The Error-Translation Contract"). <see cref="IBlogPostReadService.GetByAuthorAsync"/>/
/// <see cref="IBlogPostReadService.GetByGroupAsync"/> translate through <see cref="PagedResult{T}"/>
/// at the HTTP boundary only (layer5-wasm.md §"Paged results") — the tuple shape the interface
/// expects is unchanged.
/// <para>
/// Read auth: public for the detail/author/group listing reads — mirrors the public profile Blog
/// tab and public <c>GroupPage</c> (no <c>[Authorize]</c>). <see cref="IBlogPostReadService.GetForEditAsync"/>
/// is gated to mirror <c>BlogPostEditorPage</c>'s own <c>@attribute [Authorize]</c>; the real
/// ownership check still lives in the write service (the read only feeds the editor's UX
/// pre-check per its doc comment). <see cref="IBlogPostReadService.GetByGroupAsync"/> does not
/// check group membership itself (mirrors <c>CommentEndpoints</c>' public group-comments read) —
/// visibility of the group's own content is enforced wherever the group is fetched, not here.
/// </para>
/// <para>
/// Write auth: <c>RequireAuthorization()</c> on every write — create/like require only an
/// authenticated user; update/delete/like additionally enforce author-only ownership via
/// <c>UnauthorizedAccessException</c>, translated to 403 by <c>ExecuteWriteAsync</c>. Group blog
/// post creation additionally enforces group membership the same way (also translated to 403).
/// </para>
/// </summary>
public static class BlogPostEndpoints
{
    public static WebApplication MapBlogPostEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/blog-posts");

        // ── Reads (public unless noted — see class summary) ──

        group.MapGet("/{blogPostId:int}", async (IBlogPostReadService blogPosts, int blogPostId) =>
            Results.Json(await blogPosts.GetByIdAsync(blogPostId)));

        // Gated-existence read (WU-AccessGate): interstitial metadata for a mature-gated post
        // (reveal target = post for profile posts, GROUP for group posts); JSON null for
        // absent/unpublished/taken-down. Backs the WASM interstitial pass.
        group.MapGet("/{blogPostId:int}/gate", async (IBlogPostReadService blogPosts, int blogPostId) =>
            Results.Json(await blogPosts.GetBlogPostGateAsync(blogPostId)));

        group.MapGet("/by-author/{authorId:int}", async (
            IBlogPostReadService blogPosts, int authorId, int page, int pageSize, bool includeUnpublished = false) =>
        {
            (BlogPostListingDto[] Items, int TotalCount) result =
                await blogPosts.GetByAuthorAsync(authorId, page, pageSize, includeUnpublished);
            return Results.Ok(new PagedResult<BlogPostListingDto>(result.Items, result.TotalCount));
        });

        // Author-only editor read — wrapped in ExecuteWriteAsync (unlike the other reads) because
        // GetForEditAsync enforces the author gate and throws UnauthorizedAccessException for a
        // non-author; the shared helper translates that to 403 so the client's
        // 403→UnauthorizedAccessException mapping works over WASM (same wire shape as the
        // ChapterEndpoints/StoryEndpoints /edit routes — endpoint-authz sweep 2026-07-18).
        group.MapGet("/{blogPostId:int}/edit", (IBlogPostReadService blogPosts, int blogPostId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Json(await blogPosts.GetForEditAsync(blogPostId))))
            .RequireAuthorization();

        group.MapGet("/by-group/{groupId:int}", async (
            IBlogPostReadService blogPosts, int groupId, int page, int pageSize) =>
        {
            (BlogPostListingDto[] Items, int TotalCount) result =
                await blogPosts.GetByGroupAsync(groupId, page, pageSize);
            return Results.Ok(new PagedResult<BlogPostListingDto>(result.Items, result.TotalCount));
        });

        // ── Writes (authenticated — author/membership ownership enforced by the service) ──

        group.MapPost("/", (IBlogPostWriteService blogPosts, CreateProfileBlogPostDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await blogPosts.CreateProfileBlogPostAsync(dto))))
            .RequireAuthorization();

        group.MapPut("/{blogPostId:int}", (IBlogPostWriteService blogPosts, int blogPostId, UpdateBlogPostDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    blogPostId != dto.BlogPostId
                        ? Results.Problem(detail: "Route blogPostId does not match body BlogPostId.",
                            statusCode: StatusCodes.Status400BadRequest)
                        : await UpdateAndRespondAsync(blogPosts, dto)))
            .RequireAuthorization();

        group.MapDelete("/{blogPostId:int}", (IBlogPostWriteService blogPosts, int blogPostId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await blogPosts.DeleteBlogPostAsync(blogPostId);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapPost("/{blogPostId:int}/like", (IBlogPostWriteService blogPosts, int blogPostId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await blogPosts.ToggleLikeAsync(blogPostId))))
            .RequireAuthorization();

        group.MapPost("/group", (IBlogPostWriteService blogPosts, CreateGroupBlogPostDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await blogPosts.CreateGroupBlogPostAsync(dto))))
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> UpdateAndRespondAsync(IBlogPostWriteService blogPosts, UpdateBlogPostDto dto)
    {
        await blogPosts.UpdateBlogPostAsync(dto);
        return Results.NoContent();
    }
}
