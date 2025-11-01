namespace TheCanalaveLibrary.Models;

public class UserStoryTreeSearchEntry
{
    public int UserId { get; set; }
    public int StoryId { get; set; }
    public bool IsPublicFavorite { get; set; }
    public bool IsPublicOrPrivateFavorite { get; set; }
    public bool IsRecommendation { get; set; }
    public bool IsHiddenGem { get; set; }
    public bool IsAuthorSpotlighted { get; set; }
}