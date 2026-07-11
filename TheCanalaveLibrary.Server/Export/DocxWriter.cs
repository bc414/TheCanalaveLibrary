using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
// Wordprocessing also defines Document/Text etc. — alias the AngleSharp DOM types explicitly.
using IElement = AngleSharp.Dom.IElement;
using INode = AngleSharp.Dom.INode;
using NodeType = AngleSharp.Dom.NodeType;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// DOCX export (WU38c) via the Open XML SDK. Uses REAL Word constructs, not lookalikes — named
/// heading styles (so Word's navigation pane works and Mammoth-based re-import sees headings),
/// hyperlink relationships, and a numbering part for lists. Style mapping: story title → "Title",
/// chapter titles → "Heading 1", content <c>h2→"Heading 2"</c>, <c>h3→"Heading 3"</c> (the import
/// side maps Heading 1→h2, Heading 2/3→h3 — the one-level demotion on a full round-trip is the
/// documented cost of DOCX having 9 heading levels to our 2).
/// </summary>
public static class DocxWriter
{
    public static byte[] Write(StoryExportModel story)
    {
        using var buffer = new MemoryStream();
        using (var word = WordprocessingDocument.Create(buffer, WordprocessingDocumentType.Document))
        {
            MainDocumentPart main = word.AddMainDocumentPart();
            main.Document = new Document();
            AddStyles(main);
            NumberingDefinitionsPart numbering = AddNumbering(main);

            var body = new Body();
            var context = new DocxContext(main, numbering);

            // Title page
            body.Append(StyledParagraph("Title", story.Title));
            body.Append(PlainParagraph($"by {story.AuthorName}"));
            body.Append(PlainParagraph(
                $"Rated {story.RatingLabel} · Published {story.PublishDate:MMM d, yyyy} · Updated {story.LastUpdatedDate:MMM d, yyyy}"));
            if (!string.IsNullOrWhiteSpace(story.LongDescriptionHtml))
            {
                AppendHtmlBlocks(body, story.LongDescriptionHtml, context);
            }

            foreach (var chapter in story.Chapters)
            {
                body.Append(PageBreakParagraph());
                body.Append(StyledParagraph("Heading1", $"Chapter {chapter.ChapterNumber}: {chapter.Title}"));

                if (!string.IsNullOrWhiteSpace(chapter.TopAuthorsNote))
                {
                    body.Append(ItalicParagraph("Author's note:"));
                    AppendHtmlBlocks(body, chapter.TopAuthorsNote, context);
                }

                AppendHtmlBlocks(body, chapter.HtmlContent, context);

                if (!string.IsNullOrWhiteSpace(chapter.BottomAuthorsNote))
                {
                    body.Append(ItalicParagraph("Author's note:"));
                    AppendHtmlBlocks(body, chapter.BottomAuthorsNote, context);
                }
            }

            main.Document.Append(body);
            main.Document.Save();
        }

        return buffer.ToArray();
    }

    /// <summary>Walker state: the part (for hyperlink relationships) + per-OL numbering ids.</summary>
    private sealed class DocxContext(MainDocumentPart main, NumberingDefinitionsPart numbering)
    {
        public MainDocumentPart Main { get; } = main;
        public NumberingDefinitionsPart Numbering { get; } = numbering;
        private int _nextNumberId = 2; // 1 is the shared bullet instance

        /// <summary>
        /// Each ordered list gets its own NumberingInstance so numbering restarts at 1 per list
        /// (a single shared instance would continue counting across lists).
        /// </summary>
        public int CreateOrderedListInstance()
        {
            int id = _nextNumberId++;
            Numbering.Numbering!.Append(
                new NumberingInstance(new AbstractNumId { Val = 2 }) { NumberID = id });
            return id;
        }
    }

    // ── Block walk ───────────────────────────────────────────────────────────────

    private static void AppendHtmlBlocks(Body body, string html, DocxContext context)
    {
        foreach (INode node in ExportDom.ParseFragment(html))
        {
            AppendBlock(body, node, context);
        }
    }

    private static void AppendBlock(Body body, INode node, DocxContext context)
    {
        if (node is not IElement element)
        {
            string stray = node.TextContent.Trim();
            if (stray.Length > 0)
            {
                body.Append(PlainParagraph(stray));
            }
            return;
        }

        switch (element.TagName)
        {
            case "P":
                body.Append(InlineParagraph(element, context, paragraphProperties: null));
                break;
            case "H2":
                body.Append(InlineParagraph(element, context,
                    new ParagraphProperties(new ParagraphStyleId { Val = "Heading2" })));
                break;
            case "H3":
                body.Append(InlineParagraph(element, context,
                    new ParagraphProperties(new ParagraphStyleId { Val = "Heading3" })));
                break;
            case "BLOCKQUOTE":
                body.Append(InlineParagraph(element, context,
                    new ParagraphProperties(new Indentation { Left = "720" })));
                break;
            case "UL":
            {
                foreach (IElement li in element.Children.Where(c => c.TagName == "LI"))
                {
                    body.Append(InlineParagraph(li, context, ListParagraphProperties(numberId: 1)));
                }
                break;
            }
            case "OL":
            {
                int numberId = context.CreateOrderedListInstance();
                foreach (IElement li in element.Children.Where(c => c.TagName == "LI"))
                {
                    body.Append(InlineParagraph(li, context, ListParagraphProperties(numberId)));
                }
                break;
            }
            default:
            {
                string text = element.TextContent.Trim();
                if (text.Length > 0)
                {
                    body.Append(PlainParagraph(text));
                }
                break;
            }
        }
    }

    private static ParagraphProperties ListParagraphProperties(int numberId) =>
        new(new NumberingProperties(
            new NumberingLevelReference { Val = 0 },
            new NumberingId { Val = numberId }));

    // ── Inline walk ──────────────────────────────────────────────────────────────

    private readonly record struct InlineStyle(bool Bold, bool Italic, bool Underline, bool Strike);

    private static Paragraph InlineParagraph(
        IElement element, DocxContext context, ParagraphProperties? paragraphProperties)
    {
        var paragraph = new Paragraph();
        if (paragraphProperties is not null)
        {
            paragraph.Append(paragraphProperties);
        }
        AppendInlines(paragraph, element, context, default);
        return paragraph;
    }

    private static void AppendInlines(
        Paragraph paragraph, INode node, DocxContext context, InlineStyle style)
    {
        foreach (INode child in node.ChildNodes)
        {
            switch (child)
            {
                case IElement { TagName: "BR" }:
                    paragraph.Append(new Run(new Break()));
                    break;
                case IElement { TagName: "STRONG" } el:
                    AppendInlines(paragraph, el, context, style with { Bold = true });
                    break;
                case IElement { TagName: "EM" } el:
                    AppendInlines(paragraph, el, context, style with { Italic = true });
                    break;
                case IElement { TagName: "U" } el:
                    AppendInlines(paragraph, el, context, style with { Underline = true });
                    break;
                case IElement { TagName: "S" } el:
                    AppendInlines(paragraph, el, context, style with { Strike = true });
                    break;
                case IElement { TagName: "A" } el:
                {
                    string href = el.GetAttribute("href") ?? string.Empty;
                    Run linkRun = MakeRun(el.TextContent, style, hyperlinkStyled: true);
                    if (Uri.TryCreate(href, UriKind.Absolute, out Uri? uri))
                    {
                        HyperlinkRelationship rel = context.Main.AddHyperlinkRelationship(uri, isExternal: true);
                        paragraph.Append(new Hyperlink(linkRun) { Id = rel.Id });
                    }
                    else
                    {
                        paragraph.Append(linkRun); // unlinkable href — keep the text, drop the link
                    }
                    break;
                }
                case IElement el:
                    AppendInlines(paragraph, el, context, style);
                    break;
                default:
                    if (child.NodeType == NodeType.Text && child.TextContent.Length > 0)
                    {
                        paragraph.Append(MakeRun(child.TextContent, style, hyperlinkStyled: false));
                    }
                    break;
            }
        }
    }

    private static Run MakeRun(string text, InlineStyle style, bool hyperlinkStyled)
    {
        var props = new RunProperties();
        if (style.Bold) props.Append(new Bold());
        if (style.Italic) props.Append(new Italic());
        if (style.Underline || hyperlinkStyled) props.Append(new Underline { Val = UnderlineValues.Single });
        if (style.Strike) props.Append(new Strike());
        if (hyperlinkStyled) props.Append(new Color { Val = "0563C1" });

        var run = new Run();
        if (props.HasChildren) run.Append(props);
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    // ── Simple paragraph helpers ────────────────────────────────────────────────

    private static Paragraph StyledParagraph(string styleId, string text) =>
        new(new ParagraphProperties(new ParagraphStyleId { Val = styleId }),
            new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    private static Paragraph PlainParagraph(string text) =>
        new(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    private static Paragraph ItalicParagraph(string text) =>
        new(new Run(
            new RunProperties(new Italic()),
            new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    private static Paragraph PageBreakParagraph() =>
        new(new Run(new Break { Type = BreakValues.Page }));

    // ── Parts ────────────────────────────────────────────────────────────────────

    private static void AddStyles(MainDocumentPart main)
    {
        var stylesPart = main.AddNewPart<StyleDefinitionsPart>();
        stylesPart.Styles = new Styles(
            // Document defaults: 11pt body, spacing after paragraphs.
            new DocDefaults(
                new RunPropertiesDefault(new RunPropertiesBaseStyle(new FontSize { Val = "22" })),
                new ParagraphPropertiesDefault(new ParagraphProperties(
                    new SpacingBetweenLines { After = "160", Line = "276", LineRule = LineSpacingRuleValues.Auto }))),
            MakeHeadingStyle("Title", "Title", fontSizeHalfPoints: 52, outlineLevel: null),
            MakeHeadingStyle("Heading1", "heading 1", 32, 0),
            MakeHeadingStyle("Heading2", "heading 2", 26, 1),
            MakeHeadingStyle("Heading3", "heading 3", 24, 2));
        stylesPart.Styles.Save();
    }

    /// <summary>
    /// Built-in style names ("heading 1", "Title") so Word's navigation pane and Mammoth's
    /// style-name matching both recognize them.
    /// </summary>
    private static Style MakeHeadingStyle(string styleId, string name, int fontSizeHalfPoints, int? outlineLevel)
    {
        var paragraphProps = new StyleParagraphProperties(
            new SpacingBetweenLines { Before = "240", After = "120" });
        if (outlineLevel is not null)
        {
            paragraphProps.Append(new OutlineLevel { Val = outlineLevel.Value });
        }

        return new Style(
            new StyleName { Val = name },
            new PrimaryStyle(),
            paragraphProps,
            new StyleRunProperties(new Bold(), new FontSize { Val = fontSizeHalfPoints.ToString() }))
        {
            Type = StyleValues.Paragraph,
            StyleId = styleId
        };
    }

    private static NumberingDefinitionsPart AddNumbering(MainDocumentPart main)
    {
        var numberingPart = main.AddNewPart<NumberingDefinitionsPart>();
        numberingPart.Numbering = new Numbering(
            // Abstract 1: bullet. Abstract 2: decimal (one NumberingInstance per OL — see DocxContext).
            new AbstractNum(
                new Level(
                    new NumberingFormat { Val = NumberFormatValues.Bullet },
                    new LevelText { Val = "•" },
                    new PreviousParagraphProperties(new Indentation { Left = "720", Hanging = "360" }))
                { LevelIndex = 0 })
            { AbstractNumberId = 1 },
            new AbstractNum(
                new Level(
                    new StartNumberingValue { Val = 1 },
                    new NumberingFormat { Val = NumberFormatValues.Decimal },
                    new LevelText { Val = "%1." },
                    new PreviousParagraphProperties(new Indentation { Left = "720", Hanging = "360" }))
                { LevelIndex = 0 })
            { AbstractNumberId = 2 },
            new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = 1 });
        numberingPart.Numbering.Save();
        return numberingPart;
    }
}
