namespace TheCanalaveLibrary.Models;

//Part 1: Pure Magic short enums
public enum Rating : short
{
    E = 0,
    T = 1,
    M = 2
}

public enum ReportedEntityType : short
{
    User = 0,
    Story = 1,
    Comment = 2,
    BlogPost = 3,
    Recommendation = 4
}

public enum ReadStatus : short
{
    Unread = 0,
    InProgress = 1,
    Completed = 2
}

public enum FavoriteStatus : short
{
    None = 0,
    Favorite = 1,
    PrivateFavorite = 2
}

public enum FilterEntityType : short
{
    PersonalList = 0,
    PublicList = 1,
    Group = 2,
    GroupFolder = 3
}

public enum TagPriority : short
{
    Primary = 0,
    Supporting = 1
}

public enum CharacterRelationshipType : short
{
    Romantic = 0,
    Platonic = 1
}

public enum StoryRelationshipStatus : short
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}

//Part 2: enums that also cover lookup tables
//The lookup table has display names and descriptions/other related data
//The enum is for application logic and is used on the foreign key to the lookup table

// This enum is a C# "mirror" of the 'StoryStatuses' table.
// The values MUST match the IDs in the SQL script.
// We use 'short' to match the 'TINYINT' SQL data type.
public enum StoryStatusEnum : short
{
    Draft = 0,
    PendingApproval = 1,
    InProgress = 2,
    Completed = 3,
    OnHiatus = 4,
    Cancelled = 5,
    Rewriting = 6,
    OpenBeta = 7,
    Rejected = 8
}

public enum TagTypeEnum : short
{
    Character = 0,
    Setting = 1,
    Genre = 2,
    ContentWarning = 3,
    CrossoverFandom = 4,
    Relationship = 5
}

public enum ReportStatusEnum : short
{
    Open = 0,
    UnderReview = 1,
    ResolvedNoAction = 2,
    ResolvedActionTaken = 3
}

public enum NotificationCategoryEnum : short
{
    SiteNews,
    YourFollows,
    YourStories,
    YourProfile,
    YourRecommendations,
    Collaborations,
    Groups,
    Moderation,
}

public enum NotificationTypeEnum : short
{
    SiteAnnouncement = 0,
    
    //Notifications for followed content
    NewChapterOnFollowedStory = 10,
    NewStoryByFollowedUser = 11,
    NewRecommendationByFollowedUser = 12,
    NewBlogPostByFollowedUser = 13,
    NewBlogPostOnFollowedStory = 14,
    NewBlogPostOnFavoritedStory = 15,
    NewBlogPostOnReadItLaterStory = 16,
    
    //Notifications for interactions on you
    NewFollowerOnYou = 23,
    NewCommentOnYourProfile,
    NewVouchOnYou,
    NewCommentOnBlog,
    
    //Notifications for interactions on your stories
    NewReview = 20,
    NewStoryFavorite = 21,
    NewStoryFollower = 22,
    NewRecommendationOnYourStory = 24,
    HiddenGem, //A recommendation on your story was designated as a hidden gem
    NewStoryComment = 25,
    NewBlogPostComment = 26,
    
    //Notifications about your recommendations
    RecommendationApproved,
    RecommendationSpotlighted,
    SuccessfulRec,
    
    //Story relationships
    StoryRelationshipRequested, //someone else is asking to cite your story
    StoryRelationshipApproved, //your request to cite someone else's story was approved by them
    NewStoryAcknowledgement, //you were acknowleged for helping with a story
    
    //Groups
    NewGroupStory,
    NewGroupBlogPost,
    
    //Fanon
    TagUpdateSuggestion, //One of your OC tag names matches a newly fanonized tag. Do you want to update it?
    
}

// ... (Keep all existing enums from Part 1 and Part 2) ...

//Part 3: Enums for User Settings
public enum ProfileVisibility : short
{
    Public = 0,
    UsersOnly = 1,
    Private = 2
}

public enum AllowInteractions : short
{
    Public = 0,
    UsersOnly = 1,
    Following = 2,
    Nobody = 3
}

public enum DefaultSortOrder : short
{
    LastUpdated = 0,
    PublishDate = 1,
    Favorites = 2,
    ViewCount = 3,
    Relevance = 4,
    Random = 5, //Discovery!
}