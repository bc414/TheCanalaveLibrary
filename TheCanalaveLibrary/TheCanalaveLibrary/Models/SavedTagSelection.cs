using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Models;

/// <summary>
/// A user-created, saved selection of tags for reuse in searching.
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
    /// Whether this selection can be viewed and copied by other users.
    /// </summary>
    public bool IsPublic { get; set; } = false;

    public DateTime DateCreated { get; set; }

    // --- Navigation Properties ---

    public virtual User User { get; set; } = null!;
    
    public virtual ICollection<SavedTagSelectionEntry> Entries { get; set; } = new List<SavedTagSelectionEntry>();
}