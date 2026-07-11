namespace TheCanalaveLibrary.Core;

/// <summary>
/// Download formats for story export (Feature 54, WU38c). Each value has exactly one writer in
/// <c>Server/Export/</c>; adding a format = one enum value + one writer + one dispatch arm in
/// <c>ServerExportService</c> (see <c>audit/Export.md</c> "Settled"). MOBI deliberately absent
/// (obsolete — Kindle ingests EPUB directly). PDF <em>import</em> is a different feature
/// (<c>audit/Import.md</c>).
/// </summary>
public enum ExportFormat
{
    Epub,
    Pdf,
    Html,
    Txt,
    Markdown,
    Docx
}
