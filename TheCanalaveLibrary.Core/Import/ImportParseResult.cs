namespace TheCanalaveLibrary.Core;

/// <summary>
/// Result of parsing a whole document (mode 4) or an EPUB (mode 5) — the suggest-then-refine
/// state: the UI shows <see cref="Drafts"/> (split by <see cref="SuggestedStrategy"/>), the author
/// can pick a different <see cref="AvailableStrategies"/> entry, and
/// <c>IContentImportService.Resplit</c> recomputes drafts from <see cref="NormalizedHtml"/>
/// in memory — no re-upload, no re-parse.
/// </summary>
/// <param name="NormalizedHtml">
/// The full document as normalized (near-allowlist) HTML, pre-sanitize — it retains the
/// <c>&lt;hr&gt;</c> page-break markers splitting needs (the sanitizer would strip them).
/// NEVER rendered raw: every draft's <c>Html</c> is sanitized per segment. Null for EPUB
/// (spine-defined chapters — no re-split).
/// </param>
/// <param name="BookTitle">EPUB metadata title (display-only prefill hint); null otherwise.</param>
/// <param name="BookAuthor">EPUB metadata author (display-only); null otherwise.</param>
public record ImportParseResult(
    string? NormalizedHtml,
    SplitStrategy SuggestedStrategy,
    IReadOnlyList<SplitStrategy> AvailableStrategies,
    IReadOnlyList<ImportedChapterDraft> Drafts,
    IReadOnlyList<ImportWarning> Warnings,
    string? BookTitle = null,
    string? BookAuthor = null);
