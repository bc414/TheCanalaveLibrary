using System.Net;
using System.Text;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Single-file HTML export (WU38c). The stored chapter HTML is already sanitized allowlist markup
/// (sanitize-once-on-save), so chapter bodies are embedded verbatim — only metadata strings we
/// compose ourselves are encoded. Self-contained: minimal inline CSS, no external references.
/// </summary>
public static class HtmlWriter
{
    public static byte[] Write(StoryExportModel story)
    {
        var sb = new StringBuilder();

        sb.Append("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        sb.Append("<title>").Append(Encode(story.Title)).Append("</title>\n");
        sb.Append("<style>\n");
        sb.Append("body { font-family: Georgia, 'Times New Roman', serif; max-width: 42em; margin: 2em auto; padding: 0 1em; line-height: 1.6; color: #222; }\n");
        sb.Append("h1, h2, h3 { line-height: 1.25; }\n");
        sb.Append("blockquote { border-left: 3px solid #999; margin-left: 0; padding-left: 1em; color: #444; }\n");
        sb.Append(".meta { color: #666; font-size: 0.9em; }\n");
        sb.Append(".authors-note { font-size: 0.9em; color: #555; border: 1px solid #ccc; border-radius: 4px; padding: 0.5em 1em; margin: 1em 0; }\n");
        sb.Append("hr.chapter-break { margin: 3em 0; border: none; border-top: 1px solid #ccc; }\n");
        sb.Append("</style>\n</head>\n<body>\n");

        // Title block
        sb.Append("<h1>").Append(Encode(story.Title)).Append("</h1>\n");
        sb.Append("<p class=\"meta\">by ").Append(Encode(story.AuthorName))
          .Append(" · Rated ").Append(Encode(story.RatingLabel))
          .Append(" · Published ").Append(story.PublishDate.ToString("MMM d, yyyy"))
          .Append(" · Updated ").Append(story.LastUpdatedDate.ToString("MMM d, yyyy"))
          .Append("</p>\n");

        if (!string.IsNullOrWhiteSpace(story.LongDescriptionHtml))
        {
            sb.Append("<div class=\"description\">\n").Append(story.LongDescriptionHtml).Append("\n</div>\n");
        }

        foreach (var chapter in story.Chapters)
        {
            sb.Append("<hr class=\"chapter-break\">\n");
            sb.Append("<h2>Chapter ").Append(chapter.ChapterNumber).Append(": ")
              .Append(Encode(chapter.Title)).Append("</h2>\n");

            if (!string.IsNullOrWhiteSpace(chapter.TopAuthorsNote))
            {
                sb.Append("<div class=\"authors-note\">\n").Append(chapter.TopAuthorsNote).Append("\n</div>\n");
            }

            sb.Append(chapter.HtmlContent).Append('\n');

            if (!string.IsNullOrWhiteSpace(chapter.BottomAuthorsNote))
            {
                sb.Append("<div class=\"authors-note\">\n").Append(chapter.BottomAuthorsNote).Append("\n</div>\n");
            }
        }

        sb.Append("</body>\n</html>\n");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Encode(string text) => WebUtility.HtmlEncode(text);
}
