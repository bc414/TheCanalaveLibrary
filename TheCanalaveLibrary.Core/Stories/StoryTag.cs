using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Flat per-story tag association (Genre / Setting / ContentWarning / CrossoverFandom — Character
/// routes through <see cref="StoryCharacter"/> instead). Carries the per-story overlay pair
/// (WU-TagFanon, which folded the former <c>SettingDetail</c> side-row onto this junction):
/// <see cref="CustomName"/> — a named instance of the archetype, gated by
/// <c>Tag.AllowCustomName</c> — and <see cref="Nuance"/> — the author's per-story note on the
/// tag, never gated, any type.
/// </summary>
public partial class StoryTag : IStoryTag
{
    public int StoryId { get; set; }

    public int TagId { get; set; }

    public TagPriority Priority { get; set; }

    [MaxLength(128)]
    public string? CustomName { get; set; }

    [MaxLength(2048)]
    public string? Nuance { get; set; }

    public virtual Story Story { get; set; } = null!;

    public virtual Tag Tag { get; set; } = null!;

    [NotMapped] TagTypeEnum IStoryTag.TagTypeEnum => Tag.TagTypeId;
}
