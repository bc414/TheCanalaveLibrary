using AngleSharp.Html.Parser;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// HTML file → body HTML (WU38d). Nearly free: AO3 exports single-file HTML; the body's inner
/// markup goes straight to the normalizer+sanitizer pipeline. Full-document parse (files carry
/// <c>&lt;html&gt;&lt;head&gt;</c>), not the fragment parse the DOM walks use.
/// </summary>
public static class HtmlFileReader
{
    public static string Read(Stream file)
    {
        using var reader = new StreamReader(file);
        string html = reader.ReadToEnd();

        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);
        return document.Body?.InnerHtml ?? string.Empty;
    }
}
