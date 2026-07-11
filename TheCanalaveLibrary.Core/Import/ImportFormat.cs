namespace TheCanalaveLibrary.Core;

/// <summary>
/// File formats the chapter import pipeline can ingest (Feature 63, WU38d). A downloaded Google
/// Doc IS a .docx — same parser. <b>PDF is deliberately absent</b>: positioned glyphs, not
/// structure — it cannot meet the formatting-preservation contract; EPUB/HTML cover the AO3/FFN
/// exit paths (audit/Import.md "Settled"). Adding a format = one enum value + one reader.
/// </summary>
public enum ImportFormat
{
    Docx,
    Epub,
    Html,
    Txt,
    Markdown
}

/// <summary>Filename→format mapping + the InputFile accept strings (single home for both).</summary>
public static class ImportFormats
{
    /// <summary>Accept string for single-chapter modes (EPUB is inherently multi-chapter → bulk modes only).</summary>
    public const string AcceptSingle = ".docx,.html,.htm,.txt,.md,.markdown";

    /// <summary>Accept string for the one-file-per-chapter bulk mode.</summary>
    public const string AcceptBulkFiles = AcceptSingle;

    /// <summary>Accept string for the whole-document bulk mode.</summary>
    public const string AcceptDocument = ".docx,.html,.htm,.txt,.md,.markdown";

    /// <summary>Accept string for the EPUB mode.</summary>
    public const string AcceptEpub = ".epub";

    public static ImportFormat? TryFromFileName(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".docx" => ImportFormat.Docx,
            ".epub" => ImportFormat.Epub,
            ".html" or ".htm" or ".xhtml" => ImportFormat.Html,
            ".txt" => ImportFormat.Txt,
            ".md" or ".markdown" => ImportFormat.Markdown,
            _ => null
        };
}
