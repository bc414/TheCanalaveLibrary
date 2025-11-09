namespace TheCanalaveLibrary.Core.Models;

// This table is a daily, pre-calculated data mart for graph queries.
// It is read-only for the API. Only users who opt in to include their hidden favorites will appear here.
// This class needs filtered indexes for Users->Stories and for Stories->Users since graph traversals can go both ways.
// The table will be populated daily by a background worker by pulling data from:
// Stories - to get authors, Recommendations - to get Recommendation/HiddenGem/AuthorSpotlight, and
// UserStoryInteractions to get favorites and hidden favorites (must check user setting for opt-in)
public class UserStoryTreeSearchEntry
{
    public int UserId { get; set; }
    public int StoryId { get; set; }
    public bool IsAuthoredByUser { get; set; }
    public bool IsPublicFavorite { get; set; }
    public bool IsHiddenFavorite { get; set; }
    public bool IsRecommendation { get; set; }
    public bool IsHiddenGem { get; set; }
    public bool IsAuthorSpotlighted { get; set; }
}