namespace TheCanalaveLibrary.Core;

/// <summary>
/// The high-frequency interaction table: one tiny row per (user, story) pair the user has interacted with.
/// Sparse — no row means every flag is false. Kept deliberately small so many rows fit on an 8 KB page,
/// because its core job is fast personal filtering of search results (one filtered index per flag).
/// Related metadata is vertically partitioned into <see cref="UserStoryInteractionDate"/> and
/// <see cref="UserStoryRecommendationSource"/>.
/// </summary>
public partial class UserStoryInteraction
{
    public int UserId { get; set; }
    public int StoryId { get; set; }

    // --- Reading-status flags (§4, §5.12) ---
    // HasStarted: permanent past event (Has- prefix). Reading began; set at 90% scroll of Chapter 1.
    public bool HasStarted { get; set; }
    // IsCompleted: current mutable state (Is- prefix). Caught up with everything published, or "marked read elsewhere".
    public bool IsCompleted { get; set; }
    // NOTE: "Actively reading" / "in progress" is DERIVED (HasStarted && !IsCompleted && !IsIgnored) — never stored.

    // --- Other interaction flags ---
    public bool IsFavorite { get; set; }        // Public, on-profile
    public bool IsHiddenFavorite { get; set; }  // Private, off-profile
    public bool IsFollowed { get; set; }
    public bool IsReadItLater { get; set; }
    public bool IsIgnored { get; set; }

    // Zero-coupling: no flag drives another. The service layer rejects impossible combinations but never cascades.

    // --- Navigation Properties ---
    public virtual Story Story { get; set; } = null!;
    public virtual User User { get; set; } = null!;

    // Vertical partitions of related metadata (sparse — only present when relevant)
    // InteractionDatePartition: renamed from InteractionDate (WU23) to avoid shadowing the type UserStoryInteractionDate.
    public virtual UserStoryInteractionDate? InteractionDatePartition { get; set; }
    public virtual UserStoryRecommendationSource? RecommendationSource { get; set; }
}
