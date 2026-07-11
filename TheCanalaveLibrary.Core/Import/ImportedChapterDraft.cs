namespace TheCanalaveLibrary.Core;

/// <summary>
/// One parsed chapter candidate (Feature 63, WU38d). <see cref="Html"/> is ALREADY sanitized
/// through <c>IHtmlSanitizationService</c> — the import pipeline's single trust boundary — so it
/// is safe to preview via <c>RichTextView</c> and to hand to the chapter write service (which
/// re-sanitizes on save; harmless). <see cref="Title"/> is the detected title (delimiter text /
/// nav entry / filename) — null when the source offered none (review UI lets the author fill it;
/// the write service defaults to "Chapter N").
/// </summary>
public record ImportedChapterDraft(
    string? Title,
    string Html,
    int WordCount,
    IReadOnlyList<ImportWarning> Warnings);
