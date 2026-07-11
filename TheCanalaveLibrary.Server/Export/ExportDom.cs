using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Shared AngleSharp helpers for the export writers' DOM walks (WU38c). Input is always sanitized
/// allowlist HTML (13 tags — layer2-services.md §"Export &amp; Import"), so walkers only need to
/// handle that closed set; anything else degrades to its text content.
/// </summary>
public static class ExportDom
{
    /// <summary>Parses an HTML fragment (chapter body) into its top-level nodes.</summary>
    public static INodeList ParseFragment(string html)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument("<html><body></body></html>");
        return parser.ParseFragment(html, document.Body!);
    }

    /// <summary>
    /// Text content of an inline subtree with <c>&lt;br&gt;</c> rendered as a newline
    /// (<c>TextContent</c> alone drops line breaks).
    /// </summary>
    public static string InlineText(INode node)
    {
        var sb = new StringBuilder();
        AppendInlineText(sb, node);
        return sb.ToString();
    }

    private static void AppendInlineText(StringBuilder sb, INode node)
    {
        foreach (INode child in node.ChildNodes)
        {
            if (child is IElement { TagName: "BR" })
            {
                sb.Append('\n');
            }
            else if (child.NodeType == NodeType.Text)
            {
                sb.Append(child.TextContent);
            }
            else
            {
                AppendInlineText(sb, child);
            }
        }
    }
}
