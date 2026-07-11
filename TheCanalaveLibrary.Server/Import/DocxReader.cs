using Mammoth;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// DOCX → HTML via Mammoth (WU38d) — semantic conversion, the same philosophy as the allowlist:
/// styles map to meaning, presentation junk is discarded. A downloaded Google Doc is a .docx —
/// identical path. Style map targets the import contract (audit/Import.md): source
/// <c>Heading 1→h2</c>, deeper headings→<c>h3</c>, page breaks→<c>hr</c> (the mode-4 split
/// marker). Images come out as inline base64 <c>img</c>s and are counted+dropped by
/// <see cref="ImportHtmlNormalizer"/>. Mammoth's own conversion messages surface as
/// <see cref="ImportWarningKind.UnrecognizedStyle"/> warnings.
/// </summary>
public static class DocxReader
{
    private const string StyleMap =
        """
        p[style-name='Heading 1'] => h2:fresh
        p[style-name='Heading 2'] => h3:fresh
        p[style-name='Heading 3'] => h3:fresh
        p[style-name='Title'] => h2:fresh
        p[style-name='Subtitle'] => h3:fresh
        p[style-name='Quote'] => blockquote:fresh
        p[style-name='Intense Quote'] => blockquote:fresh
        u => u
        br[type='page'] => hr
        """;

    public static (string RawHtml, List<ImportWarning> Warnings) Read(Stream file)
    {
        var converter = new DocumentConverter().AddStyleMap(StyleMap);

        IResult<string> result;
        try
        {
            result = converter.ConvertToHtml(file);
        }
        catch (Exception ex)
        {
            throw new ImportException(
                "This file couldn't be read as a Word document (.docx). If it came from Google Docs, " +
                "use File → Download → Microsoft Word (.docx).", ex);
        }

        List<ImportWarning> warnings = result.Warnings
            .Distinct()
            .Select(w => new ImportWarning(ImportWarningKind.UnrecognizedStyle, w))
            .ToList();

        return (result.Value, warnings);
    }
}
