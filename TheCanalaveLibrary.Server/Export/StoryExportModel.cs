using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Normalized input for the per-format export writers (WU38c) — everything a writer needs, already
/// loaded and viewer-filtered by <see cref="ServerExportService"/>. Server-internal shape (never
/// crosses to UI — the DTO firewall doesn't apply); public so <c>Tests.Unit</c> can construct it
/// directly and exercise writers as pure functions.
/// <see cref="LongDescriptionHtml"/> and each chapter's HTML are sanitized stored HTML (trusted).
/// </summary>
public sealed record StoryExportModel(
    int StoryId,
    string Title,
    string AuthorName,
    Rating Rating,
    string? LongDescriptionHtml,
    /// <summary>First publication on this site; null = never published (D2) — only the author can
    /// export such a story.</summary>
    DateTime? PublishDate,
    DateTime LastUpdatedDate,
    IReadOnlyList<ChapterExportDto> Chapters)
{
    /// <summary>"Published MMM d, yyyy", or "Not yet published" for a never-published story.</summary>
    public string PublishedLabel => FormatPublished("MMM d, yyyy");

    /// <summary><see cref="PublishedLabel"/> with a writer-specific date format (Markdown keeps its
    /// ISO-style dates).</summary>
    public string FormatPublished(string dateFormat) =>
        PublishDate is DateTime published ? $"Published {published.ToString(dateFormat)}" : "Not yet published";

    public string RatingLabel => Rating switch
    {
        Rating.E => "Everyone",
        Rating.T => "Teen",
        Rating.M => "Mature",
        _ => "Unknown"
    };
}
