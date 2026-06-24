using System.Text.RegularExpressions;
using System.Web;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Pure, dependency-free text helpers for the Recommendations cluster.
/// Parallel to <see cref="ChapterText"/> in Core/Chapters/.
/// </summary>
public static partial class RecommendationText
{
    /// <summary>
    /// Counts plain-text characters in sanitized HTML by stripping tags and decoding entities.
    /// Used to enforce <see cref="RecommendationConstants.MinLength"/> before persisting.
    /// Call on the already-sanitized HTML (output of IHtmlSanitizationService.Sanitize), not raw editor output.
    /// </summary>
    public static int CountPlainTextLength(string? sanitizedHtml)
    {
        if (string.IsNullOrWhiteSpace(sanitizedHtml)) return 0;
        string stripped = HtmlTagRun().Replace(sanitizedHtml, string.Empty);
        string decoded = HttpUtility.HtmlDecode(stripped).Replace(' ', ' ');
        return decoded.Trim().Length;
    }

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex HtmlTagRun();
}
