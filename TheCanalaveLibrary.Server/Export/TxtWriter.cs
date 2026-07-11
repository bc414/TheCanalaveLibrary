using System.Text;
using AngleSharp.Dom;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Plain-text export (WU38c). Strips all formatting but preserves the document's *shape*:
/// paragraph breaks, heading lines, list items, quoted blocks. The inverse reference is
/// <c>ChapterText.CountWords</c> (Core/Chapters) — this writer is what "readable words" look like.
/// </summary>
public static class TxtWriter
{
    public static byte[] Write(StoryExportModel story)
    {
        var sb = new StringBuilder();

        sb.AppendLine(story.Title);
        sb.AppendLine($"by {story.AuthorName}");
        sb.AppendLine($"Rated {story.RatingLabel}");
        sb.AppendLine($"Published {story.PublishDate:MMM d, yyyy} · Updated {story.LastUpdatedDate:MMM d, yyyy}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(story.LongDescriptionHtml))
        {
            AppendHtmlAsText(sb, story.LongDescriptionHtml);
            sb.AppendLine();
        }

        foreach (var chapter in story.Chapters)
        {
            string heading = $"Chapter {chapter.ChapterNumber}: {chapter.Title}";
            sb.AppendLine(new string('=', Math.Min(heading.Length, 72)));
            sb.AppendLine(heading);
            sb.AppendLine(new string('=', Math.Min(heading.Length, 72)));
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(chapter.TopAuthorsNote))
            {
                sb.AppendLine("[Author's note]");
                AppendHtmlAsText(sb, chapter.TopAuthorsNote);
            }

            AppendHtmlAsText(sb, chapter.HtmlContent);

            if (!string.IsNullOrWhiteSpace(chapter.BottomAuthorsNote))
            {
                sb.AppendLine("[Author's note]");
                AppendHtmlAsText(sb, chapter.BottomAuthorsNote);
            }
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static void AppendHtmlAsText(StringBuilder sb, string html)
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
            // Stray top-level text (rare in Quill output) — keep it as its own paragraph.
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
            case "H2":
            case "H3":
            {
                string text = ExportDom.InlineText(element).Trim();
                if (text.Length > 0)
                {
                    sb.AppendLine(text);
                    sb.AppendLine();
                }
                break;
            }
            case "BLOCKQUOTE":
            {
                string text = ExportDom.InlineText(element).Trim();
                if (text.Length > 0)
                {
                    foreach (string line in text.Split('\n'))
                    {
                        sb.Append("> ").AppendLine(line);
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
                    string text = ExportDom.InlineText(li).Trim();
                    sb.Append(ordered ? $"{index}. " : "- ").AppendLine(text);
                    index++;
                }
                sb.AppendLine();
                break;
            }
            default:
            {
                // Unknown/inline element at block level — degrade to text.
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
}
