using System.Text.RegularExpressions;
using System.Web;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Pure server-side helpers for chapter text processing.
/// Dependency-free — lives in Core so it can be unit-tested without a host or DbContext.
/// Parallel to <see cref="StorySlug"/> in Core/Stories/.
/// </summary>
public static partial class ChapterText
{
    /// <summary>
    /// Counts readable words in sanitized HTML by stripping all tags, decoding HTML entities,
    /// and splitting on whitespace. Never call on raw editor output — call on the already-sanitized
    /// HTML (<see cref="IHtmlSanitizationService.Sanitize"/> first), per the convention in
    /// layer2-services.md §"Word Count Is Computed Server-Side, On Save — From Stripped Text".
    /// </summary>
    /// <param name="sanitizedHtml">
    /// Sanitized HTML string (output of <c>IHtmlSanitizationService.Sanitize</c>), or null/empty.
    /// </param>
    /// <returns>
    /// Number of whitespace-delimited tokens in the plain-text content;
    /// 0 for null, empty, or whitespace-only input.
    /// </returns>
    public static int CountWords(string? sanitizedHtml)
    {
        if (string.IsNullOrWhiteSpace(sanitizedHtml)) return 0;

        // Strip all HTML tags.
        string stripped = HtmlTagRun().Replace(sanitizedHtml, " ");

        // Decode entities (e.g. &amp; &nbsp; &lt;) to their text equivalents, then
        // treat non-breaking space as an ordinary space so it doesn't inflate counts.
        string decoded = HttpUtility.HtmlDecode(stripped).Replace(' ', ' ');

        // Split on any whitespace run; remove empty entries produced by leading/trailing space.
        string[] tokens = decoded.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length;
    }

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex HtmlTagRun();
}
