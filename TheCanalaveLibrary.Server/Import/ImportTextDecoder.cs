using System.Text;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Decodes an imported text file (TXT, HTML, Markdown) without garbling a legacy encoding
/// (layer2-services.md §"Export &amp; Import" → "Text decoding"; WU-ParityAndRemaining, 2026-10-13 —
/// service audit §2.12). Order: a byte-order mark wins; otherwise strict UTF-8; and if the bytes are not
/// valid UTF-8, the file is re-read as Windows-1252 with an
/// <see cref="ImportWarningKind.EncodingFallback"/> warning. Before this, a <c>StreamReader</c> decoded
/// leniently, so a Notepad/Word file's curly quotes (0x93/0x94) became U+FFFD with no warning at all,
/// against the import doctrine that a lossy step always tells the author.
/// <para>Windows-1252 is the implementer's default: it is the de facto legacy encoding of the
/// Word/Notepad files fanfiction comes from, and every byte decodes to something, so the worst case is
/// a wrong accented letter the warning tells the author to check — never worse than the replacement
/// characters it replaces.</para>
/// </summary>
public static class ImportTextDecoder
{
    /// <summary>The warning shown when the UTF-8 read fails and Windows-1252 is used instead.</summary>
    public const string EncodingFallbackMessage =
        "This file wasn't saved as UTF-8, so it was read as Windows-1252 (Western European). Check " +
        "accented letters, curly quotes and dashes before saving.";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly Encoding Windows1252;

    static ImportTextDecoder()
    {
        // Code page 1252 is not built into .NET Core; Encoding.GetEncoding(1252) throws until the
        // provider is registered. Registering is process-wide and idempotent.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Windows1252 = Encoding.GetEncoding(1252);
    }

    /// <summary>
    /// Reads the whole stream and decodes it. Returns the text and, when the Windows-1252 fallback was
    /// used, the warning to surface; null otherwise.
    /// </summary>
    public static (string Text, ImportWarning? Warning) Decode(Stream file)
    {
        byte[] bytes;
        using (MemoryStream copy = new())
        {
            file.CopyTo(copy);
            bytes = copy.ToArray();
        }

        // A byte-order mark is an explicit declaration: honor it (UTF-8, UTF-16 LE/BE, UTF-32).
        if (TryDecodeWithBom(bytes, out string? withBom))
            return (withBom!, null);

        try
        {
            return (StrictUtf8.GetString(bytes), null);
        }
        catch (DecoderFallbackException)
        {
            // Not valid UTF-8 — translated, not swallowed: the fallback below is the handling, and the
            // author is told through the returned warning.
            return (Windows1252.GetString(bytes),
                new ImportWarning(ImportWarningKind.EncodingFallback, EncodingFallbackMessage));
        }
    }

    private static bool TryDecodeWithBom(byte[] bytes, out string? text)
    {
        (byte[] Preamble, Encoding Encoding)[] boms =
        [
            ([0xFF, 0xFE, 0x00, 0x00], new UTF32Encoding(bigEndian: false, byteOrderMark: true)),
            ([0x00, 0x00, 0xFE, 0xFF], new UTF32Encoding(bigEndian: true, byteOrderMark: true)),
            ([0xEF, 0xBB, 0xBF], new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)),
            ([0xFF, 0xFE], new UnicodeEncoding(bigEndian: false, byteOrderMark: true)),
            ([0xFE, 0xFF], new UnicodeEncoding(bigEndian: true, byteOrderMark: true)),
        ];
        foreach ((byte[] preamble, Encoding encoding) in boms)
        {
            if (bytes.AsSpan().StartsWith(preamble))
            {
                text = encoding.GetString(bytes, preamble.Length, bytes.Length - preamble.Length);
                return true;
            }
        }
        text = null;
        return false;
    }
}
