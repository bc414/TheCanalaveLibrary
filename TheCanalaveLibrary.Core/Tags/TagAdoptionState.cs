namespace TheCanalaveLibrary.Core;

/// <summary>
/// Per-(author, target tag) adoption bookkeeping (WU-TagFanon Group 8). Carries the two author-side
/// rules: <see cref="DateNotified"/> — an author is told a name became official exactly once per
/// tag, no matter how many later stories they write with it or how many mod sweeps run — and
/// <see cref="IsDismissed"/> — only the author knows whether their "Saura" is <i>that</i> Saura,
/// so they may mark a pending adoption not-applicable, reversibly. Composite PK (UserId, TargetTagId).
/// </summary>
public partial class TagAdoptionState
{
    public int UserId { get; set; }

    public int TargetTagId { get; set; }

    /// <summary>When the type-26 notification was sent. Non-null blocks any re-notify.</summary>
    public DateTime? DateNotified { get; set; }

    /// <summary>Author-side "not my character" — hides the tag from their pending index,
    /// reversibly.</summary>
    public bool IsDismissed { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Tag TargetTag { get; set; } = null!;
}
