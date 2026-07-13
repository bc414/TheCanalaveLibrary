using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Author-defined chapter grouping ("Book 1", "Volume 2") — a contiguous, non-overlapping range of
/// chapter numbers within one story (WU45; spec §4 "StoryArc"). Gaps between arcs are allowed
/// (prologues/interludes belong to no arc). Overlap and Start ≤ End validation is service-layer
/// business logic, not DB constraints. Display order is fully determined by
/// <see cref="StartChapterNumber"/> — the former SortOrder column was eliminated in WU45 as
/// redundant, drift-prone state; ordinal "Arc X" labels are computed at read time.
/// Chapter reorder/delete shifts these bounds in the same transaction (see
/// ServerChapterWriteService) so ranges track renumbering; an arc emptied by deletion
/// (Start > End) is auto-deleted.
/// </summary>
public partial class StoryArc
{
    public int StoryArcId { get; set; }

    public int StoryId { get; set; }

    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = null!;

    public int StartChapterNumber { get; set; }

    public int EndChapterNumber { get; set; }

    public virtual Story Story { get; set; } = null!;
}
