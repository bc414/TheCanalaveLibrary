using System.Net;
using System.Text;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Plain text → HTML (WU38d). Fanfic .txt convention: blank line = paragraph break, single
/// newline = line break within a paragraph. All text is encoded — a .txt containing markup
/// characters is prose, not markup. Decoded through <see cref="ImportTextDecoder"/> (BOM, then strict
/// UTF-8, then Windows-1252 with a warning); the warning rides out beside the HTML like
/// <c>DocxReader</c>'s.
/// </summary>
public static class TxtReader
{
    public static (string Html, List<ImportWarning> Warnings) Read(Stream file)
    {
        (string decoded, ImportWarning? warning) = ImportTextDecoder.Decode(file);
        string text = decoded.Replace("\r\n", "\n").Replace('\r', '\n');

        var sb = new StringBuilder(text.Length + 256);
        foreach (string block in text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = block.Trim('\n');
            if (trimmed.Trim().Length == 0)
            {
                continue;
            }
            sb.Append("<p>")
              .Append(string.Join("<br>", trimmed.Split('\n').Select(WebUtility.HtmlEncode)))
              .Append("</p>");
        }
        return (sb.ToString(), warning is null ? [] : [warning]);
    }
}
