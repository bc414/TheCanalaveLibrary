using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IChapterReadService"/> / <see cref="IChapterWriteService"/>.
/// Thin pass-throughs: no business logic here — validation lives in the service (single
/// enforcement point). The endpoint's only added job is exception→status translation via the
/// shared <see cref="EndpointHelpers.ExecuteWriteAsync"/> (layer5-wasm.md §"The Error-Translation
/// Contract").
/// <para>
/// Reads are public — mirrors <c>ChapterReadingPage</c> (SharedUI/Chapters/ChapterReadingPage.razor),
/// which carries no <c>[Authorize]</c>. The one exception is
/// <see cref="IChapterReadService.GetChapterForEditAsync"/>, which feeds the author-only editor
/// form (<c>ChapterEditorPage.razor</c> — <c>[Authorize]</c>) and is gated with
/// <c>RequireAuthorization()</c> here even though the service itself performs no authorship check
/// on that read (a pre-existing service-layer gap — flagged, not silently fixed, by this
/// mechanical add-only pass).
/// </para>
/// <para>
/// Writes all require an authenticated user (<c>RequireAuthorization()</c>, mirroring
/// <c>ChapterEditorPage.razor</c>'s <c>[Authorize]</c>). Only
/// <see cref="IChapterWriteService.MoveChapterAsync"/> and
/// <see cref="IChapterWriteService.DeleteChapterAsync"/> additionally verify story authorship
/// inside the service itself (throwing <see cref="UnauthorizedAccessException"/>); the other five
/// write methods (Create/AddAlternateVersion/UpdateContent/SetPrimaryVersion/SetPublished) carry
/// no service-level ownership check today — same pre-existing gap as above, out of scope for this
/// pass (it changes nothing about the pre-existing production behavior, which relies on the
/// editor page's own <c>[Authorize]</c> gate; the HTTP surface added here is no more permissive
/// than that page already was).
/// </para>
/// </summary>
public static class ChapterEndpoints
{
    public static WebApplication MapChapterEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/chapters");

        // ── Reads (public — mirror the public ChapterReadingPage) ──

        group.MapGet("/{storyId:int}/{chapterNumber:int}",
            async (IChapterReadService chapters, int storyId, int chapterNumber, int? versionOrder) =>
                Results.Json(await chapters.GetChapterForReadingAsync(storyId, chapterNumber, versionOrder)));

        group.MapGet("/{storyId:int}/toc", async (IChapterReadService chapters, int storyId) =>
            Results.Ok(await chapters.GetChapterTocAsync(storyId)));

        group.MapGet("/{storyId:int}/{chapterNumber:int}/versions",
            async (IChapterReadService chapters, int storyId, int chapterNumber) =>
                Results.Ok(await chapters.GetChapterVersionsAsync(storyId, chapterNumber)));

        group.MapGet("/{storyId:int}/list", async (IChapterReadService chapters, int storyId) =>
            Results.Ok(await chapters.GetChapterListAsync(storyId)));

        group.MapGet("/{storyId:int}/last-interaction",
            async (IChapterReadService chapters, int storyId) =>
                Results.Json(await chapters.GetViewerLastInteractionUtcAsync(storyId)));

        group.MapGet("/{storyId:int}/export", async (IChapterReadService chapters, int storyId) =>
            Results.Ok(await chapters.GetChaptersForExportAsync(storyId)));

        // Author-only editor read — see class doc's authorization note.
        group.MapGet("/edit/{chapterContentId:long}",
                async (IChapterReadService chapters, long chapterContentId) =>
                    Results.Json(await chapters.GetChapterForEditAsync(chapterContentId)))
            .RequireAuthorization();

        // ── Writes (authenticated author — RequireAuthorization(); see class doc) ──

        group.MapPost("/", (IChapterWriteService chapters, CreateChapterDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await chapters.CreateChapterAsync(dto))))
            .RequireAuthorization();

        group.MapPost("/{chapterId:int}/versions",
                (IChapterWriteService chapters, int chapterId, CreateChapterDto dto) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                        Results.Ok(await chapters.AddAlternateVersionAsync(chapterId, dto))))
            .RequireAuthorization();

        group.MapPut("/content/{chapterContentId:long}",
                (IChapterWriteService chapters, long chapterContentId, UpdateChapterContentDto dto) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                        chapterContentId != dto.ChapterContentId
                            ? Results.Problem(
                                detail: "Route chapterContentId does not match body ChapterContentId.",
                                statusCode: StatusCodes.Status400BadRequest)
                            : await UpdateContentAndReturnNoContentAsync(chapters, dto)))
            .RequireAuthorization();

        group.MapPut("/{chapterId:int}/primary/{chapterContentId:long}",
                (IChapterWriteService chapters, int chapterId, long chapterContentId) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await chapters.SetPrimaryVersionAsync(chapterId, chapterContentId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        group.MapPut("/{chapterId:int}/published",
                (IChapterWriteService chapters, int chapterId, bool isPublished) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await chapters.SetPublishedAsync(chapterId, isPublished);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        group.MapPut("/{storyId:int}/move",
                (IChapterWriteService chapters, int storyId, int fromNumber, int toNumber) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await chapters.MoveChapterAsync(storyId, fromNumber, toNumber);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        group.MapDelete("/{chapterId:int}",
                (IChapterWriteService chapters, int chapterId) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await chapters.DeleteChapterAsync(chapterId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        return app;
    }

    // Mirrors CommentEndpoints' EditAndReturnNoContentAsync — the ternary's other branch
    // (Results.Problem) is a sync IResult, so this branch needs its own async method + await to
    // share a static type with the enclosing async lambda.
    private static async Task<IResult> UpdateContentAndReturnNoContentAsync(
        IChapterWriteService chapters, UpdateChapterContentDto dto)
    {
        await chapters.UpdateChapterContentAsync(dto);
        return Results.NoContent();
    }
}
