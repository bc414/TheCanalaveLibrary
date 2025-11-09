namespace TheCanalaveLibrary.Core.Models;

/// <summary>
/// This class packs interaction history for a user and all stories they interact with into 8-bits. It will be used
/// for filtering on search results based on the user's personal interaction history. There will be a filtered index
/// on each boolean column to achieve this in a highly performant manner. The rest of the related metadata is
/// separated into other tables for vertical partitioning so that the core use case of this table, personal filtering,
/// is as performant as possible by making the rows as tiny as possible so that more rows can fit onto an 8KB data page.
/// </summary>
public partial class UserStoryInteraction
{
    public int UserId { get; set; }
    public int StoryId { get; set; }

    // --- 8 Bit-Packed Columns (1 Byte Total) ---
    public bool IsInProgress { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsActivelyReading { get; set; }
    public bool IsFavorite { get; set; } // Public, on-profile
    public bool IsHiddenFavorite { get; set; } // Private, off-profile
    public bool IsFollowed { get; set; }
    public bool IsReadItLater { get; set; }
    public bool IsIgnored { get; set; }

    // --- Navigation Properties ---
    public virtual Story Story { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    
    //For the vertical partitions of related metadata
    
    //There won't be an entry if the user only has the story as in progress.
    //There will be an entry if it is completed, favorited
    public virtual UserStoryInteractionDate? InteractionDate { get; set; }
    public virtual UserStoryRecommendationSource? RecommendationSource { get; set; }
}
