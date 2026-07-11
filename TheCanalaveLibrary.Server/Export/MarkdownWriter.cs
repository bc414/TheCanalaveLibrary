using System.Text;
using AngleSharp.Dom;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Markdown export (WU38c). Allowlist→MD mapping: <c>strong→**</c>, <c>em→*</c>, <c>s→~~</c>,
/// <c>h2→##</c>, <c>h3→###</c>, <c>blockquote→&gt;</c>, <c>ul→- </c>, <c>ol→1. </c>,
/// <c>a→[text](href)</c>, <c>br→</c> hard break. <c>u</c> has no Markdown equivalent → plain text.
/// Prose is deliberately NOT MD-escaped (readability wins; stray <c>*</c>/<c>_</c> in fiction prose
/// is rare and harmless in most renderers).
/// </summary>
public static class MarkdownWriter
{
    public static byte[] Write(StoryExportModel story)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# {story.Title}");
        sb.AppendLine();
        sb.AppendLine($"*by {story.AuthorName} · Rated {story.RatingLabel} · " +
                      $"Published {story.PublishDate:yyyy-MM-dd} · Updated {story.LastUpdatedDate:yyyy-MM-dd}*");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(story.LongDescriptionHtml))
        {
            AppendHtmlAsMarkdown(sb, story.LongDescriptionHtml);
        }

        foreach (var chapter in story.Chapters)
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"## Chapter {chapter.ChapterNumber}: {chapter.Title}");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(chapter.TopAuthorsNote))
            {
                sb.AppendLine("*Author's note:*");
                sb.AppendLine();
                AppendHtmlAsMarkdown(sb, chapter.TopAuthorsNote);
            }

            AppendHtmlAsMarkdown(sb, chapter.HtmlContent);

            if (!string.IsNullOrWhiteSpace(chapter.BottomAuthorsNote))
            {
                sb.AppendLine("*Author's note:*");
                sb.AppendLine();
                AppendHtmlAsMarkdown(sb, chapter.BottomAuthorsNote);
            }
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static void AppendHtmlAsMarkdown(StringBuilder sb, string html)
    {
        foreach (INode node in ExportDom.ParseFragment(html))
        {
            AppendBlock(sb, node);
        }
    }

    private static void AppendBlock(StringBuilder sb, INode node)
    {
        if (node is not IElement element)
        {
            string text = node.TextContent.Trim();
            if (text.Length > 0)
            {
                sb.AppendLine(text);
                sb.AppendLine();
            }
            return;
        }

        switch (element.TagName)
        {
            case "P":
            {
                string text = Inline(element).Trim();
                if (text.Length > 0)
                {
                    sb.AppendLine(text);
                    sb.AppendLine();
                }
                break;
            }
            case "H2":
                sb.AppendLine($"## {Inline(element).Trim()}");
                sb.AppendLine();
                break;
            case "H3":
                sb.AppendLine($"### {Inline(element).Trim()}");
                sb.AppendLine();
                break;
            case "BLOCKQUOTE":
            {
                string text = Inline(element).Trim();
                if (text.Length > 0)
                {
                    foreach (string line in text.Split('\n'))
                    {
                        sb.Append("> ").AppendLine(line.TrimEnd());
                    }
                    sb.AppendLine();
                }
                break;
            }
            case "UL":
            case "OL":
            {
                bool ordered = element.TagName == "OL";
                int index = 1;
                foreach (IElement li in element.Children.Where(c => c.TagName == "LI"))
                {
                    sb.Append(ordered ? $"{index}. " : "- ").AppendLine(Inline(li).Trim());
                    index++;
                }
                sb.AppendLine();
                break;
            }
            default:
            {
                string text = element.TextContent.Trim();
                if (text.Length > 0)
                {
                    sb.AppendLine(text);
                    sb.AppendLine();
                }
                break;
            }
        }
    }

    /// <summary>Renders an element's inline content as Markdown (recursive).</summary>
    private static string Inline(INode node)
    {
        var sb = new StringBuilder();
        foreach (INode child in node.ChildNodes)
        {
            switch (child)
            {
                case IElement { TagName: "BR" }:
                    // Markdown hard break: two trailing spaces + newline.
                    sb.Append("  \n");
                    break;
                case IElement { TagName: "STRONG" } el:
                    sb.Append("**").Append(Inline(el)).Append("**");
                    break;
                case IElement { TagName: "EM" } el:
                    sb.Append('*').Append(Inline(el)).Append('*');
                    break;
                case IElement { TagName: "S" } el:
                    sb.Append("~~").Append(Inline(el)).Append("~~");
                    break;
                case IElement { TagName: "U" } el:
                    // No Markdown equivalent — emit plain text (documented lossy corner).
                    sb.Append(Inline(el));
                    break;
                case IElement { TagName: "A" } el:
                    sb.Append('[').Append(Inline(el)).Append("](")
                      .Append(el.GetAttribute("href") ?? string.Empty).Append(')');
                    break;
                case IElement el:
                    sb.Append(Inline(el));
                    break;
                default:
                    if (child.NodeType == NodeType.Text)
                    {
                        sb.Append(child.TextContent);
                    }
                    break;
            }
        }
        return sb.ToString();
    }
}
