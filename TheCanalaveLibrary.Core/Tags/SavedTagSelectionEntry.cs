namespace TheCanalaveLibrary.Core;

/// <summary>
/// A single tag entry in a <see cref="SavedTagSelection"/> (Feature 15, WU43). Flat across every
/// <see cref="TagTypeEnum"/> — a saved selection is ONE unified combination spanning all tag types, not
/// a per-type artifact; each entry's type is recovered from its own <see cref="Tag"/> row when
/// hydrating chips. See <c>layer2-services.md</c> §"Saved Tag Selections Persist Only the Tag Axis".
/// </summary>
public class SavedTagSelectionEntry
{
    public int SavedTagSelectionEntryId { get; set; }

    public int SavedTagSelectionId { get; set; }

    public int TagId { get; set; }

    /// <summary>
    /// <c>false</c> (default) = included tag; <c>true</c> = excluded tag. Added WU43 — the original
    /// model only captured include (bare <c>TagId</c>); this additive column lets a selection carry
    /// both axes <c>TagFilter</c> exposes.
    /// </summary>
    public bool IsExcluded { get; set; }

    // --- Navigation Properties ---

    public virtual SavedTagSelection SavedTagSelection { get; set; } = null!;

    public virtual Tag Tag { get; set; } = null!;
}
