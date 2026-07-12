namespace TheCanalaveLibrary.Core;

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
    Recommendation = 4,
    Message = 5
}

// NOTE: The vestigial ReadStatus and FavoriteStatus enums were removed. They predated the
// boolean-column interaction model (Settled Axiom #3); reading status is now expressed by the
// HasStarted/IsCompleted/IsIgnored flags on UserStoryInteraction (§4, §5.12), and favorite status
// by IsFavorite/IsHiddenFavorite.

public enum FilterEntityType : short
{
    PersonalList = 0,
    PublicList = 1,
    Group = 2,
    GroupFolder = 3
}



public enum StoryLineageStatus : short
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
// This enum is a C# "mirror" of the 'recommendation_statuses' table.
// The values MUST match the IDs seeded in RecommendationConfigurations.HasData.
// Note: 1-indexed (the table uses identity(1,1)).
public enum RecommendationStatusEnum : short
{
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    UnderReview = 4
}

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



public enum ReportStatusEnum : short
{
    Open = 0,
    UnderReview = 1,
    ResolvedNoAction = 2,
    ResolvedActionTaken = 3
}

/// <summary>
/// Author-verification state of a <c>StoryExternalLink</c> (Feature 53, WU38d). New links start
/// <c>Unverified</c>; moderators flip to <c>Verified</c> (story page shows the checkmark) or
/// <c>Rejected</c> via the WU39 workflow. Editing a verified link's URL resets it to Unverified.
/// </summary>
public enum VerificationStatusEnum : short
{
    Unverified = 0,
    Verified = 1,
    Rejected = 2
}

public enum NotificationCategoryEnum : short
{
    SiteNews = 0,
    YourFollows = 1,
    YourStories = 2,
    YourProfile = 3,
    YourRecommendations = 4,
    Collaborations = 5,
    Groups = 6,
    Warnings = 7,
    YourReports = 8
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
    
    //Notifications for interactions on your stories
    NewStoryFavorite = 20,
    NewStoryFollower = 21,
    NewRecommendationOnYourStory = 22,
    HiddenGem = 23, //A recommendation on your story was designated as a hidden gem
    NewStoryComment = 24,
    YourStoryAddedToGroup = 25,
    TagUpdateSuggestion = 26, //One of your OC tag names matches a newly fanonized tag. Do you want to update it?
    
    //Notifications for interactions on you
    NewFollowerOnYou = 30,
    NewCommentOnYourProfile = 31,
    NewVouchOnYou = 32,
    NewCommentOnBlog = 33,
    CommentReply = 34,
    
    //Notifications about your recommendations
    RecommendationApproved = 40,
    RecommendationHighlighted = 41,
    SuccessfulRec = 42,
    
    //Collaborations
    StoryLineageRequested = 50, //someone else is asking to cite your story
    StoryLineageApproved = 51, //your request to cite someone else's story was approved by them
    NewStoryAcknowledgement = 52, //you were acknowleged for helping with a story
    
    //Groups
    NewGroupStory = 60,
    NewGroupBlogPost = 61,
    
    //Moderation
    //Notifications for the user *receiving* a moderation action
    ContentRemoved = 70, // Your story/comment/etc. was removed for a ToS violation
    StoryRejected = 71,  // Your story submission (from 'PendingApproval') was rejected
    AccountWarning = 72, // You have received an official warning from a moderator
    AccountSuspended = 73, // Your account has been temporarily suspended
    AccountBanned = 74,    // Your account has been permanently banned
    StoryApproved = 75,    // Your story submission was approved

    //Notifications for the user who *sent* a report
    ReportReceived = 80, // "Thank you, we have received your report."
    ReportResolved = 81,  // "Your report about '...' has been resolved."
    ReportResolvedNoAction = 82,  // "Your report about '...' has been resolved."

    //Community Spotlight (Feature 55, WU-Spotlight)
    SpotlightSlotGranted = 90,      // You were awarded a Community Spotlight slot (inline at grant)
    StorySpotlighted = 91,          // Your story is featured on the Community Spotlight (at go-live, worker)
    RecommendationSpotlighted = 92, // Your recommendation is featured beside a spotlighted story (at go-live, worker)
}

public enum SiteRoles : int
{
    User = 1,
    Moderator = 2,
    Admin = 3,
}

//Part 3: Enums for User Settings

// AccountStatus mirrors the User.AccountStatus column (WU34).
// No Shadowbanned — see cross-cutting.md "Moderation Model."
public enum AccountStatusEnum : short
{
    Active = 0,
    Warned = 1,
    Suspended = 2,
    Banned = 3,
}
public enum ProfileVisibility : short
{
    Public = 0,
    UsersOnly = 1,
    Private = 2
}

/// <summary>
/// Audience policy for social interactions (profile comments, private messages).
/// Renamed from AllowInteractions (WU23) to distinguish from UserStoryInteraction.
/// </summary>
public enum SocialInteractionPermission : short
{
    Public = 0,
    UsersOnly = 1,
    Following = 2,
    Nobody = 3
}

// The Sort axis of the three-axis search model (§5.3). Deliberately excludes favorites / last-updated /
// view-count / rec-count sorts (§5.3.3) — popularity-style ordering is not a sanctioned surface.
public enum DefaultSortOrder : short
{
    Random = 0,         // Discovery default (Source=All preload)
    DatePublished = 1,
    Relevance = 2,      // Only available when the FTS filter is active
    Score = 3,          // Recommendation / co-occurrence score (specific surfaces)
    RecentlyRead = 4,   // Viewer-relative: MAX(UserChapterInteraction.LastInteractionDate) per story.
                        // Bookshelves "Actively Reading" only (personal reading management, not a
                        // popularity metric) — surfaces opt in via AvailableSorts; never on /discover.
}

// Groups (WU32) ---------------------------------------------------------------

/// <summary>
/// Role of a user within a group. Stored as <c>short</c> in the DB (HasConversion).
/// Two roles only — no Moderator category (settled WU32).
/// </summary>
public enum GroupRole : short
{
    Member = 0,
    Admin  = 1,
}

/// <summary>
/// Presentation-only preset for group creation / display — NOT stored in the database.
/// Derives from / maps to <c>(AudienceRating, MaxContentRating)</c> via
/// <c>GroupAudienceTypeMapper</c> in <c>Core/Groups/</c>.
/// </summary>
public enum GroupAudienceType
{
    /// <summary>Visible to all; allows all content ratings. AudienceRating=E, MaxContentRating=M.</summary>
    Standard,
    /// <summary>Visible to all; blocks M-rated content. AudienceRating=E, MaxContentRating=T.</summary>
    SfwOnly,
    /// <summary>Hidden from mature-disabled users; allows all content. AudienceRating=M, MaxContentRating=M.</summary>
    Mature,
}