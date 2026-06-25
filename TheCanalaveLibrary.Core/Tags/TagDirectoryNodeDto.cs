namespace TheCanalaveLibrary.Core;

/// <summary>
/// A top-level tag (no parent) in the Tag Directory, with its direct children (one level deep).
/// Both this node and its children carry a fully-resolved <see cref="TagChipDto"/> so the UI
/// can render <c>TagChip</c> leaves directly without further service calls.
/// </summary>
public sealed class TagDirectoryNodeDto
{
    public required TagChipDto Tag { get; init; }

    /// <summary>
    /// Direct children of this tag, ordered by name. Empty when the tag has no children.
    /// Children are always leaves — the hierarchy is strictly one level deep.
    /// </summary>
    public required IReadOnlyList<TagChipDto> Children { get; init; }
}
