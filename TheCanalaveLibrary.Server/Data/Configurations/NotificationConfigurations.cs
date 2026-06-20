using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(e => e.NotificationTypeId).HasConversion<short>();
        builder.Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");
        // Future indexes for querying (e.g., by RecipientUserId, IsRead, DateCreated)...
    }
}

public sealed class NotificationCategoryConfiguration : IEntityTypeConfiguration<NotificationCategory>
{
    public void Configure(EntityTypeBuilder<NotificationCategory> builder)
    {
        builder.Property(e => e.NotificationCategoryId).HasConversion<short>();

        builder.HasMany(nc => nc.NotificationTypes)
            .WithOne() // Assuming no nav property on NotificationType
            .HasForeignKey(nt => nt.NotificationCategory)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new { NotificationCategoryId = NotificationCategoryEnum.SiteNews, CategoryName = "Site News", Description = "Announcements and updates from the site staff.", SortOrder = 1 },
            new { NotificationCategoryId = NotificationCategoryEnum.YourFollows, CategoryName = "Followed Content", Description = "Updates from authors, stories, and recommendations you follow.", SortOrder = 2 },
            new { NotificationCategoryId = NotificationCategoryEnum.YourStories, CategoryName = "Your Stories", Description = "Interactions with stories you have written.", SortOrder = 3 },
            new { NotificationCategoryId = NotificationCategoryEnum.YourProfile, CategoryName = "Your Profile", Description = "Interactions with your user profile.", SortOrder = 4 },
            new { NotificationCategoryId = NotificationCategoryEnum.YourRecommendations, CategoryName = "Your Recommendations", Description = "Updates on recommendations you have written.", SortOrder = 5 },
            new { NotificationCategoryId = NotificationCategoryEnum.Collaborations, CategoryName = "Collaborations", Description = "Updates related to co-authoring and beta reading.", SortOrder = 6 },
            new { NotificationCategoryId = NotificationCategoryEnum.Groups, CategoryName = "Groups", Description = "Notifications from groups you are a member of.", SortOrder = 7 },
            new { NotificationCategoryId = NotificationCategoryEnum.Warnings, CategoryName = "Warnings", Description = "Alerts related to your account or content.", SortOrder = 8 },
            new { NotificationCategoryId = NotificationCategoryEnum.YourReports, CategoryName = "Your Reports", Description = "Updates on reports you have submitted.", SortOrder = 9 }
        );

        builder.HasIndex(e => e.CategoryName).IsUnique();
        // Future indexes for querying...
    }
}

public sealed class NotificationTypeConfiguration : IEntityTypeConfiguration<NotificationType>
{
    public void Configure(EntityTypeBuilder<NotificationType> builder)
    {
        builder.Property(e => e.NotificationCategory).HasConversion<short>();
        builder.Property(e => e.NotificationTypeId).HasConversion<short>();

        builder.HasMany(nt => nt.Notifications)
            .WithOne(n => n.NotificationType)
            .HasForeignKey(n => n.NotificationTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(nt => nt.UserNotificationSettings)
            .WithOne(uns => uns.NotificationType)
            .HasForeignKey(uns => uns.NotificationTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            // Site News (Category 0)
            new { NotificationTypeId = NotificationTypeEnum.SiteAnnouncement, NotificationKey = "SiteAnnouncement", DisplayName = "Site Announcement", Description = "A new announcement from site staff.", NotificationCategory = NotificationCategoryEnum.SiteNews, DefaultEmailEnabled = false, DefaultCollapsed = false },

            // Followed Content (Category 1)
            new { NotificationTypeId = NotificationTypeEnum.NewChapterOnFollowedStory, NotificationKey = "NewChapterOnFollowedStory", DisplayName = "New Chapter", Description = "A story you follow has a new chapter.", NotificationCategory = NotificationCategoryEnum.YourFollows, DefaultEmailEnabled = true, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.NewStoryByFollowedUser, NotificationKey = "NewStoryByFollowedUser", DisplayName = "New Story", Description = "An author you follow posted a new story.", NotificationCategory = NotificationCategoryEnum.YourFollows, DefaultEmailEnabled = true, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.NewRecommendationByFollowedUser, NotificationKey = "NewRecommendationByFollowedUser", DisplayName = "New Recommendation by Followed User", Description = "An author you follow posted a new recommendation.", NotificationCategory = NotificationCategoryEnum.YourFollows, DefaultEmailEnabled = false, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.NewBlogPostByFollowedUser, NotificationKey = "NewBlogPostByFollowedUser", DisplayName = "New Blog Post", Description = "An author you follow posted a new blog post.", NotificationCategory = NotificationCategoryEnum.YourFollows, DefaultEmailEnabled = false, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.NewBlogPostOnFollowedStory, NotificationKey = "NewBlogPostOnFollowedStory", DisplayName = "New Story Blog Post", Description = "A story you follow has a new blog post.", NotificationCategory = NotificationCategoryEnum.YourFollows, DefaultEmailEnabled = false, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.NewBlogPostOnFavoritedStory, NotificationKey = "NewBlogPostOnFavoritedStory", DisplayName = "Blog Post on Favorited Story", Description = "A story you favorited has a new blog post.", NotificationCategory = NotificationCategoryEnum.YourFollows, DefaultEmailEnabled = false, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.NewBlogPostOnReadItLaterStory, NotificationKey = "NewBlogPostOnReadItLaterStory", DisplayName = "Blog Post on 'Read Later' Story", Description = "A story on your 'Read Later' list has a new blog post.", NotificationCategory = NotificationCategoryEnum.YourFollows, DefaultEmailEnabled = false, DefaultCollapsed = false },

            // Your Stories (Category 2)
            new { NotificationTypeId = NotificationTypeEnum.NewStoryFavorite, NotificationKey = "NewStoryFavorite", DisplayName = "New Favorite", Description = "Someone favorited one of your stories.", NotificationCategory = NotificationCategoryEnum.YourStories, DefaultEmailEnabled = true, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.NewStoryFollower, NotificationKey = "NewStoryFollower", DisplayName = "New Story Follower", Description = "Someone followed one of your stories.", NotificationCategory = NotificationCategoryEnum.YourStories, DefaultEmailEnabled = true, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.NewRecommendationOnYourStory, NotificationKey = "NewRecommendationOnYourStory", DisplayName = "New Recommendation on Your Story", Description = "Someone recommended one of your stories.", NotificationCategory = NotificationCategoryEnum.YourStories, DefaultEmailEnabled = true, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.HiddenGem, NotificationKey = "HiddenGem", DisplayName = "Hidden Gem", Description = "A recommendation on your story was designated as a 'Hidden Gem'.", NotificationCategory = NotificationCategoryEnum.YourStories, DefaultEmailEnabled = true, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.NewStoryComment, NotificationKey = "NewStoryComment", DisplayName = "New Story Comment", Description = "You received a new comment on one of your story chapters.", NotificationCategory = NotificationCategoryEnum.YourStories, DefaultEmailEnabled = true, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.YourStoryAddedToGroup, NotificationKey = "YourStoryAddedToGroup", DisplayName = "Story Added to Group", Description = "Your story was added to a group's collection.", NotificationCategory = NotificationCategoryEnum.YourStories, DefaultEmailEnabled = false, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.TagUpdateSuggestion, NotificationKey = "TagUpdateSuggestion", DisplayName = "Tag Update Suggestion", Description = "One of your OC tags matches a new fanon tag.", NotificationCategory = NotificationCategoryEnum.YourStories, DefaultEmailEnabled = false, DefaultCollapsed = false },

            // Your Profile (Category 3)
            new { NotificationTypeId = NotificationTypeEnum.NewFollowerOnYou, NotificationKey = "NewFollowerOnYou", DisplayName = "New Profile Follower", Description = "A new user is following you.", NotificationCategory = NotificationCategoryEnum.YourProfile, DefaultEmailEnabled = true, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.NewCommentOnYourProfile, NotificationKey = "NewCommentOnYourProfile", DisplayName = "New Profile Comment", Description = "You received a new comment on your profile.", NotificationCategory = NotificationCategoryEnum.YourProfile, DefaultEmailEnabled = true, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.NewVouchOnYou, NotificationKey = "NewVouchOnYou", DisplayName = "New Vouch", Description = "A user you follow vouched for you.", NotificationCategory = NotificationCategoryEnum.YourProfile, DefaultEmailEnabled = false, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.NewCommentOnBlog, NotificationKey = "NewCommentOnBlog", DisplayName = "New Blog Comment", Description = "You received a new comment on your blog post.", NotificationCategory = NotificationCategoryEnum.YourProfile, DefaultEmailEnabled = true, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.CommentReply, NotificationKey = "CommentReply", DisplayName = "New Reply", Description = "Someone replied to your comment.", NotificationCategory = NotificationCategoryEnum.YourProfile, DefaultEmailEnabled = true, DefaultCollapsed = false },

            // Your Recommendations (Category 4)
            new { NotificationTypeId = NotificationTypeEnum.RecommendationApproved, NotificationKey = "RecommendationApproved", DisplayName = "Recommendation Approved", Description = "An author approved your recommendation.", NotificationCategory = NotificationCategoryEnum.YourRecommendations, DefaultEmailEnabled = true, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.RecommendationHighlighted, NotificationKey = "RecommendationHighlighted", DisplayName = "Recommendation Highlighted", Description = "An author highlighted your recommendation.", NotificationCategory = NotificationCategoryEnum.YourRecommendations, DefaultEmailEnabled = true, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.SuccessfulRec, NotificationKey = "SuccessfulRec", DisplayName = "Successful Recommendation", Description = "A user marked your recommendation as helpful.", NotificationCategory = NotificationCategoryEnum.YourRecommendations, DefaultEmailEnabled = true, DefaultCollapsed = false },

            // Collaborations (Category 5)
            new { NotificationTypeId = NotificationTypeEnum.StoryRelationshipRequested, NotificationKey = "StoryRelationshipRequested", DisplayName = "New Story Relationship Request", Description = "An author wants to link their story to yours.", NotificationCategory = NotificationCategoryEnum.Collaborations, DefaultEmailEnabled = true, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.StoryRelationshipApproved, NotificationKey = "StoryRelationshipApproved", DisplayName = "Story Relationship Approved", Description = "Your request to link to another story was approved.", NotificationCategory = NotificationCategoryEnum.Collaborations, DefaultEmailEnabled = true, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.NewStoryAcknowledgement, NotificationKey = "NewStoryAcknowledgement", DisplayName = "New Acknowledgment", Description = "You were acknowledged as a contributor on a new story.", NotificationCategory = NotificationCategoryEnum.Collaborations, DefaultEmailEnabled = true, DefaultCollapsed = false },

            // Groups (Category 6)
            new { NotificationTypeId = NotificationTypeEnum.NewGroupStory, NotificationKey = "NewGroupStory", DisplayName = "New Group Story", Description = "A new story was added to a group you're in.", NotificationCategory = NotificationCategoryEnum.Groups, DefaultEmailEnabled = false, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.NewGroupBlogPost, NotificationKey = "NewGroupBlogPost", DisplayName = "New Group Blog Post", Description = "A new blog post was made in a group you're in.", NotificationCategory = NotificationCategoryEnum.Groups, DefaultEmailEnabled = false, DefaultCollapsed = false },

            // Moderation Warnings (Category 7)
            new { NotificationTypeId = NotificationTypeEnum.ContentRemoved, NotificationKey = "ContentRemoved", DisplayName = "Content Removed", Description = "Your content was removed for a ToS violation.", NotificationCategory = NotificationCategoryEnum.Warnings, DefaultEmailEnabled = true, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.StoryRejected, NotificationKey = "StoryRejected", DisplayName = "Story Rejected", Description = "Your story submission was rejected.", NotificationCategory = NotificationCategoryEnum.Warnings, DefaultEmailEnabled = true, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.AccountWarning, NotificationKey = "AccountWarning", DisplayName = "Account Warning", Description = "You have received an official warning.", NotificationCategory = NotificationCategoryEnum.Warnings, DefaultEmailEnabled = true, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.AccountSuspended, NotificationKey = "AccountSuspended", DisplayName = "Account Suspended", Description = "Your account has been temporarily suspended.", NotificationCategory = NotificationCategoryEnum.Warnings, DefaultEmailEnabled = true, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.AccountBanned, NotificationKey = "AccountBanned", DisplayName = "Account Banned", Description = "Your account has been permanently banned.", NotificationCategory = NotificationCategoryEnum.Warnings, DefaultEmailEnabled = true, DefaultCollapsed = false },

            // Your Reports (Category 8)
            new { NotificationTypeId = NotificationTypeEnum.ReportReceived, NotificationKey = "ReportReceived", DisplayName = "Report Received", Description = "Thank you, we have received your report.", NotificationCategory = NotificationCategoryEnum.YourReports, DefaultEmailEnabled = false, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.ReportResolved, NotificationKey = "ReportResolved", DisplayName = "Report Resolved (Action Taken)", Description = "Your report has been resolved and action was taken.", NotificationCategory = NotificationCategoryEnum.YourReports, DefaultEmailEnabled = false, DefaultCollapsed = false },
            new { NotificationTypeId = NotificationTypeEnum.ReportResolvedNoAction, NotificationKey = "ReportResolvedNoAction", DisplayName = "Report Resolved (No Action)", Description = "Your report was reviewed, but no action was deemed necessary.", NotificationCategory = NotificationCategoryEnum.YourReports, DefaultEmailEnabled = false, DefaultCollapsed = false }
        );

        builder.HasIndex(e => e.DisplayName).IsUnique();
        // Future indexes for querying (e.g., by NotificationCategory)...
    }
}

public sealed class UserNotificationSettingConfiguration : IEntityTypeConfiguration<UserNotificationSetting>
{
    public void Configure(EntityTypeBuilder<UserNotificationSetting> builder)
    {
        builder.Property(e => e.NotificationTypeId).HasConversion<short>();

        builder.HasKey(e => new { e.UserId, e.NotificationTypeId });
        // Future indexes for querying...
    }
}
