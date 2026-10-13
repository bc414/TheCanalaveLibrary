using System.Text.RegularExpressions;
using System.Web;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// The one test for "this rich-text field holds nothing", shared by every validation of a required
/// <c>EditorView</c> field (layer2-services.md §"Three-Tiered Validation" → "A required rich-text field
/// refuses blank markup").
/// <para>An untouched or emptied Quill editor does not hand back an empty string: it hands back
/// <c>&lt;p&gt;&lt;br&gt;&lt;/p&gt;</c>, and a cleared one can leave several such paragraphs, or
/// <c>&amp;nbsp;</c>. <c>string.IsNullOrWhiteSpace</c> passes all of them, so until the
/// WU-ParityAndRemaining browser pass (2026-10-13) an empty comment, message, chapter, blog post or story
/// summary was accepted as content (an empty comment box was posted to a public thread).</para>
/// <para>Blank means no visible text once the tags are removed and the entities decoded. The
/// sanitizer's allow-list (<c>ServerHtmlSanitizationService</c>, the mirror of the editor's toolbar)
/// admits no image or embed, so text is the only content a field can carry; a link with no text is
/// invisible and counts as blank. Works on raw editor output as well as sanitized HTML.</para>
/// </summary>
public static partial class RichTextContent
{
    /// <summary>True when <paramref name="html"/> is null, whitespace, or markup with no visible
    /// text (<c>&lt;p&gt;&lt;br&gt;&lt;/p&gt;</c>, <c>&lt;p&gt;&amp;nbsp;&lt;/p&gt;</c>, empty
    /// formatting tags).</summary>
    public static bool IsBlank(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return true;
        string text = HttpUtility.HtmlDecode(HtmlTag().Replace(html, string.Empty));
        // string.IsNullOrWhiteSpace treats U+00A0 (&nbsp;) and the other Unicode spaces as white space.
        return string.IsNullOrWhiteSpace(text);
    }

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex HtmlTag();
}
