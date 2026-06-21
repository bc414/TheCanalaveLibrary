namespace TheCanalaveLibrary.Core;

/// <summary>
/// Server-side allow-list sanitizer for user-authored rich text (spec §3.21). Every write path that
/// persists <c>EditorView</c> output (chapters, comments, recommendations, blog posts, profile bios,
/// messages) injects this and calls <see cref="Sanitize"/> before saving — never on display.
/// <c>RichTextView</c> trusts that stored HTML is already clean and performs no sanitization of its
/// own (see canalave-conventions/layer2-services.md "User HTML Is Sanitized Once, On Save — Never On
/// Display"). The allow-list is the inverse of <c>EditorView</c>'s toolbar — extend both together
/// (same doc, "The allow-list is the inverse of the toolbar").
/// </summary>
public interface IHtmlSanitizationService
{
    /// <summary>Strips anything outside the allow-list. Null/empty input returns empty.</summary>
    string Sanitize(string? html);
}
