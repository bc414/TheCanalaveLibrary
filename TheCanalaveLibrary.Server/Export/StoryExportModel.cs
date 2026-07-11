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
    DateTime PublishDate,
    DateTime LastUpdatedDate,
    IReadOnlyList<ChapterExportDto> Chapters)
{
    public string RatingLabel => Rating switch
    {
        Rating.E => "Everyone",
        Rating.T => "Teen",
        Rating.M => "Mature",
        _ => "Unknown"
    };
}
