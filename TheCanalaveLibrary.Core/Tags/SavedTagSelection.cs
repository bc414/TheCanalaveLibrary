using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// A user-created, saved selection of tags for reuse in searching (Feature 15, WU43). Persists only
/// the tag include/exclude axis of a discovery filter — never text/sort/interaction-exclusion state.
/// See <c>layer2-services.md</c> §"Saved Tag Selections Persist Only the Tag Axis" for the full
/// rationale. Moved from <c>Core/Models/</c> to <c>Core/Tags/</c> WU43 (legacy technical-layer folder
/// cleanup, per Code Organization convention) — no namespace change (flat <c>TheCanalaveLibrary.Core</c>).
/// </summary>
public class SavedTagSelection
{
    public int SavedTagSelectionId { get; set; }

    /// <summary>
    /// The ID of the user who owns this selection.
    /// </summary>
    public int UserId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Nickname { get; set; } = null!;

    /// <summary>
    /// Optional caption/note (WU43). Bounded plain text — semantic metadata, not authored content, so
    /// no EditorView/RichTextView/sanitize pipeline. Shown both in the load flyout (notes-to-self) and
    /// on the owner's profile Tag Selections tab (sharing caption) when <see cref="IsPublic"/>.
    /// </summary>
    [MaxLength(280)]
    public string? Description { get; set; }

    /// <summary>
    /// Whether this selection can be viewed and copied by other users. Sharing is copy-on-write — see
    /// <see cref="ISavedTagSelectionWriteService.CopyPublicSelectionAsync"/> — never a subscription.
    /// </summary>
    public bool IsPublic { get; set; } = false;

    public DateTime DateCreated { get; set; }

    // --- Navigation Properties ---

    public virtual User User { get; set; } = null!;

    public virtual ICollection<SavedTagSelectionEntry> Entries { get; set; } = new List<SavedTagSelectionEntry>();
}
