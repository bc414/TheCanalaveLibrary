namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// The closed variant set of the Content Surface material (design constitution, 2026-07-10 —
/// layer4-style.md §"Element Roles"). Owners of RichTextView/EditorView select a variant and
/// placement; they never invent a treatment.
/// </summary>
public enum ContentSurfaceVariant
{
    /// <summary>Long-form reading: chapter bodies, blog posts, story long descriptions.</summary>
    Reading,

    /// <summary>Compact prose blocks: comment/recommendation/vouch bodies, bios, messages.</summary>
    Inline,

    /// <summary>Authoring: wraps EditorView/Quill; carries the focus-within ring.</summary>
    Input,
}
