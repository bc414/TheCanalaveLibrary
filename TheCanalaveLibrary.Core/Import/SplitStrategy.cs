namespace TheCanalaveLibrary.Core;

/// <summary>
/// How the one-document-many-chapters mode (mode 4) splits a parsed document into chapter drafts.
/// Strategies describe the NORMALIZED document (import normalization maps source Heading 1→h2,
/// Heading 2+→h3 — see <c>audit/Import.md</c>), so "top heading" = <c>h2</c>, "sub-heading" =
/// <c>h3</c>. Splitting runs ONLY in mode 4 — a single-chapter doc in any other mode can never be
/// accidentally shredded (settled: explicit modes, common backend).
/// </summary>
public enum SplitStrategy
{
    /// <summary>No split — the whole document is one chapter.</summary>
    None,

    /// <summary>Split at each top-level heading (normalized <c>h2</c>).</summary>
    TopHeading,

    /// <summary>Split at each sub-heading (normalized <c>h3</c>).</summary>
    SubHeading,

    /// <summary>Split at standalone "Chapter N"-like paragraphs (regex; the line becomes the title).</summary>
    ChapterTextPattern,

    /// <summary>Split at explicit page breaks (DOCX only; available when the source carried any).</summary>
    PageBreak
}
