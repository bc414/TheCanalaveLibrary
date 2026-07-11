using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
// QuestPDF also defines IElement/Document — alias the AngleSharp DOM types explicitly.
using Document = QuestPDF.Fluent.Document;
using IElement = AngleSharp.Dom.IElement;
using INode = AngleSharp.Dom.INode;
using NodeType = AngleSharp.Dom.NodeType;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// PDF export (WU38c) via QuestPDF. The fixed 13-tag sanitizer allowlist is what makes direct
/// HTML→PDF mapping tractable without a headless browser: block elements become column items,
/// inline elements become styled text spans (strong→Bold, em→Italic, u→Underline,
/// s→Strikethrough, a→hyperlink), lists become bullet/number rows, blockquote indents.
/// </summary>
public static class PdfWriter
{
    static PdfWriter()
    {
        // Community license: free under $1M annual revenue (see the csproj note). Set in the
        // static ctor rather than Program.cs so every entry path — app, integration tests,
        // direct unit-test writer calls — is covered by the single assignment.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private const float BlockSpacing = 8f;

    public static byte[] Write(StoryExportModel story)
    {
        Document document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(54);
                page.DefaultTextStyle(style => style.FontSize(11));

                page.Content().Column(col =>
                {
                    // Title block
                    col.Item().PaddingTop(140).AlignCenter().Text(story.Title).FontSize(28).Bold();
                    col.Item().PaddingTop(12).AlignCenter().Text($"by {story.AuthorName}").FontSize(14);
                    col.Item().PaddingTop(4).AlignCenter()
                       .Text($"Rated {story.RatingLabel} · Published {story.PublishDate:MMM d, yyyy} · Updated {story.LastUpdatedDate:MMM d, yyyy}")
                       .FontSize(10).FontColor(Colors.Grey.Darken1);

                    if (!string.IsNullOrWhiteSpace(story.LongDescriptionHtml))
                    {
                        col.Item().PaddingTop(24);
                        RenderHtmlBlocks(col, story.LongDescriptionHtml);
                    }

                    foreach (var chapter in story.Chapters)
                    {
                        col.Item().PageBreak();
                        col.Item().PaddingBottom(12)
                           .Text($"Chapter {chapter.ChapterNumber}: {chapter.Title}")
                           .FontSize(18).Bold();

                        if (!string.IsNullOrWhiteSpace(chapter.TopAuthorsNote))
                        {
                            RenderAuthorsNote(col, chapter.TopAuthorsNote);
                        }

                        RenderHtmlBlocks(col, chapter.HtmlContent);

                        if (!string.IsNullOrWhiteSpace(chapter.BottomAuthorsNote))
                        {
                            RenderAuthorsNote(col, chapter.BottomAuthorsNote);
                        }
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(9).FontColor(Colors.Grey.Darken1));
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void RenderAuthorsNote(ColumnDescriptor col, string noteHtml)
    {
        col.Item().PaddingBottom(BlockSpacing)
           .Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8)
           .Column(inner =>
           {
               inner.Item().Text("Author's note").FontSize(9).Italic().FontColor(Colors.Grey.Darken1);
               RenderHtmlBlocks(inner, noteHtml);
           });
    }

    private static void RenderHtmlBlocks(ColumnDescriptor col, string html)
    {
        foreach (INode node in ExportDom.ParseFragment(html))
        {
            RenderBlock(col, node);
        }
    }

    private static void RenderBlock(ColumnDescriptor col, INode node)
    {
        if (node is not IElement element)
        {
            string stray = node.TextContent.Trim();
            if (stray.Length > 0)
            {
                col.Item().PaddingBottom(BlockSpacing).Text(stray);
            }
            return;
        }

        switch (element.TagName)
        {
            case "P":
                col.Item().PaddingBottom(BlockSpacing)
                   .Text(text => RenderInlines(text, element, default));
                break;
            case "H2":
                col.Item().PaddingTop(6).PaddingBottom(BlockSpacing)
                   .Text(text =>
                   {
                       text.DefaultTextStyle(style => style.FontSize(15).Bold());
                       RenderInlines(text, element, default);
                   });
                break;
            case "H3":
                col.Item().PaddingTop(4).PaddingBottom(BlockSpacing)
                   .Text(text =>
                   {
                       text.DefaultTextStyle(style => style.FontSize(13).Bold());
                       RenderInlines(text, element, default);
                   });
                break;
            case "BLOCKQUOTE":
                col.Item().PaddingBottom(BlockSpacing)
                   .BorderLeft(2).BorderColor(Colors.Grey.Medium).PaddingLeft(10)
                   .Text(text =>
                   {
                       text.DefaultTextStyle(style => style.FontColor(Colors.Grey.Darken2).Italic());
                       RenderInlines(text, element, default);
                   });
                break;
            case "UL":
            case "OL":
            {
                bool ordered = element.TagName == "OL";
                int index = 1;
                foreach (IElement li in element.Children.Where(c => c.TagName == "LI"))
                {
                    string marker = ordered ? $"{index}." : "•";
                    col.Item().PaddingBottom(2).Row(row =>
                    {
                        row.ConstantItem(18).Text(marker);
                        row.RelativeItem().Text(text => RenderInlines(text, li, default));
                    });
                    index++;
                }
                col.Item().PaddingBottom(BlockSpacing - 2);
                break;
            }
            default:
            {
                string text = element.TextContent.Trim();
                if (text.Length > 0)
                {
                    col.Item().PaddingBottom(BlockSpacing).Text(text);
                }
                break;
            }
        }
    }

    private readonly record struct InlineStyle(bool Bold, bool Italic, bool Underline, bool Strike);

    private static void RenderInlines(TextDescriptor text, INode node, InlineStyle style)
    {
        foreach (INode child in node.ChildNodes)
        {
            switch (child)
            {
                case IElement { TagName: "BR" }:
                    text.Span("\n");
                    break;
                case IElement { TagName: "STRONG" } el:
                    RenderInlines(text, el, style with { Bold = true });
                    break;
                case IElement { TagName: "EM" } el:
                    RenderInlines(text, el, style with { Italic = true });
                    break;
                case IElement { TagName: "U" } el:
                    RenderInlines(text, el, style with { Underline = true });
                    break;
                case IElement { TagName: "S" } el:
                    RenderInlines(text, el, style with { Strike = true });
                    break;
                case IElement { TagName: "A" } el:
                {
                    // Quill links contain plain text; render as a styled hyperlink span.
                    string href = el.GetAttribute("href") ?? string.Empty;
                    var span = href.Length > 0
                        ? text.Hyperlink(el.TextContent, href)
                        : text.Span(el.TextContent);
                    Apply(span.Underline().FontColor(Colors.Blue.Darken2), style with { Underline = false });
                    break;
                }
                case IElement el:
                    RenderInlines(text, el, style);
                    break;
                default:
                    if (child.NodeType == NodeType.Text)
                    {
                        Apply(text.Span(child.TextContent), style);
                    }
                    break;
            }
        }
    }

    private static void Apply(TextSpanDescriptor span, InlineStyle style)
    {
        if (style.Bold) span = span.Bold();
        if (style.Italic) span = span.Italic();
        if (style.Underline) span = span.Underline();
        if (style.Strike) span.Strikethrough();
    }
}
