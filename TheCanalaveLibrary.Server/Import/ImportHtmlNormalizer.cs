using System.Net;
using System.Text;
using AngleSharp.Dom;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Rewrites reader output toward the 13-tag sanitizer allowlist BEFORE sanitization (WU38d).
/// Two-stage rationale: the sanitizer's default drops disallowed elements <em>with their
/// children</em> — running raw reader HTML straight through it would silently delete text inside
/// <c>h4</c>/<c>div</c>/<c>span</c> etc. This normalizer maps known equivalents
/// (<c>b→strong</c>, <c>h1→h2</c>, <c>h4+→h3</c>, containers unwrapped, tables flattened) and
/// counts what's genuinely lost (images), so the sanitizer's final pass — still always run, the
/// trust boundary — has almost nothing left to remove. <c>&lt;hr&gt;</c> survives normalization
/// deliberately: it's the page-break split marker (mode 4); per-segment sanitization strips it.
/// </summary>
public static class ImportHtmlNormalizer
{
    public sealed record Result(string Html, IReadOnlyList<ImportWarning> Warnings);

    private static readonly HashSet<string> PassThroughTags =
        ["P", "BLOCKQUOTE", "UL", "OL", "LI", "H2", "H3", "STRONG", "EM", "U", "S"];

    private static readonly HashSet<string> DropEntirelyTags =
        ["SCRIPT", "STYLE", "HEAD", "TITLE", "META", "LINK", "IFRAME", "OBJECT", "EMBED", "SVG", "NOSCRIPT"];

    private static readonly HashSet<string> BlockLevelTags =
        ["P", "DIV", "SECTION", "ARTICLE", "ASIDE", "FIGURE", "MAIN", "HEADER", "FOOTER", "NAV",
         "BLOCKQUOTE", "UL", "OL", "LI", "TABLE", "H1", "H2", "H3", "H4", "H5", "H6", "HR", "PRE"];

    public static Result Normalize(string rawHtml)
    {
        var counters = new Counters();
        var sb = new StringBuilder(rawHtml.Length);
        foreach (INode node in ExportDom.ParseFragment(rawHtml))
        {
            EmitNode(sb, node, counters);
        }

        var warnings = new List<ImportWarning>();
        if (counters.ImagesDropped > 0)
        {
            warnings.Add(new ImportWarning(ImportWarningKind.ImagesDropped,
                $"{counters.ImagesDropped} image{(counters.ImagesDropped == 1 ? "" : "s")} dropped — chapters don't support embedded images."));
        }
        if (counters.DeepHeadingsDemoted > 0)
        {
            warnings.Add(new ImportWarning(ImportWarningKind.UnsupportedFormatting,
                "Headings deeper than two levels were flattened to the smallest supported heading."));
        }
        if (counters.TablesFlattened > 0)
        {
            warnings.Add(new ImportWarning(ImportWarningKind.UnsupportedFormatting,
                $"{counters.TablesFlattened} table{(counters.TablesFlattened == 1 ? "" : "s")} flattened to plain paragraphs — chapters don't support tables."));
        }

        return new Result(sb.ToString(), warnings);
    }

    private sealed class Counters
    {
        public int ImagesDropped;
        public int DeepHeadingsDemoted;
        public int TablesFlattened;
    }

    private static void EmitNode(StringBuilder sb, INode node, Counters counters)
    {
        if (node.NodeType == NodeType.Text)
        {
            sb.Append(WebUtility.HtmlEncode(node.TextContent));
            return;
        }

        if (node is not IElement element)
        {
            return; // comments, processing instructions — dropped
        }

        string tag = element.TagName;

        if (DropEntirelyTags.Contains(tag))
        {
            return;
        }

        switch (tag)
        {
            case "BR":
                sb.Append("<br>");
                return;
            case "HR":
                sb.Append("<hr>"); // split marker — kept through normalization, stripped per-segment
                return;
            case "IMG":
                counters.ImagesDropped++;
                return;
            case "A":
            {
                string? href = element.GetAttribute("href");
                sb.Append(href is null ? "<a>" : $"<a href=\"{WebUtility.HtmlEncode(href)}\">");
                EmitChildren(sb, element, counters);
                sb.Append("</a>");
                return;
            }
            case "B":
                EmitWrapped(sb, "strong", element, counters);
                return;
            case "I":
                EmitWrapped(sb, "em", element, counters);
                return;
            case "STRIKE":
            case "DEL":
                EmitWrapped(sb, "s", element, counters);
                return;
            case "INS":
                EmitWrapped(sb, "u", element, counters);
                return;
            case "H1":
                EmitWrapped(sb, "h2", element, counters);
                return;
            case "H4":
            case "H5":
            case "H6":
                counters.DeepHeadingsDemoted++;
                EmitWrapped(sb, "h3", element, counters);
                return;
            case "TABLE":
            {
                counters.TablesFlattened++;
                foreach (IElement row in element.QuerySelectorAll("tr"))
                {
                    string rowText = string.Join(" · ", row.Children
                        .Where(c => c.TagName is "TD" or "TH")
                        .Select(c => c.TextContent.Trim())
                        .Where(t => t.Length > 0));
                    if (rowText.Length > 0)
                    {
                        sb.Append("<p>").Append(WebUtility.HtmlEncode(rowText)).Append("</p>");
                    }
                }
                return;
            }
            case "PRE":
                // No code blocks in the allowlist — keep the text as a paragraph.
                sb.Append("<p>").Append(WebUtility.HtmlEncode(element.TextContent.Trim())).Append("</p>");
                return;
        }

        if (PassThroughTags.Contains(tag))
        {
            string lower = tag.ToLowerInvariant();
            sb.Append('<').Append(lower).Append('>');
            EmitChildren(sb, element, counters);
            sb.Append("</").Append(lower).Append('>');
            return;
        }

        // Container blocks (div/section/…): unwrap when they hold block children; otherwise the
        // content is inline-only — wrap it in a paragraph so text doesn't float bare.
        if (BlockLevelTags.Contains(tag))
        {
            bool hasBlockChild = element.Children.Any(c => BlockLevelTags.Contains(c.TagName));
            if (hasBlockChild)
            {
                EmitChildren(sb, element, counters);
            }
            else if (element.TextContent.Trim().Length > 0)
            {
                sb.Append("<p>");
                EmitChildren(sb, element, counters);
                sb.Append("</p>");
            }
            return;
        }

        // Unknown inline (span/font/…): unwrap, keep children.
        EmitChildren(sb, element, counters);
    }

    private static void EmitWrapped(StringBuilder sb, string tag, IElement element, Counters counters)
    {
        sb.Append('<').Append(tag).Append('>');
        EmitChildren(sb, element, counters);
        sb.Append("</").Append(tag).Append('>');
    }

    private static void EmitChildren(StringBuilder sb, IElement element, Counters counters)
    {
        foreach (INode child in element.ChildNodes)
        {
            EmitNode(sb, child, counters);
        }
    }
}
