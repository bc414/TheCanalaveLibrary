namespace TheCanalaveLibrary.Core;

/// <summary>
/// Category of a lossy or noteworthy event during import (Feature 63, WU38d). Unrepresentable
/// source formatting is stripped <b>with a warning</b>, never silently — the allowlist is the
/// fidelity contract and the author gets told what didn't survive it.
/// </summary>
public enum ImportWarningKind
{
    /// <summary>Source contained images — the chapter allowlist has no <c>img</c>.</summary>
    ImagesDropped,

    /// <summary>A named style/construct the reader didn't recognize (Mammoth conversion message).</summary>
    UnrecognizedStyle,

    /// <summary>Formatting with no allowlist equivalent was flattened (e.g. tables, deep headings).</summary>
    UnsupportedFormatting,

    /// <summary>The file (or a split segment) produced no readable text.</summary>
    EmptyContent
}

/// <summary>One import warning, shown to the importing author in the review UI.</summary>
public record ImportWarning(ImportWarningKind Kind, string Message);
