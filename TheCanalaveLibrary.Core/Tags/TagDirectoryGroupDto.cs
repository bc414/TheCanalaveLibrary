namespace TheCanalaveLibrary.Core;

/// <summary>
/// All tags of one <see cref="TagTypeEnum"/>, structured for the Tag Directory browse page.
/// Contains top-level parent nodes; each node carries its children (one level deep).
/// </summary>
public sealed class TagDirectoryGroupDto
{
    public required TagTypeEnum TagType { get; init; }

    /// <summary>
    /// Top-level tags (no parent), ordered by name. Each node's <see cref="TagDirectoryNodeDto.Children"/>
    /// are the tags whose <c>ParentTagId</c> points at this node, also ordered by name.
    /// </summary>
    public required IReadOnlyList<TagDirectoryNodeDto> Nodes { get; init; }
}
