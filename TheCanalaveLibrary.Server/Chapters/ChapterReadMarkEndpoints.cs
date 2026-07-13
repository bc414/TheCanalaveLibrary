using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IChapterReadMarkWriteService"/> — durable, write-only manual
/// read-marks (WU45; deliberately NOT the buffered Feature-44 progress-ping seam, see the
/// interface's own doc comment). Both methods require an authenticated user: the service throws
/// <see cref="InvalidOperationException"/> for anonymous callers (translated to 401 by
/// <see cref="EndpointHelpers.ExecuteWriteAsync"/>); <c>RequireAuthorization()</c> is added as
/// defense-in-depth so the cookie handler's own 401 (Program.cs <c>OnRedirectToLogin</c>) wins the
/// race first in the normal case (mirrors CommentEndpoints/TagEndpoints).
/// </summary>
public static class ChapterReadMarkEndpoints
{
    public static WebApplication MapChapterReadMarkEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/chapter-read-marks");

        group.MapPut("/{chapterId:int}",
                (IChapterReadMarkWriteService marks, int chapterId, bool isRead) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await marks.SetChapterReadAsync(chapterId, isRead);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        group.MapPut("/story/{storyId:int}",
                (IChapterReadMarkWriteService marks, int storyId, bool isRead) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await marks.SetAllChaptersReadAsync(storyId, isRead);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        return app;
    }
}
