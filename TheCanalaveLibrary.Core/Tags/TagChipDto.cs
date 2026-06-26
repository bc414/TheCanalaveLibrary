namespace TheCanalaveLibrary.Core;

/// <summary>
/// Render-ready tag data emitted by the tag/story read services for the TagChip leaf.
/// SpriteUrl is a RESOLVED RELATIVE PATH, produced server-side in the read service's .Select()
/// projection via ISpriteReadService.GetSpriteUrl(theme, identifier, animated) using the current
/// user's theme + animation prefs (mirrors StoryListingDto.CoverArtRelativeUrl — see
/// layer2-services.md "Sprite URLs Are Resolved Server-Side, At Projection Time"). Because it is
/// per-user it is request-scoped — never cache across users/themes. Distinct from the lean
/// TagDropDownDTO used as a typeahead source.
/// </summary>
public class TagChipDto
{
    public int TagId { get; set; }
    public string TagName { get; set; } = null!;
    public TagTypeEnum TagTypeId { get; set; }
    public string? Description { get; set; }  // tooltip
    public string? SpriteUrl { get; set; }     // resolved by the producing read service; null = no sprite

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
