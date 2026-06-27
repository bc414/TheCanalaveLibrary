namespace TheCanalaveLibrary.Core;

/// <summary>
/// Render-ready tag data emitted by the tag/story read services for the TagChip leaf.
/// <see cref="SpriteIdentifier"/> is the RAW semantic key from <see cref="Tag.SpriteIdentifier"/>
/// (e.g. <c>"bulbasaur"</c>) — NOT a resolved URL. Render components call
/// <c>ISpriteReadService.GetSpriteUrl(ThemeContext.Slug, id, ThemeContext.PrefersAnimated)</c>
/// themselves (see <c>layer2-services.md</c> "Sprite URLs Are Resolved At Render Time").
/// Because the identifier is per-content (not per-viewer), this DTO is freely cacheable across
/// users and themes. Distinct from the lean <see cref="TagDropDownDTO"/> used as a typeahead source.
/// </summary>
public class TagChipDto
{
    public int TagId { get; set; }
    public string TagName { get; set; } = null!;
    public TagTypeEnum TagTypeId { get; set; }
    public string? Description { get; set; }           // tooltip
    public string? SpriteIdentifier { get; set; }      // raw Tag.SpriteIdentifier key; null = no sprite

    // ── Admin fields — only populated by GetTagDirectoryAsync; default false elsewhere. ──

    /// <summary>Whether the tag is a community-fanon tag rather than an official canon tag.</summary>
    public bool IsFanon { get; set; }

    /// <summary>
    /// Character-domain: whether this Character tag permits OC details on StoryCharacter rows.
    /// Always false for non-Character types (coerced by TagValidations).
    /// </summary>
    public bool AllowOCDetails { get; set; }

    /// <summary>
    /// Setting-domain: whether this Setting tag permits custom SettingDetail side-rows.
    /// Always false for non-Setting types (coerced by TagValidations).
    /// </summary>
    public bool AllowSettingDetails { get; set; }

    /// <summary>
    /// FK to the parent Tag, or null for top-level tags. Only one level deep.
    /// Populated by GetTagDirectoryAsync; null elsewhere.
    /// </summary>
    public int? ParentTagId { get; set; }
}
