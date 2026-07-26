namespace TheCanalaveLibrary.Core;

/// <summary>
/// Render-ready tag data emitted by the tag/story read services for the TagChip leaf.
/// <see cref="SpriteIdentifier"/> is the RAW semantic key from <see cref="Tag.SpriteIdentifier"/>
/// (e.g. <c>"bulbasaur"</c>) — NOT a resolved URL. Render components call
/// <c>ISpriteReadService.GetSpriteUrl(ThemeContext.Slug, id, ThemeContext.PrefersAnimated)</c>
/// themselves (see <c>layer2-services.md</c> "Sprite URLs Are Resolved At Render Time").
/// Distinct from the lean <see cref="TagDropDownDTO"/> used as a typeahead source.
///
/// <para><b>WU-TagFanon:</b> the per-tag fields (<see cref="IsFanon"/>,
/// <see cref="AllowCustomName"/>, <see cref="ParentTagId"/>, <see cref="ParentTagName"/>) are
/// populated by EVERY chip projection — the old "only GetTagDirectoryAsync" caveat was a
/// hydration trap and is gone. A child tag with no sprite of its own inherits the parent's
/// <see cref="SpriteIdentifier"/> at projection time. The per-STORY overlay pair
/// (<see cref="CustomName"/>/<see cref="Nuance"/>) is populated only by story-scoped projections
/// (story page, cards) and stays null in catalog contexts (directory, typeahead).</para>
/// </summary>
public class TagChipDto
{
    public int TagId { get; set; }
    public string TagName { get; set; } = null!;
    public TagTypeEnum TagTypeId { get; set; }
    public string? Description { get; set; }           // tooltip
    public string? SpriteIdentifier { get; set; }      // raw key (parent-inherited when own is null); null = no sprite

    /// <summary>Whether the tag is a community-fanon tag rather than an official canon tag.</summary>
    public bool IsFanon { get; set; }

    /// <summary>
    /// Whether per-story associations of this tag may carry a <c>CustomName</c>
    /// (WU-TagFanon; replaced <c>AllowOCDetails</c>/<c>AllowSettingDetails</c>).
    /// </summary>
    public bool AllowCustomName { get; set; }

    /// <summary>FK to the parent Tag, or null for top-level tags. Only one level deep.</summary>
    public int? ParentTagId { get; set; }

    /// <summary>Parent tag's name (null for top-level tags) — feeds the chip tooltip
    /// ("Saura — a Bulbasaur") and the sr-only text.</summary>
    public string? ParentTagName { get; set; }

    // ── Per-story overlay (story-scoped projections only; null in catalog contexts) ──

    /// <summary>The story's named instance of this tag ("Aethon Region" on Original Setting).</summary>
    public string? CustomName { get; set; }

    /// <summary>The story's note on this tag ("slow burn, no love triangle" on Romance).</summary>
    public string? Nuance { get; set; }
}
