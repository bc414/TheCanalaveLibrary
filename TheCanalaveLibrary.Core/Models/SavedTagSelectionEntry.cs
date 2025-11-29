using TheCanalaveLibrary.Core.Tags;

namespace TheCanalaveLibrary.Core.Models;

/// <summary>
/// A single tag entry in a SavedTagSelection.
/// </summary>
public class SavedTagSelectionEntry
{
    public int SavedTagSelectionEntryId { get; set; }

    public int SavedTagSelectionId { get; set; }

    public int TagId { get; set; }

    // (Future consideration: you could add an 'Exclude' boolean here
    // to support negative tag matching in a selection)

    // --- Navigation Properties ---

    public virtual SavedTagSelection SavedTagSelection { get; set; } = null!;
    
    public virtual Tag Tag { get; set; } = null!;
}