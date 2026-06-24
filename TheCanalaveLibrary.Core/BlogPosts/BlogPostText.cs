using System.Text.RegularExpressions;
using System.Web;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Pure, dependency-free text helpers for the BlogPosts cluster.
/// Parallel to <see cref="ChapterText"/> (Core/Chapters/) and <see cref="RecommendationText"/>.
/// </summary>
public static partial class BlogPostText
{
    private const int SnippetMaxLength = 200;

    /// <summary>
    /// Produces a plain-text excerpt from sanitized HTML — suitable for the
    /// <see cref="BlogPostListingDto.ContentSnippet"/> field on <c>BlogPostCard</c>.
    /// Strips HTML tags, decodes entities, trims whitespace, and truncates to
    /// <see cref="SnippetMaxLength"/> characters with an ellipsis.
    /// Call on the already-sanitized HTML (output of <see cref="IHtmlSanitizationService"/>),
    /// not raw editor output.
    /// </summary>
    public static string MakeSnippet(string? sanitizedHtml)
    {
        if (string.IsNullOrWhiteSpace(sanitizedHtml)) return string.Empty;

        string stripped = HtmlTagRun().Replace(sanitizedHtml, " ");
        string decoded = HttpUtility.HtmlDecode(stripped).Replace(' ', ' ');

        // Collapse runs of whitespace to a single space.
        decoded = WhitespaceRun().Replace(decoded, " ").Trim();

        return decoded.Length <= SnippetMaxLength
            ? decoded
            : decoded[..SnippetMaxLength].TrimEnd() + "…";
    }

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex HtmlTagRun();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();
}
