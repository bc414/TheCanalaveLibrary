using Markdig;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Markdown → HTML via Markdig (WU38d). Default pipeline — headings, emphasis, lists,
/// blockquotes, links land directly in allowlist territory; anything exotic (raw HTML, code
/// blocks) is handled by the normalizer+sanitizer downstream.
/// </summary>
public static class MarkdownReader
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().Build();

    public static string Read(Stream file)
    {
        using var reader = new StreamReader(file);
        return Markdown.ToHtml(reader.ReadToEnd(), Pipeline);
    }
}
