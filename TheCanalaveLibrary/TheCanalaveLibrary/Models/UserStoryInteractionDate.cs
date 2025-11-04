namespace TheCanalaveLibrary.Models;

/// <summary>
/// A table that has a one-to-one relationship with UserStoryInteractions. It is a vertical partition so that
/// UserStoryInteractions can save dramatically on row size. This table will most likely have only one of the
/// fields populated, but we will have a filtered index on each column which only contains entries which have
/// that field populated. The filtered index will then be used for queries that sort a category by date.
/// </summary>
public class UserStoryInteractionDate
{
    public int UserId { get; set; }
    public int StoryId { get; set; }

    public DateTime? FavoriteDate { get; set; }
    public DateTime? HiddenFavoriteDate { get; set; }
    public DateTime? FollowedDate { get; set; }
    public DateTime? ReadItLaterDate { get; set; }
    public DateTime? IgnoredDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    public virtual UserStoryInteraction UserStoryInteraction { get; set; } = null!;
}