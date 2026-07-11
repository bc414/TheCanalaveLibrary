namespace TheCanalaveLibrary.Core;

/// <summary>
/// One chapter's full content for story export (WU38c) — the bulk companion to
/// <see cref="ChapterReadingDto"/>: every published chapter's primary-version HTML in reading
/// order, fetched in one query instead of a per-chapter round-trip.
/// <see cref="HtmlContent"/> is sanitized stored HTML (trusted; sanitize-once-on-save).
/// </summary>
public record ChapterExportDto(
    int ChapterNumber,
    string Title,
    string HtmlContent,
    string? TopAuthorsNote,
    string? BottomAuthorsNote);
