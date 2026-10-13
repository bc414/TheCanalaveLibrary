using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Story download endpoint (WU38c). A file download is an ordinary HTTP GET with
/// <c>Content-Disposition: attachment</c> — the SignalR circuit cannot produce one, so UI download
/// affordances are plain <c>&lt;a href&gt;</c> anchors pointing here (layer2-services.md
/// §"File Downloads Bypass the Circuit"). No <c>[Authorize]</c>: the anchor navigation carries the
/// auth cookie, and the read services' content-rating filter is the permission model
/// ("export = what you can read") — invisible stories yield <c>null</c> → 404.
/// <para>Both routes carry the <see cref="RateLimitPolicy"/> concurrency limiter (owner ruling D20,
/// WU-ThrottleCoverage): generating a document is expensive and never commits, so no token bucket
/// sees it (security.md §"Write Throttling", the cost classes). A rejection is the edge limiters'
/// plain-text 429, which an anchor download shows as text.</para>
/// </summary>
public static class ExportEndpoints
{
    /// <summary>The server-wide concurrency limiter on both export downloads (registered in Program.cs).</summary>
    public const string RateLimitPolicy = "ExportGenerate";

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
        })
        .RequireRateLimiting(RateLimitPolicy);

        // Per-chapter download (WU45) — same anchor-not-circuit rule, same permission model.
        app.MapGet("/api/stories/{storyId:int}/chapters/{chapterNumber:int}/export/{format}", async (
            int storyId,
            int chapterNumber,
            string format,
            IExportService exportService) =>
        {
            if (format.Length == 0 || !format.All(char.IsLetter) ||
                !Enum.TryParse(format, ignoreCase: true, out ExportFormat parsed))
            {
                return Results.NotFound();
            }

            StoryExportResult? result =
                await exportService.ExportChapterAsync(storyId, chapterNumber, parsed);
            return result is null
                ? Results.NotFound()
                : Results.File(result.Content, result.ContentType, result.FileName);
        })
        .RequireRateLimiting(RateLimitPolicy);
    }
}
