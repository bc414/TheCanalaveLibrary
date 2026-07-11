using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Splits a NORMALIZED document (near-allowlist HTML, <c>hr</c> page-break markers intact) into
/// chapter segments (mode 4, WU38d). Pure and host-free — unit-tested directly. Boundary elements
/// are CONSUMED: a heading that marks a chapter boundary becomes that segment's title, not part of
/// its content. Content before the first boundary becomes segment 0 with a null title (front
/// matter — droppable in review). Suggestion order: top headings, then sub-headings, then page
/// breaks, then "Chapter N" text lines — the first that yields more than one segment.
/// </summary>
public static partial class ChapterSplitter
{
    public sealed record Segment(string? Title, string Html);

    [GeneratedRegex(@"^(chapter|ch\.?|part|episode)\s+\S+", RegexOptions.IgnoreCase)]
    private static partial Regex ChapterLinePattern();

    /// <summary>A paragraph is a chapter-boundary line when it's short and starts like "Chapter N".</summary>
    private static bool IsChapterTextBoundary(IElement element) =>
        element.TagName == "P" &&
        element.TextContent.Trim() is { Length: > 0 and <= 60 } text &&
        ChapterLinePattern().IsMatch(text);

    private static bool IsBoundary(INode node, SplitStrategy strategy) => node is IElement element && strategy switch
    {
        SplitStrategy.TopHeading => element.TagName == "H2",
        SplitStrategy.SubHeading => element.TagName == "H3",
        SplitStrategy.PageBreak => element.TagName == "HR",
        SplitStrategy.ChapterTextPattern => IsChapterTextBoundary(element),
        _ => false
    };

    public static IReadOnlyList<Segment> Split(string normalizedHtml, SplitStrategy strategy)
    {
        if (strategy == SplitStrategy.None)
        {
            return [new Segment(null, normalizedHtml)];
        }

        var segments = new List<Segment>();
        var current = new StringBuilder();
        string? currentTitle = null;
        bool sawBoundary = false;

        void CloseSegment()
        {
            string html = current.ToString();
            // Front matter (pre-first-boundary) is kept only when it has content; boundary-opened
            // segments are always kept so an empty chapter is visible (and droppable) in review.
            if (sawBoundary || html.Trim().Length > 0)
            {
                segments.Add(new Segment(currentTitle, html));
            }
            current.Clear();
        }

        foreach (INode node in ExportDom.ParseFragment(normalizedHtml))
        {
            if (IsBoundary(node, strategy))
            {
                CloseSegment();
                sawBoundary = true;
                string title = node.TextContent.Trim();
                currentTitle = title.Length > 0 ? title : null; // hr boundaries carry no title
                if (segments.Count > ImportLimits.MaxSplitSegments)
                {
                    throw new ImportException(
                        $"This document splits into more than {ImportLimits.MaxSplitSegments} chapters — " +
                        "pick a different delimiter or split the file up.");
                }
            }
            else
            {
                current.Append(node is IElement el ? el.OuterHtml : node.TextContent);
            }
        }
        CloseSegment();

        return segments.Count > 0 ? segments : [new Segment(null, normalizedHtml)];
    }

    /// <summary>
    /// Picks the suggested strategy (first that yields &gt;1 segment) and the set worth offering
    /// in the delimiter picker (those yielding &gt;1, plus <see cref="SplitStrategy.None"/>).
    /// </summary>
    public static (SplitStrategy Suggested, IReadOnlyList<SplitStrategy> Available) Suggest(string normalizedHtml)
    {
        var available = new List<SplitStrategy> { SplitStrategy.None };
        SplitStrategy suggested = SplitStrategy.None;

        foreach (SplitStrategy strategy in (SplitStrategy[])
                 [SplitStrategy.TopHeading, SplitStrategy.SubHeading, SplitStrategy.PageBreak, SplitStrategy.ChapterTextPattern])
        {
            if (Split(normalizedHtml, strategy).Count > 1)
            {
                available.Add(strategy);
                if (suggested == SplitStrategy.None)
                {
                    suggested = strategy;
                }
            }
        }

        return (suggested, available);
    }
}
