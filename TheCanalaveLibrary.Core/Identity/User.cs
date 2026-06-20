using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
namespace TheCanalaveLibrary.Core;

// --- SETTINGS CLASSES (to be stored as JSON) ---

public class ReaderSettings
{
    // Font and Layout
    [MaxLength(100)]
    public string FontName { get; set; } = "Georgia";
    public int FontSize { get; set; } = 16; // Stored in px
    public float LineHeight { get; set; } = 1.5f; // Stored as an 'em' multiplier
    public int TextWidth { get; set; } = 800; // Max-width in px
    public bool JustifyText { get; set; } = false;
    
    // Browsing Behavior
    public bool AutoLoadNextChapter { get; set; } = false;
    public bool CollapseCommentThreads { get; set; } = true;
    public int DefaultPaginationSize { get; set; } = 20;
    public DefaultSortOrder DefaultSearchSort { get; set; } = DefaultSortOrder.DatePublished;
}

public class PrivacySettings
{
    public ProfileVisibility ProfileVisibility { get; set; } = ProfileVisibility.Public;
    public bool ShowActivityStatus { get; set; } = true;
    public AllowInteractions AllowProfileComments { get; set; } = AllowInteractions.Public;
    public AllowInteractions AllowPrivateMessages { get; set; } = AllowInteractions.UsersOnly;
    public bool ShowUserStats { get; set; } = true;
    public bool ShowCurrentlyReading { get; set; } = true;
}

public class AuthorSettings
{
    public Rating DefaultStoryRating { get; set; } = Rating.T; // Assumes Rating.T is a valid default
}


// --- THE USER CLASS ---

public class User : IdentityUser<int>
{
    [StringLength(512)]
    public string? ProfilePictureRelativeUrl { get; set; }
    [StringLength(256)]
    public string? Tagline { get; set; }
    
    // "ProfileText" is removed (moved to UserProfile partition)
    
    // --- "HOT" FILTER ---
    // This is a critical filter for site-wide queries
    public bool ShowMatureContent { get; set; } = false;
    
    // --- "WARM" SETTINGS (Stored as JSON) ---
    public ReaderSettings ReaderSettings { get; set; } = new ReaderSettings();
    public PrivacySettings PrivacySettings { get; set; } = new PrivacySettings();
    public AuthorSettings AuthorSettings { get; set; } = new AuthorSettings();
    
    // --- Other Settings ---
    public bool PrefersDataSaverMode { get; set; } = false;
    public bool PrefersAnimatedSprites { get; set; } = true;
    public bool AllowDiscoveryFromHiddenFavorites { get; set; } = false;
    
    public int ThemeId { get; set; }
    public Theme Theme { get; set; } = null!;
    
    // --- "COLD" PARTITION ---
    // 1-to-1 Navigation to the "cold" blob
    public virtual UserProfile UserProfile { get; set; } = null!;

    // --- NAVIGATION PROPERTIES ---
    public virtual ICollection<BaseComment> BaseComments { get; set; } = new List<BaseComment>();
    public virtual ICollection<BetaReader> BetaReaders { get; set; } = new List<BetaReader>();
    public virtual ICollection<BlogPostLike> BlogPostLikes { get; set; } = new List<BlogPostLike>();
    public virtual ICollection<PollVote> PollVotes { get; set; } = new List<PollVote>();
    public virtual ICollection<BaseBlogPost> BlogPosts { get; set; } = new List<BaseBlogPost>();
    public virtual ICollection<ChapterContent> ChapterContents { get; set; } = new List<ChapterContent>();
    public virtual ICollection<CoAuthor> CoAuthors { get; set; } = new List<CoAuthor>();
    public virtual ICollection<CommentLike> CommentLikes { get; set; } = new List<CommentLike>();
    public virtual ICollection<CommunitySpotlight> CommunitySpotlights { get; set; } = new List<CommunitySpotlight>();
    public virtual ICollection<ConversationParticipant> ConversationParticipants { get; set; } = new List<ConversationParticipant>();
    public virtual ICollection<CustomList> CustomLists { get; set; } = new List<CustomList>();
    public virtual ICollection<FeatureContribution> FeatureContributions { get; set; } = new List<FeatureContribution>();
    public virtual ICollection<FollowedUser> FollowedUserFollowedUserNavigations { get; set; } = new List<FollowedUser>();
    public virtual ICollection<FollowedUser> FollowedUserUsers { get; set; } = new List<FollowedUser>();
    public virtual ICollection<Vouch> VouchesGiven { get; set; } = new List<Vouch>();
    public virtual ICollection<Vouch> VouchesReceived { get; set; } = new List<Vouch>();
    public virtual ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();
    public virtual ICollection<GroupStory> GroupStories { get; set; } = new List<GroupStory>();
    public virtual ICollection<Group> Groups { get; set; } = new List<Group>();
    public virtual ICollection<Notification> NotificationRecipientUsers { get; set; } = new List<Notification>();
    public virtual ICollection<Notification> NotificationSourceUsers { get; set; } = new List<Notification>();
    public virtual ICollection<PrivateMessage> PrivateMessages { get; set; } = new List<PrivateMessage>();
    public virtual ICollection<RecommendationSuccess> RecommendationSuccesses { get; set; } = new List<RecommendationSuccess>();
    public virtual ICollection<Recommendation> Recommendations { get; set; } = new List<Recommendation>();
    public virtual ICollection<Report> ReportModeratorUsers { get; set; } = new List<Report>();
    public virtual ICollection<Report> ReportReporterUsers { get; set; } = new List<Report>();
    public virtual ICollection<Series> Series { get; set; } = new List<Series>();
    public virtual ICollection<Story> Stories { get; set; } = new List<Story>();
    public virtual ICollection<StoryAcknowledgment> StoryAcknowledgments { get; set; } = new List<StoryAcknowledgment>();
    public virtual ICollection<UserBadge> UserBadges { get; set; } = new List<UserBadge>();
    public virtual ICollection<UserChapterInteraction> UserChapterInteractions { get; set; } = new List<UserChapterInteraction>();
    public virtual ICollection<UserCustomFilter> UserCustomFilters { get; set; } = new List<UserCustomFilter>();
    public virtual ICollection<UserNotificationSetting> UserNotificationSettings { get; set; } = new List<UserNotificationSetting>();
    public virtual ICollection<UserProfileComment> UserProfileComments { get; set; } = new List<UserProfileComment>();
    public virtual ICollection<UserSearchSetting> UserSearchSettings { get; set; } = new List<UserSearchSetting>();
    public virtual UserStat? UserStat { get; set; }
    public virtual ICollection<UserStoryInteraction> UserStoryInteractions { get; set; } = new List<UserStoryInteraction>();
    public virtual ICollection<ApplicationRole> Roles { get; set; } = new List<ApplicationRole>();
}