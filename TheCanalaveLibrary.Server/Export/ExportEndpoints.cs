using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Story download endpoint (WU38c). A file download is an ordinary HTTP GET with
/// <c>Content-Disposition: attachment</c> — the SignalR circuit cannot produce one, so UI download
/// affordances are plain <c>&lt;a href&gt;</c> anchors pointing here (layer2-services.md
/// §"File Downloads Bypass the Circuit"). No <c>[Authorize]</c>: the anchor navigation carries the
/// auth cookie, and the read services' content-rating filter is the permission model
/// ("export = what you can read") — invisible stories yield <c>null</c> → 404.
/// </summary>
public static class ExportEndpoints
{
    public static void MapExportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/stories/{storyId:int}/export/{format}", async (
            int storyId,
            string format,
            IExportService exportService) =>
        {
            // Alpha-only guard keeps numeric enum values ("3") out of the URL space —
            // only the format names are routable.
            if (format.Length == 0 || !format.All(char.IsLetter) ||
                !Enum.TryParse(format, ignoreCase: true, out ExportFormat parsed))
            {
                return Results.NotFound();
            }

            StoryExportResult? result = await exportService.ExportStoryAsync(storyId, parsed);
            return result is null
                ? Results.NotFound()
                : Results.File(result.Content, result.ContentType, result.FileName);
        });
    }
}
