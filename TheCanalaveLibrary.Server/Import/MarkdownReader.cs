using Markdig;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Markdown → HTML via Markdig (WU38d). Default pipeline — headings, emphasis, lists,
/// blockquotes, links land directly in allowlist territory; anything exotic (raw HTML, code
/// blocks) is handled by the normalizer+sanitizer downstream. Decoded through
/// <see cref="ImportTextDecoder"/> (BOM, then strict UTF-8, then Windows-1252 with a warning).
/// </summary>
public static class MarkdownReader
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().Build();

    public static (string Html, List<ImportWarning> Warnings) Read(Stream file)
    {
        (string markdown, ImportWarning? warning) = ImportTextDecoder.Decode(file);
        return (Markdown.ToHtml(markdown, Pipeline), warning is null ? [] : [warning]);
    }
}
