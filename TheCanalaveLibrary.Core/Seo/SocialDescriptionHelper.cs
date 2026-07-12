using System.Net;
using System.Text.RegularExpressions;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Pure text-cleaning for social meta descriptions (<c>og:description</c> /
/// <c>meta name="description"</c>). Several source fields consumed here (e.g.
/// <see cref="StoryDetailsDTO.LongDescription"/>, <see cref="BlogPostDto.Content"/>) are sanitized
/// HTML, not plain text — this strips tags, decodes entities, collapses whitespace, and truncates
/// at a word boundary. A no-op on already-plain text (no tags found), so it is safe to run over
/// short plain-text blurbs too (e.g. <see cref="StoryListingDto.ShortDescription"/>). Kept
/// standalone in Core (no DI) so callers compose it directly and it stays Unit-testable in isolation.
/// </summary>
public static class SocialDescriptionHelper
{
    public const int DefaultMaxLength = 200;

    private static readonly Regex TagPattern = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Cleans <paramref name="html"/> into a description-safe plain-text string, truncated to
    /// <paramref name="maxLength"/> characters at a word boundary (an ellipsis is appended when
    /// truncated). Returns <c>null</c> for null/whitespace-only/tags-only input, so callers can
    /// chain a fallback with <c>??</c>.
    /// </summary>
    public static string? Clean(string? html, int maxLength = DefaultMaxLength)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        string stripped = TagPattern.Replace(html, " ");
        string decoded = WebUtility.HtmlDecode(stripped);
        string collapsed = WhitespacePattern.Replace(decoded, " ").Trim();

        if (collapsed.Length == 0)
        {
            return null;
        }

        if (collapsed.Length <= maxLength)
        {
            return collapsed;
        }

        // Truncate at the last word boundary within maxLength so a word is never cut mid-way.
        int cut = collapsed.LastIndexOf(' ', maxLength - 1);
        string truncated = cut > 0 ? collapsed[..cut] : collapsed[..maxLength];
        return truncated.TrimEnd() + "…";
    }
}
