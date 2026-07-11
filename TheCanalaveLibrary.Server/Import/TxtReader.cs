using System.Net;
using System.Text;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Plain text → HTML (WU38d). Fanfic .txt convention: blank line = paragraph break, single
/// newline = line break within a paragraph. All text is encoded — a .txt containing markup
/// characters is prose, not markup.
/// </summary>
public static class TxtReader
{
    public static string Read(Stream file)
    {
        using var reader = new StreamReader(file);
        string text = reader.ReadToEnd().Replace("\r\n", "\n").Replace('\r', '\n');

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
        return sb.ToString();
    }
}
