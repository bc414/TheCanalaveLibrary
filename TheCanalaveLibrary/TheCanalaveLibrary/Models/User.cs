using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using TheCanalaveLibrary.Data;

namespace TheCanalaveLibrary.Models;

// Add profile data for application users by adding properties to the ApplicationUser class
public class User : IdentityUser<int>
{
    [StringLength(500)]
    public string? ProfilePictureUrl { get; set; }
    [StringLength(256)]
    public string? Tagline { get; set; }
    public string? ProfileText { get; set; }
    
    // 'Role' is handled by Identity Roles, so you can remove it.
    
    public bool ShowMatureContent { get; set; } = false;
    [StringLength(50)]
    public string? ThemeName { get; set; }
    public bool PrefersDataSaverMode { get; set; } = false;
    public bool PrefersAnimatedSprites { get; set; } = true;
    
    public virtual ICollection<BaseComment> BaseComments { get; set; } = new List<BaseComment>();

    public virtual ICollection<BetaReader> BetaReaders { get; set; } = new List<BetaReader>();

    public virtual ICollection<BlogPostLike> BlogPostLikes { get; set; } = new List<BlogPostLike>();

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