namespace TheCanalaveLibrary.Core;

/// <summary>
/// Chapter import (Feature 63, WU38d): parses uploaded files into sanitized chapter drafts.
/// One backend, five explicit UI modes (audit/Import.md "Settled") — the caller declares intent
/// by choosing the method, so auto-detect splitting can never shred a doc the author meant as one
/// chapter. Every returned draft's HTML has passed <c>IHtmlSanitizationService</c> (the single
/// trust boundary; the allowlist is the fidelity contract, layer2-services.md §"Export &amp;
/// Import"). All methods throw <see cref="ImportException"/> with a presentation-safe message on
/// unreadable/oversized/malformed input.
/// </summary>
public interface IContentImportService
{
    /// <summary>
    /// Parses one file as ONE chapter (modes 1 into-editor / 2 as-version / 3 file-per-chapter).
    /// Never splits. EPUB is not accepted here (inherently multi-chapter → <see cref="ParseEpubAsync"/>).
    /// </summary>
    Task<ImportedChapterDraft> ParseSingleAsync(Stream file, string fileName, ImportFormat format);

    /// <summary>
    /// Parses one document that may hold a whole story (mode 4): suggests a
    /// <see cref="SplitStrategy"/>, returns the drafts for it plus the strategies worth offering.
    /// Re-split with <see cref="Resplit"/> — in-memory, no re-upload.
    /// </summary>
    Task<ImportParseResult> ParseDocumentAsync(Stream file, string fileName, ImportFormat format);

    /// <summary>
    /// Parses an EPUB (mode 5): spine reading order defines the drafts, titles come from the
    /// navigation document; no splitting UI. Front matter is dropped by the author in review.
    /// </summary>
    Task<ImportParseResult> ParseEpubAsync(Stream file);

    /// <summary>
    /// Recomputes <paramref name="parsed"/>'s drafts under a different strategy (mode 4's
    /// delimiter picker). Pure in-memory: works on <see cref="ImportParseResult.NormalizedHtml"/>.
    /// Throws when <paramref name="parsed"/> has no normalized document (EPUB results).
    /// </summary>
    IReadOnlyList<ImportedChapterDraft> Resplit(ImportParseResult parsed, SplitStrategy strategy);
}
