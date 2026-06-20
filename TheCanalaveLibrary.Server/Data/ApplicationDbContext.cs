using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<User, ApplicationRole, int>(options)
{
    #region DbSets

    
    //The fundamentals
    //Users is in base class
    public DbSet<UserProfile> UserProfiles { get; set; }
    
    //Stories
    public DbSet<Story> Stories { get; set; } //Metadata table
    public DbSet<StoryListing> StoryListings { get; set; } //Warm table for paginated projections on search results
    public DbSet<StoryDetail> StoryDetails { get; set; } //Content table, vertically partitioned
    public DbSet<StoryStatus> StoryStatuses { get; set; } //Enum lookup table
    
    //Chapters
    public DbSet<Chapter> Chapters { get; set; } //Metadata table
    public DbSet<ChapterContent> ChapterContents { get; set; } //Content, multiple versions, and vertically partitioned
    
    //User relationships
    public DbSet<FollowedUser> FollowedUsers { get; set; }
    public DbSet<Vouch> Vouches { get; set; } //Scarce endorsement w/ optional VouchText (was a bool on FollowedUser)

    //Likes / votes — explicit junctions (no DateLiked; denormalized counts on the parent)
    public DbSet<CommentLike> CommentLikes { get; set; }
    public DbSet<BlogPostLike> BlogPostLikes { get; set; }
    public DbSet<PollVote> PollVotes { get; set; }

    //Recommendations
    public DbSet<Recommendation> Recommendations { get; set; }
    public DbSet<RecommendationDetail> RecommendationDetails { get; set; }
    public DbSet<RecommendationStatus> RecommendationStatuses { get; set; }
    public DbSet<RecommendationSuccess> RecommendationSuccesses { get; set; }
    
    //Tags
    public DbSet<TagType> TagTypes { get; set; }
    public DbSet<Tag> Tags { get; set; } //The tags must be prepopulated here by site staff
    public DbSet<StoryTag> StoryTags { get; set; } //Contains the tags on a story which are not character or setting
    public DbSet<StoryCharacter> StoryCharacters { get; set; } //Contains the characters in a story
    public DbSet<StoryCharacterRelationship> StoryCharacterRelationships { get; set; }
    public DbSet<SettingDetail> SettingDetails { get; set; } //For specifying what setting the story is in, as well as original settings
    public DbSet<SavedTagSelection> SavedTagSelections { get; set; }
    public DbSet<SavedTagSelectionEntry> SavedTagSelectionEntries { get; set; }
    
    //Groups
    public DbSet<Group> Groups { get; set; }
    public DbSet<GroupMember> GroupMembers { get; set; }
    public DbSet<GroupStory> GroupStories { get; set; }
    public DbSet<GroupFolder> GroupFolders { get; set; }
    
    //Story Lists
    public DbSet<CustomList> CustomLists { get; set; }
    public DbSet<CustomListEntry> CustomListEntries { get; set; }
    
    //Blogs
    public DbSet<BaseBlogPost> BlogPosts { get; set; }
    
    //Advanced Search
    public DbSet<SearchMode> SearchModes { get; set; } //ways to search on the site, like Random Search, Tree Search, Also Favorited
    public DbSet<UserInteractionFilter> UserInteractionFilters { get; set; } //ways to enable exclusion criteria based on your past history
    
    public DbSet<DefaultSearchSetting> DefaultSearchSettings { get; set; } //a matrix of search modes and interaction filters
    public DbSet<UserSearchSetting> UserSearchSettings { get; set; } //user overrides for the default matrix
    
    public DbSet<UserCustomFilter> UserCustomFilters { get; set; } //User can designate a list or group to use as a custom exclusion filter
    
    //massive table that stores interaction history - ignored, favorited, followed, read it later, completed/in progress
    public virtual DbSet<UserStoryInteraction> UserStoryInteractions { get; set; }
    public virtual DbSet<UserStoryInteractionDate> UserStoryInteractionDates { get; set; }
    public virtual DbSet<UserStoryRecommendationSource> UserStoryRecommendationSources { get; set; }
    
    //stores which chapters have been read and read progress for returning to last read portion
    public DbSet<UserChapterInteraction> UserChapterInteractions { get; set; }
    
    // NOTE: Data-mart / cache tables (AlsoFavoritedScore, AlsoRecommendedScore, UserStoryTreeSearchEntry,
    // SiteDailyStat) are NOT EF-modeled — raw-SQL, worker-built tables with no DbSets or migrations
    // (spec §"Cache / Data Mart Tables"). DailyStoryStat was dropped entirely.

    //Comments
    public DbSet<BaseComment> BaseComments { get; set; }
    
    //Polls
    public DbSet<BasePoll> Polls { get; set; }
    public DbSet<PollOption> PollOptions { get; set; }
    
    //Notifications
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<NotificationCategory> NotificationCategories { get; set; }
    public DbSet<NotificationType> NotificationTypes { get; set; }
    public DbSet<UserNotificationSetting> UserNotificationSettings { get; set; } //user specific override for a setting
    
    //Private Messaging
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<ConversationParticipant> ConversationParticipants { get; set; }
    public DbSet<PrivateMessage> PrivateMessages { get; set; }
    
    //Reporting
    public DbSet<Report> Reports { get; set; }
    public DbSet<ReportReason> ReportReasons { get; set; }
    public DbSet<ReportStatus> ReportStatuses { get; set; }
    
    //User Settings
    public DbSet<Theme> Themes { get; set; }
    
    //Badges and badge metrics
    public DbSet<Badge> Badges { get; set; }
    public DbSet<UserBadge> UserBadges { get; set; }
    public DbSet<FeatureContribution> FeatureContributions { get; set; }
    
    //Advanced story customization
    public DbSet<Series> Series { get; set; } //Series are for splitting a concept across multiple stories
    public DbSet<SeriesEntry> SeriesEntries { get; set; }
    public DbSet<StoryArc> StoryArcs { get; set; } //StoryArcs are the opposite of using series; when the story is too long and needs to be broken down within the story
    public DbSet<StoryImport> StoryImports { get; set; }
    
    //Collaboration
    public DbSet<StoryAcknowledgment> StoryAcknowledgments { get; set; }
    public DbSet<AcknowledgmentRole> AcknowledgmentRoles { get; set; }
    public DbSet<BetaReader> BetaReaders { get; set; }
    public DbSet<CoAuthor> CoAuthors { get; set; }
    public DbSet<StoryRelationship> StoryRelationships { get; set; }
    public DbSet<StoryRelationshipType> StoryRelationshipTypes { get; set; }
    
    //Statistics
    public DbSet<UserStat> UserStats { get; set; }

    //Other
    public DbSet<CommunitySpotlight> CommunitySpotlights { get; set; }
    
    #endregion
    
    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        
        #region Enums, Lookup Tables, and non-content Seed Data
        //1. Enum to SMALLINT conversions
        modelBuilder.Entity<Story>().Property(e => e.Rating).HasConversion<short>();
        modelBuilder.Entity<ChapterContent>().Property(e => e.Rating).HasConversion<short>();
        modelBuilder.Entity<Group>().Property(e => e.Rating).HasConversion<short>();
        modelBuilder.Entity<Group>().Property(e => e.MaxContentRating).HasConversion<short>();
        modelBuilder.Entity<BaseBlogPost>().Property(e => e.Rating).HasConversion<short>();
        modelBuilder.Entity<GroupFolder>().Property(e => e.MaxRating).HasConversion<short>();
        
        modelBuilder.Entity<Report>().Property(e => e.ReportedEntityType).HasConversion<short>();

        modelBuilder.Entity<UserCustomFilter>().Property(e => e.FilterEntityType).HasConversion<short>();

        modelBuilder.Entity<StoryTag>().Property(e => e.Priority).HasConversion<short>();
        modelBuilder.Entity<StoryCharacter>().Property(e => e.Priority).HasConversion<short>();
        modelBuilder.Entity<StoryCharacterRelationship>().Property(e => e.Priority).HasConversion<short>();
        modelBuilder.Entity<StoryCharacterRelationship>().Property(e => e.RelationshipType).HasConversion<short>();

        modelBuilder.Entity<StoryRelationship>().Property(e => e.StatusId).HasConversion<short>();
        
        //1.2 Hybrid - lookup table for UI/description + enum foreign key for application logic. Seed data must match enums
        
        modelBuilder.Entity<StoryStatus>().Property(e => e.StoryStatusId).HasConversion<short>();
        modelBuilder.Entity<Story>().Property(e => e.StoryStatusId).HasConversion<short>();
        modelBuilder.Entity<StoryDetail>().Property(e => e.PostApprovalStatus).HasConversion<short>();
        
        modelBuilder.Entity<TagType>().Property(e => e.TagTypeId).HasConversion<short>();
        modelBuilder.Entity<Tag>().Property(e => e.TagTypeId).HasConversion<short>();

        modelBuilder.Entity<ReportStatus>().Property(e => e.ReportStatusId).HasConversion<short>();
        modelBuilder.Entity<Report>().Property(e => e.ReportStatusId).HasConversion<short>();
        
        modelBuilder.Entity<NotificationCategory>().Property(e => e.NotificationCategoryId).HasConversion<short>();
        modelBuilder.Entity<NotificationType>().Property(e => e.NotificationCategory).HasConversion<short>();
        modelBuilder.Entity<NotificationType>().Property(e => e.NotificationTypeId).HasConversion<short>();
        modelBuilder.Entity<Notification>().Property(e => e.NotificationTypeId).HasConversion<short>();
        modelBuilder.Entity<UserNotificationSetting>().Property(e => e.NotificationTypeId).HasConversion<short>();
        
        
        
        #region Seed Data - Enum-Backed Lookup Tables

        modelBuilder.Entity<NotificationCategory>().HasData(
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

        modelBuilder.Entity<NotificationType>().HasData(
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

        modelBuilder.Entity<ReportStatus>().HasData(
            new { ReportStatusId = ReportStatusEnum.Open, StatusName = "Open" },
            new { ReportStatusId = ReportStatusEnum.UnderReview, StatusName = "Under Review" },
            new { ReportStatusId = ReportStatusEnum.ResolvedNoAction, StatusName = "Resolved - No Action" },
            new { ReportStatusId = ReportStatusEnum.ResolvedActionTaken, StatusName = "Resolved - Action Taken" }
        );

        modelBuilder.Entity<StoryStatus>().HasData(
            new { StoryStatusId = StoryStatusEnum.Draft, StatusName = "Draft", Description = "Story is a work in progress and not visible to the public." },
            new { StoryStatusId = StoryStatusEnum.PendingApproval, StatusName = "Pending Approval", Description = "Story has been submitted and is awaiting moderator approval." },
            new { StoryStatusId = StoryStatusEnum.InProgress, StatusName = "In Progress", Description = "Story is approved, public, and actively being updated." },
            new { StoryStatusId = StoryStatusEnum.Completed, StatusName = "Completed", Description = "The story is finished." },
            new { StoryStatusId = StoryStatusEnum.OnHiatus, StatusName = "On Hiatus", Description = "The author is taking a break from updating." },
            new { StoryStatusId = StoryStatusEnum.Cancelled, StatusName = "Cancelled", Description = "The story will not be continued." },
            new { StoryStatusId = StoryStatusEnum.Rewriting, StatusName = "Rewriting", Description = "The story is undergoing major revisions." },
            new { StoryStatusId = StoryStatusEnum.OpenBeta, StatusName = "Open Beta", Description = "Story is visible to beta readers for feedback." },
            new { StoryStatusId = StoryStatusEnum.Rejected, StatusName = "Rejected", Description = "Story was submitted but did not pass moderation." }
        );

        modelBuilder.Entity<TagType>().HasData(
            new { TagTypeId = TagTypeEnum.Character, TypeName = "Character" },
            new { TagTypeId = TagTypeEnum.Setting, TypeName = "Setting" },
            new { TagTypeId = TagTypeEnum.Genre, TypeName = "Genre" },
            new { TagTypeId = TagTypeEnum.ContentWarning, TypeName = "Content Warning" },
            new { TagTypeId = TagTypeEnum.CrossoverFandom, TypeName = "Crossover Fandom" },
            new { TagTypeId = TagTypeEnum.Relationship, TypeName = "Relationship" }
        );

        #endregion

        #region Seed Data - Non-Enum Lookup Tables
        //Pure lookup tables for UI/description. No enum needed for application logic, but seed data needed

        modelBuilder.Entity<AcknowledgmentRole>().HasData(
            new { AcknowledgmentRoleId = (short)1, RoleName = "Beta Reader" },
            new { AcknowledgmentRoleId = (short)2, RoleName = "Planner" },
            new { AcknowledgmentRoleId = (short)3, RoleName = "Cover Artist" },
            new { AcknowledgmentRoleId = (short)4, RoleName = "Editor" },
            new { AcknowledgmentRoleId = (short)5, RoleName = "Inspiration" }
        );

        modelBuilder.Entity<ApplicationRole>().HasData(
            new { Id = (int)SiteRoles.User, Name = "User", NormalizedName = "USER", ConcurrencyStamp = "1" },
            new { Id = (int)SiteRoles.Moderator, Name = "Moderator", NormalizedName = "MODERATOR", ConcurrencyStamp = "2" },
            new { Id = (int)SiteRoles.Admin, Name = "Admin", NormalizedName = "ADMIN", ConcurrencyStamp = "3" }
        );

        modelBuilder.Entity<Badge>().HasData(
            new { BadgeKey = SiteBadges.BetaReader, DisplayName = "Beta Reader", Description = "Acknowledged as a Beta Reader on stories.", IconBaseUrl = "icons/badges/beta.png", SortOrder = 1 },
            new { BadgeKey = SiteBadges.Patron, DisplayName = "Patron", Description = "Supported the site through Community Spotlight.", IconBaseUrl = "icons/badges/patron.png", SortOrder = 2 },
            new { BadgeKey = SiteBadges.Recommender, DisplayName = "Recommender", Description = "Has many successful recs", IconBaseUrl = "icons/badges/recommender.png", SortOrder = 3 },
            new { BadgeKey = SiteBadges.Architect, DisplayName = "Architect", Description = "Helped develop a site feature", IconBaseUrl = "icons/badges/architect.png", SortOrder = 4 },
            new { BadgeKey = SiteBadges.Artist, DisplayName = "Artist", Description = "Made cover art for others", IconBaseUrl = "icons/badges/artist.png", SortOrder = 5 }
            // ... add other badges
        );
        
        // DefaultSearchSetting requires SearchMode and UserInteractionFilter seeded first.
        // Minimal sane defaults: exclude already-Ignored stories on every discovery surface. Profile
        // surfaces intentionally have no default exclusions (they show the user's full lists).
        // TODO(user): flesh out the full SearchMode × InteractionFilter default matrix when desired.
        modelBuilder.Entity<DefaultSearchSetting>().HasData(
            new { SearchModeKey = SiteSearchModes.SearchPage, InteractionFilterKey = UserStoryInteractionFilters.Ignored, IsEnabled = true },
            new { SearchModeKey = SiteSearchModes.TreeSearch, InteractionFilterKey = UserStoryInteractionFilters.Ignored, IsEnabled = true },
            new { SearchModeKey = SiteSearchModes.AutoTreeSearch, InteractionFilterKey = UserStoryInteractionFilters.Ignored, IsEnabled = true },
            new { SearchModeKey = SiteSearchModes.AlsoFavorited, InteractionFilterKey = UserStoryInteractionFilters.Ignored, IsEnabled = true },
            new { SearchModeKey = SiteSearchModes.AlsoRecommended, InteractionFilterKey = UserStoryInteractionFilters.Ignored, IsEnabled = true }
        );

        modelBuilder.Entity<RecommendationStatus>().HasData(
            new { RecommendationStatusId = (short)1, StatusName = "Pending Approval", Description = "Submitted by user, awaiting author review." },
            new { RecommendationStatusId = (short)2, StatusName = "Approved", Description = "Publicly visible." },
            new { RecommendationStatusId = (short)3, StatusName = "Rejected", Description = "Rejected by author, not visible." },
            new { RecommendationStatusId = (short)4, StatusName = "Under Review", Description = "An approved recommendation that was reported and is under review." }
        );

        modelBuilder.Entity<ReportReason>().HasData(
            new { ReportReasonId = (short)1, ReasonName = "Other", Description = "A reason not covered by other categories." },
            new { ReportReasonId = (short)2, ReasonName = "Spam", Description = "Unsolicited advertising or repeated, low-effort content." },
            new { ReportReasonId = (short)3, ReasonName = "Hate Speech", Description = "Content that attacks a person or group based on race, ethnicity, religion, etc." },
            new { ReportReasonId = (short)4, ReasonName = "Harassment", Description = "Targeted abuse, bullying, or intimidation of a user." },
            new { ReportReasonId = (short)5, ReasonName = "Illegal Content", Description = "Content violating laws, such as child pornography or piracy." },
            new { ReportReasonId = (short)6, ReasonName = "Plagiarism", Description = "Posting content that is not your own without attribution." }
        );

        // Search modes are discovery surfaces (§5.3) — NOT sources/sorts. "Random" is Source=All+Sort=Random
        // on the SearchPage surface, so it is not a mode.
        modelBuilder.Entity<SearchMode>().HasData(
            new { SearchModeKey = SiteSearchModes.SearchPage, Name = "Search Page", Description = "The main discovery surface (Source=All) with tags, text search, and result ordering." },
            new { SearchModeKey = SiteSearchModes.TreeSearch, Name = "Tree Search", Description = "Discover stories through connections: favorites, recommendations, and author follows." },
            new { SearchModeKey = SiteSearchModes.AutoTreeSearch, Name = "Automatic Tree Search", Description = "Automatically surfaced connections from the tree-search data mart." },
            new { SearchModeKey = SiteSearchModes.AlsoFavorited, Name = "Also Favorited", Description = "Stories favorited by users who also favorited your selection." },
            new { SearchModeKey = SiteSearchModes.AlsoRecommended, Name = "Also Recommended", Description = "Stories recommended by users who also recommended your selection." },
            new { SearchModeKey = SiteSearchModes.ProfilePublishedStories, Name = "Profile: Published Stories", Description = "A profile's authored-stories tab." },
            new { SearchModeKey = SiteSearchModes.ProfileFavorites, Name = "Profile: Favorites", Description = "A profile's public-favorites tab." },
            new { SearchModeKey = SiteSearchModes.ProfileRecommendations, Name = "Profile: Recommendations", Description = "A profile's recommendations tab." }
        );

        modelBuilder.Entity<StoryRelationshipType>().HasData(
            new { RelationshipTypeId = (short)1, TypeName = "Inspired By" },
            new { RelationshipTypeId = (short)2, TypeName = "Prequel" },
            new { RelationshipTypeId = (short)3, TypeName = "Sequel" },
            new { RelationshipTypeId = (short)4, TypeName = "Companion Piece" }
        );

        modelBuilder.Entity<Theme>().HasData(
            new { ThemeId = 1, Name = "Pokémon", Description = "The default Pokémon theme!" }
        );
        
        // One filter per UserStoryInteraction boolean column (1:1, no compounds).
        modelBuilder.Entity<UserInteractionFilter>().HasData(
            new { InteractionFilterKey = UserStoryInteractionFilters.Ignored, Name = "Ignored", Description = "Exclude stories you have marked as 'Ignored'." },
            new { InteractionFilterKey = UserStoryInteractionFilters.Completed, Name = "Completed", Description = "Exclude stories you have already finished." },
            new { InteractionFilterKey = UserStoryInteractionFilters.HasStarted, Name = "Started", Description = "Exclude stories you have already started reading." },
            new { InteractionFilterKey = UserStoryInteractionFilters.ReadItLater, Name = "Read It Later", Description = "Exclude stories on your 'Read It Later' list." },
            new { InteractionFilterKey = UserStoryInteractionFilters.Favorited, Name = "Favorited", Description = "Exclude stories on your 'Favorite' list." },
            new { InteractionFilterKey = UserStoryInteractionFilters.HiddenFavorited, Name = "Hidden Favorite", Description = "Exclude stories on your 'Hidden Favorite' list." },
            new { InteractionFilterKey = UserStoryInteractionFilters.Followed, Name = "Followed", Description = "Exclude stories you are 'Following'." }
        );

        #endregion
        #endregion
        
        #region Foreign Keys and Delete Policies
        
            // --- USER (The most complex entity) ---
        
        // 1-to-1 Cascade (Personal data that MUST be deleted with the user)
        modelBuilder.Entity<User>()
            .HasOne(u => u.UserStat)
            .WithOne(s => s.User)
            .HasForeignKey<UserStat>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1-to-Many Cascade (Personal data owned by the user)
        modelBuilder.Entity<User>()
            .HasMany(u => u.CustomLists)
            .WithOne(l => l.User)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<User>()
            .HasMany(u => u.UserBadges)
            .WithOne(b => b.User)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasMany(u => u.UserNotificationSettings)
            .WithOne(s => s.User)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<User>()
            .HasMany(u => u.ConversationParticipants)
            .WithOne(p => p.User)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasMany(u => u.UserChapterInteractions)
            .WithOne(i => i.User)
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<User>()
            .HasMany(u => u.UserStoryInteractions)
            .WithOne(i => i.User)
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasMany(u => u.GroupMembers)
            .WithOne(m => m.User)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasMany(u => u.UserCustomFilters)
            .WithOne(f => f.User)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasMany(u => u.UserSearchSettings)
            .WithOne(s => s.User)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<User>()
            .HasMany(u => u.RecommendationSuccesses)
            .WithOne(r => r.User)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasMany(u => u.StoryAcknowledgments)
            .WithOne(a => a.AcknowledgedUser)
            .HasForeignKey(a => a.AcknowledgedUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasMany(u => u.CoAuthors)
            .WithOne(c => c.CoAuthorUser)
            .HasForeignKey(c => c.CoAuthorUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasMany(u => u.BetaReaders)
            .WithOne(b => b.BetaReaderUser)
            .HasForeignKey(b => b.BetaReaderUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1-to-Many SetNull (Anonymize created content)
        // This policy is CRITICAL for breaking all "diamond" conflicts.
        modelBuilder.Entity<User>()
            .HasMany(u => u.Stories)
            .WithOne(s => s.Author)
            .HasForeignKey(s => s.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<User>()
            .HasMany(u => u.BaseComments)
            .WithOne(c => c.Author)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.SetNull);
            
        modelBuilder.Entity<User>()
            .HasMany(u => u.BlogPosts)
            .WithOne(b => b.Author)
            .HasForeignKey(b => b.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<User>()
            .HasMany(u => u.Recommendations)
            .WithOne(r => r.Recommender)
            .HasForeignKey(r => r.RecommenderId)
            .OnDelete(DeleteBehavior.SetNull);
            
        modelBuilder.Entity<User>()
            .HasMany(u => u.Series)
            .WithOne(s => s.Author)
            .HasForeignKey(s => s.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<User>()
            .HasMany(u => u.Groups)
            .WithOne(g => g.Creator)
            .HasForeignKey(g => g.CreatorId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<User>()
            .HasMany(u => u.GroupStories)
            .WithOne(gs => gs.AddedByUser)
            .HasForeignKey(gs => gs.AddedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<User>()
            .HasMany(u => u.PrivateMessages)
            .WithOne(p => p.SenderUser)
            .HasForeignKey(p => p.SenderUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<User>()
            .HasMany(u => u.CommunitySpotlights)
            .WithOne(cs => cs.SponsoringUser)
            .HasForeignKey(cs => cs.SponsoringUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<User>()
            .HasMany(u => u.ChapterContents)
            .WithOne(cc => cc.Author)
            .HasForeignKey(cc => cc.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);
            
        modelBuilder.Entity<User>()
            .HasMany(u => u.FeatureContributions)
            .WithOne(fc => fc.User)
            .HasForeignKey(fc => fc.UserId)
            .OnDelete(DeleteBehavior.SetNull);
            
        modelBuilder.Entity<User>()
            .HasMany(u => u.ReportReporterUsers)
            .WithOne(r => r.ReporterUser)
            .HasForeignKey(r => r.ReporterUserId)
            .OnDelete(DeleteBehavior.SetNull);
            
        modelBuilder.Entity<User>()
            .HasMany(u => u.ReportModeratorUsers)
            .WithOne(r => r.ModeratorUser)
            .HasForeignKey(r => r.ModeratorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // 1-to-Many Restrict (Lookup tables or conflicts)
        modelBuilder.Entity<User>()
            .HasMany(u => u.UserProfileComments)
            .WithOne(c => c.ProfileUser)
            .HasForeignKey(c => c.ProfileUserId)
            .OnDelete(DeleteBehavior.Restrict); // CONFLICT: Solved with C# code.

        modelBuilder.Entity<Theme>()
            .HasMany<User>() // A Theme can have many Users
            .WithOne(u => u.Theme) // A User has one Theme
            .HasForeignKey(u => u.ThemeId)
            .OnDelete(DeleteBehavior.Restrict); // Don't delete a theme in use.

        // --- DIRECT CONFLICTS (require C# code) ---
        modelBuilder.Entity<User>()
            .HasMany(u => u.FollowedUserUsers)
            .WithOne(f => f.User)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade); // The "follower"

        modelBuilder.Entity<User>()
            .HasMany(u => u.FollowedUserFollowedUserNavigations)
            .WithOne(f => f.FollowedUserNavigation)
            .HasForeignKey(f => f.FollowedUserId)
            .OnDelete(DeleteBehavior.Restrict); // CONFLICT: The "followed"

        modelBuilder.Entity<User>()
            .HasMany(u => u.NotificationRecipientUsers)
            .WithOne(n => n.RecipientUser)
            .HasForeignKey(n => n.RecipientUserId)
            .OnDelete(DeleteBehavior.Cascade); // Required FK

        modelBuilder.Entity<User>()
            .HasMany(u => u.NotificationSourceUsers)
            .WithOne(n => n.SourceUser)
            .HasForeignKey(n => n.SourceUserId)
            .OnDelete(DeleteBehavior.Restrict); // CONFLICT: Nullable FK

        // --- STORY ---
        modelBuilder.Entity<Story>()
            .HasMany(s => s.Chapters)
            .WithOne(c => c.Story)
            .HasForeignKey(c => c.StoryId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<Story>()
            .HasMany(s => s.StoryTags)
            .WithOne(st => st.Story)
            .HasForeignKey(st => st.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Story>()
            .HasMany(s => s.StoryArcs)
            .WithOne(sa => sa.Story)
            .HasForeignKey(sa => sa.StoryId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<Story>()
            .HasMany(s => s.StoryCharacters)
            .WithOne(sc => sc.Story)
            .HasForeignKey(sc => sc.StoryId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<Story>()
            .HasMany(s => s.CoAuthors)
            .WithOne(c => c.Story)
            .HasForeignKey(c => c.StoryId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<Story>()
            .HasMany(s => s.BetaReaders)
            .WithOne(b => b.Story)
            .HasForeignKey(b => b.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Story>()
            .HasMany(s => s.StoryAcknowledgments)
            .WithOne(a => a.Story)
            .HasForeignKey(a => a.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Story>()
            .HasMany(s => s.Recommendations)
            .WithOne(r => r.Story)
            .HasForeignKey(r => r.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Story>()
            .HasMany(s => s.CommunitySpotlights)
            .WithOne(cs => cs.Story)
            .HasForeignKey(cs => cs.StoryId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<Story>()
            .HasMany(s => s.SeriesEntries)
            .WithOne(se => se.Story)
            .HasForeignKey(se => se.StoryId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<Story>()
            .HasMany(s => s.CustomListEntries)
            .WithOne(cle => cle.Story)
            .HasForeignKey(cle => cle.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Story>()
            .HasMany(s => s.GroupStories)
            .WithOne(gs => gs.Story)
            .HasForeignKey(gs => gs.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Story>()
            .HasMany(s => s.UserStoryInteractions)
            .WithOne(usi => usi.Story)
            .HasForeignKey(usi => usi.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Story>()
            .HasOne(s => s.StoryImport)
            .WithOne(si => si.Story)
            .HasForeignKey<StoryImport>(si => si.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Story>()
            .HasMany(s => s.SettingDetails)
            .WithOne(sd => sd.Story)
            .HasForeignKey(sd => sd.StoryId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<Story>()
            .HasMany(s => s.StoryRelationshipSourceStories)
            .WithOne(sr => sr.SourceStory)
            .HasForeignKey(sr => sr.SourceStoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Story>()
            .HasMany(s => s.StoryRelationshipTargetStories)
            .WithOne(sr => sr.TargetStory)
            .HasForeignKey(sr => sr.TargetStoryId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<Story>()
            .HasMany(s => s.ProfileBlogPosts) // From ProfileBlogPost inheritance
            .WithOne(pbp => (pbp as ProfileBlogPost)!.Story)
            .HasForeignKey(pbp => (pbp as ProfileBlogPost)!.StoryId)
            .OnDelete(DeleteBehavior.SetNull); // A blog post can exist without a story

        // Story -> Lookup Tables (Restrict)
        modelBuilder.Entity<Story>()
            .HasOne(s => s.StoryStatus)
            .WithMany(ss => ss.Stories)
            .HasForeignKey(s => s.StoryStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        // --- CHAPTER ---
        modelBuilder.Entity<Chapter>()
            .HasMany(c => c.ChapterContents)
            .WithOne(cc => cc.Chapter)
            .HasForeignKey(cc => cc.ChapterId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<Chapter>()
            .HasMany(c => c.ChapterComments)
            .WithOne(cc => cc.Chapter)
            .HasForeignKey(cc => cc.ChapterId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Chapter>()
            .HasMany(c => c.UserChapterInteractions)
            .WithOne(uci => uci.Chapter)
            .HasForeignKey(uci => uci.ChapterId)
            .OnDelete(DeleteBehavior.Cascade);
            
        // --- BASE HIERARCHIES & SELF-REFERENCES ---
        modelBuilder.Entity<BaseComment>()
            .HasMany(c => c.InverseParentComment)
            .WithOne(c => c.ParentComment)
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.SetNull); // Keep replies as top-level comments

        modelBuilder.Entity<Tag>()
            .HasMany(t => t.InverseParentTag)
            .WithOne(t => t.ParentTag)
            .HasForeignKey(t => t.ParentTagId)
            .OnDelete(DeleteBehavior.SetNull); // Keep child tags as top-level tags

        modelBuilder.Entity<BasePoll>()
            .HasMany(p => p.PollOptions)
            .WithOne(o => o.Poll)
            .HasForeignKey(o => o.PollId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- GROUP & LISTS ---
        modelBuilder.Entity<Group>()
            .HasMany(g => g.GroupMembers)
            .WithOne(m => m.Group)
            .HasForeignKey(m => m.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<Group>()
            .HasMany(g => g.GroupStories)
            .WithOne(gs => gs.Group)
            .HasForeignKey(gs => gs.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<Group>()
            .HasMany(g => g.GroupComments)
            .WithOne(gc => gc.Group)
            .HasForeignKey(gc => gc.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<Group>()
            .HasMany(g => g.GroupFolders)
            .WithOne(f => f.Group)
            .HasForeignKey(f => f.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Group>()
            .HasMany(g => g.GroupBlogPosts)
            .WithOne(gbp => (gbp as GroupBlogPost)!.Group)
            .HasForeignKey(gbp => (gbp as GroupBlogPost)!.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CustomList>()
            .HasMany(l => l.CustomListEntries)
            .WithOne(e => e.List)
            .HasForeignKey(e => e.ListId)
            .OnDelete(DeleteBehavior.Cascade);
            
        // --- CONVERSATION ---
        modelBuilder.Entity<Conversation>()
            .HasMany(c => c.ConversationParticipants)
            .WithOne(p => p.Conversation)
            .HasForeignKey(p => p.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<Conversation>()
            .HasMany(c => c.PrivateMessages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- VERTICAL PARTITIONS (1-to-1) ---
        modelBuilder.Entity<UserStoryInteraction>()
            .HasOne(usi => usi.InteractionDate)
            .WithOne(d => d.UserStoryInteraction)
            .HasForeignKey<UserStoryInteractionDate>(d => new { d.UserId, d.StoryId })
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserStoryInteraction>()
            .HasOne(usi => usi.RecommendationSource)
            .WithOne(r => r.UserStoryInteraction)
            .HasForeignKey<UserStoryRecommendationSource>(r => new { r.UserId, r.StoryId })
            .OnDelete(DeleteBehavior.Cascade);
            
        // --- LOOKUP TABLES (All Restrict) ---
        modelBuilder.Entity<Badge>()
            .HasMany(b => b.UserBadges)
            .WithOne(ub => ub.BadgeKeyNavigation)
            .HasForeignKey(ub => ub.BadgeKey)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<ReportReason>()
            .HasMany(rr => rr.Reports)
            .WithOne(r => r.ReportReason)
            .HasForeignKey(r => r.ReportReasonId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ReportStatus>()
            .HasMany(rs => rs.Reports)
            .WithOne(r => r.ReportStatus)
            .HasForeignKey(r => r.ReportStatusId)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<NotificationType>()
            .HasMany(nt => nt.Notifications)
            .WithOne(n => n.NotificationType)
            .HasForeignKey(n => n.NotificationTypeId)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<NotificationType>()
            .HasMany(nt => nt.UserNotificationSettings)
            .WithOne(uns => uns.NotificationType)
            .HasForeignKey(uns => uns.NotificationTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<NotificationCategory>()
            .HasMany(nc => nc.NotificationTypes)
            .WithOne() // Assuming no nav property on NotificationType
            .HasForeignKey(nt => nt.NotificationCategory)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<SearchMode>()
            .HasMany(sm => sm.UserSearchSettings)
            .WithOne(us => us.SearchModeKeyNavigation)
            .HasForeignKey(us => us.SearchModeKey)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<SearchMode>()
            .HasMany(sm => sm.UserCustomFilters)
            .WithOne(ucf => ucf.SearchModeKeyNavigation)
            .HasForeignKey(ucf => ucf.SearchModeKey)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<SearchMode>()
            .HasMany(sm => sm.DefaultSearchSettings)
            .WithOne(dss => dss.SearchModeKeyNavigation)
            .HasForeignKey(dss => dss.SearchModeKey)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<UserInteractionFilter>()
            .HasMany(uif => uif.UserSearchSettings)
            .WithOne(us => us.InteractionFilterKeyNavigation)
            .HasForeignKey(us => us.InteractionFilterKey)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<UserInteractionFilter>()
            .HasMany(uif => uif.DefaultSearchSettings)
            .WithOne(dss => dss.InteractionFilterKeyNavigation)
            .HasForeignKey(dss => dss.InteractionFilterKey)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RecommendationStatus>()
            .HasMany(rs => rs.Recommendations)
            .WithOne(r => r.Status)
            .HasForeignKey(r => r.StatusId)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<StoryRelationshipType>()
            .HasMany(srt => srt.StoryRelationships)
            .WithOne(sr => sr.RelationshipType)
            .HasForeignKey(sr => sr.RelationshipTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AcknowledgmentRole>()
            .HasMany(ar => ar.StoryAcknowledgments)
            .WithOne(sa => sa.AcknowledgmentRole)
            .HasForeignKey(sa => sa.AcknowledgmentRoleId)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<TagType>()
            .HasMany(tt => tt.Tags)
            .WithOne(t => t.TagType)
            .HasForeignKey(t => t.TagTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Tag>()
            .HasMany(t => t.StoryTags)
            .WithOne(st => st.Tag)
            .HasForeignKey(st => st.TagId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Tag>()
            .HasMany(t => t.StoryCharacters)
            .WithOne(sc => sc.CharacterTag)
            .HasForeignKey(sc => sc.CharacterTagId)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<Tag>()
            .HasMany(t => t.SettingDetails)
            .WithOne(sd => sd.BaseTag)
            .HasForeignKey(sd => sd.BaseTagId)
            .OnDelete(DeleteBehavior.Restrict);
            
        // --- Explicit like / vote junctions + Vouch (replace EF implicit many-to-many) ---
        modelBuilder.Entity<CommentLike>(entity =>
        {
            entity.HasKey(e => new { e.CommentId, e.UserId });
            entity.HasOne(cl => cl.Comment).WithMany(c => c.Likes)
                .HasForeignKey(cl => cl.CommentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(cl => cl.User).WithMany(u => u.CommentLikes)
                .HasForeignKey(cl => cl.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BlogPostLike>(entity =>
        {
            entity.HasKey(e => new { e.BlogPostId, e.UserId });
            entity.HasOne(bl => bl.BlogPost).WithMany(b => b.Likes)
                .HasForeignKey(bl => bl.BlogPostId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(bl => bl.User).WithMany(u => u.BlogPostLikes)
                .HasForeignKey(bl => bl.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PollVote>(entity =>
        {
            entity.HasKey(e => new { e.PollOptionId, e.UserId });
            entity.HasOne(pv => pv.PollOption).WithMany(o => o.Votes)
                .HasForeignKey(pv => pv.PollOptionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(pv => pv.User).WithMany(u => u.PollVotes)
                .HasForeignKey(pv => pv.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Vouch>(entity =>
        {
            entity.HasKey(e => new { e.VouchingUserId, e.VouchedUserId });
            entity.Property(e => e.DateVouched).HasDefaultValueSql("CURRENT_TIMESTAMP");
            // Voucher-side cascades on delete; vouched-side is RESTRICT (incoming vouches cleared in C# DeleteUserService).
            entity.HasOne(v => v.VouchingUser).WithMany(u => u.VouchesGiven)
                .HasForeignKey(v => v.VouchingUserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(v => v.VouchedUser).WithMany(u => u.VouchesReceived)
                .HasForeignKey(v => v.VouchedUserId).OnDelete(DeleteBehavior.Restrict);
        });
            
        // --- Diamond-Breaking SetNulls (Already covered by User SetNull) ---
        // Example: FeatureContribution.BlogPostId -> BaseBlogPost
        modelBuilder.Entity<BaseBlogPost>()
            .HasMany(b => b.FeatureContributions)
            .WithOne(fc => fc.BlogPost)
            .HasForeignKey(fc => fc.BlogPostId)
            .OnDelete(DeleteBehavior.SetNull); // Breaks diamond

        // Example: FeatureContribution.CommentId -> BaseComment
        modelBuilder.Entity<BaseComment>()
            .HasMany(c => c.FeatureContributions)
            .WithOne(fc => fc.Comment)
            .HasForeignKey(fc => fc.CommentId)
            .OnDelete(DeleteBehavior.SetNull); // Breaks diamond
        
        #endregion

        #region Date and Time - Make the database create them

        // --- PART 1: Specific Default Value Configurations ---
        // Set default for "creation" or "posted" timestamps to be generated by the database.
        modelBuilder.Entity<BaseBlogPost>().Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<BasePoll>().Property(e => e.DateOpened).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<BetaReader>().Property(e => e.DateAdded).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<CoAuthor>().Property(e => e.DateAdded).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<CommunitySpotlight>().Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<Conversation>().Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<CustomList>().Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<CustomListEntry>().Property(e => e.DateAdded).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<FollowedUser>().Property(e => e.DateFollowed).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<Group>().Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<GroupMember>().Property(e => e.DateJoined).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<GroupStory>().Property(e => e.DateAdded).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<Notification>().Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<PrivateMessage>().Property(e => e.DateSent).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<Recommendation>().Property(e => e.DatePosted).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<RecommendationSuccess>().Property(e => e.DateRecorded).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<Report>().Property(e => e.DateReported).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<SavedTagSelection>().Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<Series>().Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<StoryAcknowledgment>().Property(e => e.DateAcknowledged).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<StoryImport>().Property(e => e.DateImported).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<StoryRelationship>().Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<UserBadge>().Property(e => e.DateEarned).HasDefaultValueSql("CURRENT_TIMESTAMP");

        #endregion

        #region Composite Keys, Unique constraints, Table Per Type, and Indexes (to be added by query need)

        modelBuilder.Entity<AcknowledgmentRole>(entity =>
        {
            // Future indexes for querying...
        });

        // AlsoFavoritedScore / AlsoRecommendedScore removed — raw-SQL data marts, no EF model (spec §"Cache / Data Mart Tables").

        modelBuilder.Entity<ApplicationRole>(entity =>
        {
            entity.HasIndex(e => e.NormalizedName).IsUnique();
            // Future indexes for querying...
        });

        modelBuilder.Entity<Badge>(entity =>
        {
            entity.HasIndex(e => e.DisplayName).IsUnique();
            // Future indexes for querying...
        });

        modelBuilder.Entity<BaseBlogPost>(entity =>
        {
            entity.ToTable("base_blog_posts");
            // Future indexes for querying (e.g., by AuthorId, DateCreated)...
        });

        modelBuilder.Entity<BaseComment>(entity =>
        {
            // TPT Inheritance setup
            entity.ToTable("base_comments");
            
            // Future indexes for querying (e.g., by AuthorId, DatePosted)...
        });

        modelBuilder.Entity<BasePoll>(entity =>
        {
            // TPT Inheritance setup
            entity.ToTable("base_polls");
            
            // Future indexes for querying...
        });

        modelBuilder.Entity<BetaReader>(entity =>
        {
            entity.HasKey(e => new { e.StoryId, e.BetaReaderUserId });
            // Future indexes for querying (e.g., by BetaReaderUserId)...
        });

        modelBuilder.Entity<BlogPostComment>(entity =>
        {
            entity.ToTable("blog_post_comments");
            entity.Property(e => e.DatePosted) // This configuration maps the column to this table
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            // Future indexes for querying (e.g., by BlogPostId)...
        });

        modelBuilder.Entity<BlogPostPoll>(entity =>
        {
            entity.ToTable("blog_post_polls");
            // Future indexes for querying (e.g., by BlogPostId)...
        });

        modelBuilder.Entity<Chapter>(entity =>
        {
            // A story cannot have two chapters with the same number
            entity.HasIndex(e => new { e.StoryId, e.ChapterNumber }).IsUnique();
            
            // 1. Define the 1-to-many for "all versions"
            entity.HasMany(c => c.ChapterContents)
                .WithOne(cc => cc.Chapter)
                .HasForeignKey(cc => cc.ChapterId)
                .OnDelete(DeleteBehavior.Cascade);

            // 2. Define the separate 1-to-1 for "primary version"
            // This tells EF Core that PrimaryContentId is a special
            // required link to one of the ChapterContents.
            entity.HasOne(c => c.PrimaryContent)
                .WithMany() // No inverse navigation property
                .HasForeignKey(c => c.PrimaryContentId)
                .OnDelete(DeleteBehavior.Restrict); // Don't let a "primary" version be deleted
            // Future indexes for querying...
        });

        modelBuilder.Entity<ChapterComment>(entity =>
        {
            entity.ToTable("chapter_comments");
            entity.Property(e => e.DatePosted) // This configuration maps the column to this table
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            // Future indexes for querying (e.g., by ChapterId, DatePosted)...
        });

        modelBuilder.Entity<ChapterContent>(entity =>
        {
            //sort order can't be duplicated for a chapter
            entity.HasIndex(e => new { e.ChapterId, e.SortOrder }).IsUnique();
            // Future indexes for querying (e.g., by AuthorId)...
        });

        modelBuilder.Entity<CoAuthor>(entity =>
        {
            entity.HasKey(e => new { e.StoryId, e.CoAuthorUserId });
            // Future indexes for querying (e.g., by CoAuthorUserId)...
        });

        modelBuilder.Entity<CommunitySpotlight>(entity =>
        {
            // Future indexes for querying (e.g., by StoryId, EndDate)...
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            // Future indexes for querying...
        });

        modelBuilder.Entity<ConversationParticipant>(entity =>
        {
            entity.HasKey(e => new { e.ConversationId, e.UserId });
            // Future indexes for querying (e.g., by UserId, IsArchived)...
        });

        modelBuilder.Entity<CustomList>(entity =>
        {
            // A user cannot have two custom lists with the same name
            entity.HasIndex(e => new { e.UserId, e.ListName }).IsUnique();
            // Future indexes for querying (e.g., by IsPublic)...
        });

        modelBuilder.Entity<CustomListEntry>(entity =>
        {
            entity.HasKey(e => new { e.ListId, e.StoryId });
            // Future indexes for querying (e.g., by StoryId)...
        });

        // DailyStoryStat removed entirely (not in spec; per-story analytics deferred as a future Layer-8 mart).

        modelBuilder.Entity<DefaultSearchSetting>(entity =>
        {
            entity.HasKey(e => new { e.SearchModeKey, e.InteractionFilterKey });
            // Future indexes for querying...
        });

        modelBuilder.Entity<FeatureContribution>(entity =>
        {
            // Future indexes for querying (e.g., on UserId, CommentId, BlogPostId)...
        });

        modelBuilder.Entity<FollowedUser>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.FollowedUserId });
            // Future indexes for querying (e.g., by FollowedUserId)...
        });

        modelBuilder.Entity<Group>(entity =>
        {
            // Group names must be unique across the site
            entity.HasIndex(e => e.GroupName).IsUnique();
            // Future indexes for querying (e.g., by CreatorId, Rating)...
        });

        modelBuilder.Entity<GroupBlogPost>(entity =>
        {
            entity.ToTable("group_blog_posts");
            // Future indexes for querying (e.g., by GroupId)...
        });

        modelBuilder.Entity<GroupComment>(entity =>
        {
            entity.ToTable("group_comments");
            entity.Property(e => e.DatePosted) // This configuration maps the column to this table
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            // Future indexes for querying (e.g., by GroupId)...
        });

        modelBuilder.Entity<GroupFolder>(entity =>
        {
            // A folder's name must be unique within its parent folder (or at the root)
            entity.HasIndex(e => new { e.GroupId, e.ParentFolderId, e.Name }).IsUnique();
            // Future indexes for querying...
        });

        modelBuilder.Entity<GroupMember>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.GroupId });
            // Future indexes for querying (e.g., by GroupId, Role)...
        });

        modelBuilder.Entity<GroupStory>(entity =>
        {
            // Future indexes for querying (e.g., by GroupId, StoryId, DateAdded)...
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            // Future indexes for querying (e.g., by RecipientUserId, IsRead, DateCreated)...
        });

        modelBuilder.Entity<NotificationCategory>(entity =>
        {
            entity.HasIndex(e => e.CategoryName).IsUnique();
            // Future indexes for querying...
        });

        modelBuilder.Entity<NotificationType>(entity =>
        {
            entity.HasIndex(e => e.DisplayName).IsUnique();
            // Future indexes for querying (e.g., by NotificationCategory)...
        });

        modelBuilder.Entity<PollOption>(entity =>
        {
            // An option's text must be unique within that poll
            entity.HasIndex(e => new { e.PollId, e.Text }).IsUnique();
            // An option's sort order must be unique within that poll
            entity.HasIndex(e => new { e.PollId, e.SortOrder }).IsUnique();
            // Future indexes for querying...
        });

        modelBuilder.Entity<PrivateMessage>(entity =>
        {
            // Future indexes for querying (e.g., by ConversationId, DateSent)...
        });

        modelBuilder.Entity<ProfileBlogPost>(entity =>
        {
            entity.ToTable("profile_blog_posts");
            // Future indexes for querying (e.g., by StoryId)...
        });

        modelBuilder.Entity<Recommendation>(entity =>
        {
            // Future indexes for querying (e.g., by StoryId, RecommenderId, StatusId)...
        });
        
        modelBuilder.Entity<RecommendationDetail>(entity =>
        {
            // Configure the 1-to-1 relationship.
            // RecommendationDetail is the dependent, its PK is also the FK to Recommendation.
            entity.HasOne(d => d.Recommendation)
                .WithOne(r => r.RecommendationDetail)
                .HasForeignKey<RecommendationDetail>(d => d.RecommendationId)
                .OnDelete(DeleteBehavior.Cascade); // Deleting a Recommendation deletes its text

            // Future indexes for full-text search on the 'Text' column...
        });

        modelBuilder.Entity<RecommendationStatus>(entity =>
        {
            entity.HasIndex(e => e.StatusName).IsUnique();
            // Future indexes for querying...
        });

        modelBuilder.Entity<RecommendationSuccess>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RecommendationId });
            // Future indexes for querying (e.g., by RecommendationId)...
        });

        modelBuilder.Entity<Report>(entity =>
        {
            // Future indexes for querying (e.g., by StatusId, ReportedEntityId)...
        });

        modelBuilder.Entity<ReportReason>(entity =>
        {
            entity.HasIndex(e => e.ReasonName).IsUnique();
            // Future indexes for querying...
        });

        modelBuilder.Entity<ReportStatus>(entity =>
        {
            entity.HasIndex(e => e.StatusName).IsUnique();
            // Future indexes for querying...
        });
        
        // ... (after ReportStatus)

        modelBuilder.Entity<SavedTagSelection>(entity =>
        {
            // A user cannot have two selections with the same name
            entity.HasIndex(e => new { e.UserId, e.Nickname }).IsUnique();

            // When a User is deleted, delete their saved selections
            entity.HasOne(e => e.User)
                .WithMany() // No nav property on User
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // When a selection is deleted, delete all its tag entries
            entity.HasMany(e => e.Entries)
                .WithOne(e => e.SavedTagSelection)
                .HasForeignKey(e => e.SavedTagSelectionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Future indexes for querying (e.g., by is_public, user_id)...
        });

        modelBuilder.Entity<SavedTagSelectionEntry>(entity =>
        {
            // A selection cannot have the same tag twice
            entity.HasIndex(e => new { e.SavedTagSelectionId, e.TagId }).IsUnique();

            // Don't allow a Tag to be deleted if it's in a saved selection
            entity.HasOne(e => e.Tag)
                .WithMany() // No nav property on Tag
                .HasForeignKey(e => e.TagId)
                .OnDelete(DeleteBehavior.Restrict);

            // Future indexes for querying (e.g., by tag_id)...
        });

        modelBuilder.Entity<SearchMode>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            // Future indexes for querying...
        });

        modelBuilder.Entity<Series>(entity =>
        {
            // An author cannot have two series with the same name
            entity.HasIndex(e => new { e.AuthorId, e.Name }).IsUnique();
            // Future indexes for querying...
        });

        modelBuilder.Entity<SeriesEntry>(entity =>
        {
            entity.HasKey(e => new { e.SeriesId, e.StoryId });
            // Future indexes for querying (e.g., by StoryId)...
        });

        modelBuilder.Entity<SettingDetail>(entity =>
        {
            // Future indexes for querying (e.g., by StoryId, BaseTagId)...
        });

        // SiteDailyStat removed — raw-SQL data mart, no EF model (spec §"Cache / Data Mart Tables").

        modelBuilder.Entity<SitePoll>(entity =>
        {
            entity.ToTable("site_polls");
            // Future indexes for querying (e.g., by IsArchived)...
        });

        modelBuilder.Entity<Story>(entity =>
        {
            // This table will have MANY indexes for searching.
            // Future indexes for querying (e.g., by author_id, rating, story_status_id, dates)...
        });

        modelBuilder.Entity<StoryDetail>(entity =>
        {
            // Configure the 1-to-1 relationship with Story
            entity.HasOne(d => d.Story)
                .WithOne(s => s.StoryDetail)
                .HasForeignKey<StoryDetail>(d => d.StoryId)
                .OnDelete(DeleteBehavior.Cascade); // Deleting a Story deletes its details

            // A story slug must be unique, but can also be null.
            entity.HasIndex(e => e.Slug).IsUnique()
                .HasFilter("\"slug\" IS NOT NULL");
    
            // Future indexes for querying (e.g., Full-Text on long_description)...
        });

        modelBuilder.Entity<StoryListing>(entity =>
        {
            // Configure the 1-to-1 relationship with Story
            entity.HasOne(p => p.Story)
                .WithOne(s => s.StoryListing)
                .HasForeignKey<StoryListing>(p => p.StoryId)
                .OnDelete(DeleteBehavior.Cascade); // Deleting a Story deletes its listing data

            // 1. Configure the SearchVector as a "generated" column.
            // This tells PostgreSQL to automatically build the vector from
            // the title and description, handling tokenization for you.
            entity.Property<NpgsqlTypes.NpgsqlTsVector>("SearchVector")
                .HasComputedColumnSql(
                    // Combines title and description, using 'english' rules for tokenizing
                    // and `coalesce` to handle nulls safely.
                    "to_tsvector('english', coalesce(\"story_title\", '') || ' ' || coalesce(\"short_description\", ''))", 
                    stored: true); // 'stored: true' is required so we can index it.

            // 2. Create a GIN index on the new vector column.
            // This is the "magic" that makes FTS incredibly fast.
            entity.HasIndex("SearchVector")
                .HasMethod("gin")
                .HasDatabaseName("ix_story_listing_search_vector");
            // Future indexes for querying...
        });

        modelBuilder.Entity<StoryAcknowledgment>(entity =>
        {
            entity.HasKey(e => new { e.StoryId, e.AcknowledgedUserId, e.AcknowledgmentRoleId });
            // Future indexes for querying (e.g., by AcknowledgedUserId)...
        });

        modelBuilder.Entity<StoryArc>(entity =>
        {
            // A story cannot have two arcs with the same title
            entity.HasIndex(e => new { e.StoryId, e.Title }).IsUnique();
            // A story cannot have two arcs with the same sort order
            entity.HasIndex(e => new { e.StoryId, e.SortOrder }).IsUnique();
            // Future indexes for querying...
        });

        modelBuilder.Entity<StoryCharacter>(entity =>
        {
            // Future indexes for querying (e.g., by StoryId, CharacterTagId)...
        });

        modelBuilder.Entity<StoryCharacterRelationship>(entity =>
        {
            // Future indexes for querying (e.g., by StoryId)...
        });

        modelBuilder.Entity<StoryImport>(entity =>
        {
            // A story can only be imported once (1-to-1)
            entity.HasIndex(e => e.StoryId).IsUnique();
            // A specific URL can only be imported once
            entity.HasIndex(e => e.SourceUrl).IsUnique();
            // Future indexes for querying...
        });

        modelBuilder.Entity<StoryRelationship>(entity =>
        {
            entity.HasKey(e => new { e.SourceStoryId, e.TargetStoryId, e.RelationshipTypeId });
            // Future indexes for querying (e.g., by TargetStoryId)...
        });

        modelBuilder.Entity<StoryRelationshipType>(entity =>
        {
            // Future indexes for querying...
        });

        modelBuilder.Entity<StoryStatus>(entity =>
        {
            // Future indexes for querying...
        });

        modelBuilder.Entity<StoryTag>(entity =>
        {
            entity.HasKey(e => new { e.StoryId, e.TagId });
            // Future indexes for querying (e.g., by TagId)...
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            // Tag names must be unique across the site
            entity.HasIndex(e => e.TagName).IsUnique();
            // Future indexes for querying (e.g., by TagTypeId, IsFanon)...
        });

        modelBuilder.Entity<TagType>(entity =>
        {
            entity.HasIndex(e => e.TypeName).IsUnique();
            // Future indexes for querying...
        });

        modelBuilder.Entity<Theme>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            // Future indexes for querying...
        });

        modelBuilder.Entity<User>(entity =>
        {
            // Identity indexes
            entity.HasIndex(e => e.NormalizedUserName).IsUnique();
            entity.HasIndex(e => e.NormalizedEmail).IsUnique();
    
            // Future indexes for querying (e.g., on show_mature_content)...

            // --- JSON complex types (EF Core 10 ComplexProperty + ToJson) ---
            entity.ComplexProperty(u => u.ReaderSettings, b =>
            {
                b.ToJson();
                b.Property(s => s.DefaultSearchSort).HasConversion<short>();
            });

            entity.ComplexProperty(u => u.PrivacySettings, b =>
            {
                b.ToJson();
                b.Property(s => s.ProfileVisibility).HasConversion<short>();
                b.Property(s => s.AllowProfileComments).HasConversion<short>();
                b.Property(s => s.AllowPrivateMessages).HasConversion<short>();
            });

            entity.ComplexProperty(u => u.AuthorSettings, b =>
            {
                b.ToJson();
                b.Property(s => s.DefaultStoryRating).HasConversion<short>();
            });
        });

        // This block is for the "cold" vertical partition
        modelBuilder.Entity<UserProfile>(entity =>
        {
            // Configure the 1-to-1 relationship with User
            entity.HasOne(p => p.User)
                .WithOne(u => u.UserProfile)
                .HasForeignKey<UserProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade); // Deleting a User deletes their profile

            // Future indexes for full-text search on 'profile_text'...
        });

        modelBuilder.Entity<UserBadge>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.BadgeKey });
            // Future indexes for querying...
        });

        modelBuilder.Entity<UserChapterInteraction>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.ChapterId });
            // Future indexes for querying (e.g., by ChapterId, IsRead)...
        });

        modelBuilder.Entity<UserCustomFilter>(entity =>
        {
            // Future indexes for querying (e.g., by UserId)...
        });

        modelBuilder.Entity<UserInteractionFilter>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            // Future indexes for querying...
        });

        modelBuilder.Entity<UserNotificationSetting>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.NotificationTypeId });
            // Future indexes for querying...
        });

        modelBuilder.Entity<UserProfileComment>(entity =>
        {
            entity.ToTable("user_profile_comments");
            entity.Property(e => e.DatePosted) // This configuration maps the column to this table
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            // Future indexes for querying (e.g., by ProfileUserId)...
        });

        modelBuilder.Entity<UserSearchSetting>(entity =>
        {
            // A user can only have one setting for a specific filter/mode
            entity.HasIndex(e => new { e.UserId, e.SearchModeKey, e.InteractionFilterKey }).IsUnique();
            // Future indexes for querying...
        });

        modelBuilder.Entity<UserStat>(entity =>
        {
            entity.HasKey(e => e.UserId);
            // Future indexes for querying...
        });

        modelBuilder.Entity<UserStoryInteraction>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.StoryId });
            // This table will have MANY filtered indexes on the boolean flags.
            // Future indexes for querying (e.g., by StoryId, IsFavorite, IsFollowed)...
            
            // Filtered, Covered Indexes for User-centric filtering
            entity.HasIndex(e => e.UserId).IncludeProperties(e => e.StoryId)
                .HasFilter("\"is_ignored\" = true").HasDatabaseName("ix_user_story_interactions_ignored");
            entity.HasIndex(e => e.UserId).IncludeProperties(e => e.StoryId)
                .HasFilter("\"is_favorite\" = true").HasDatabaseName("ix_user_story_interactions_favorite");
            entity.HasIndex(e => e.UserId).IncludeProperties(e => e.StoryId)
                .HasFilter("\"is_hidden_favorite\" = true").HasDatabaseName("ix_user_story_interactions_hidden_favorite");
            entity.HasIndex(e => e.UserId).IncludeProperties(e => e.StoryId)
                .HasFilter("\"is_followed\" = true").HasDatabaseName("ix_user_story_interactions_followed");
            entity.HasIndex(e => e.UserId).IncludeProperties(e => e.StoryId)
                .HasFilter("\"is_read_it_later\" = true").HasDatabaseName("ix_user_story_interactions_read_it_later");
            entity.HasIndex(e => e.UserId).IncludeProperties(e => e.StoryId)
                .HasFilter("\"is_completed\" = true").HasDatabaseName("ix_user_story_interactions_completed");
            entity.HasIndex(e => e.UserId).IncludeProperties(e => e.StoryId)
                .HasFilter("\"has_started\" = true").HasDatabaseName("ix_user_story_interactions_has_started");
        });

        modelBuilder.Entity<UserStoryInteractionDate>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.StoryId });
            // This table will have filtered indexes on each date column for sorting.
        });

        modelBuilder.Entity<UserStoryRecommendationSource>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.StoryId });
            // Future indexes for querying
        });

        // UserStoryTreeSearchEntry removed — raw-SQL data mart with mirrored graph indexes, no EF model
        // (spec §"Cache / Data Mart Tables"). Built/swapped by the Layer-8 tree-search worker; the full
        // table + index design is preserved in .claude/audit/Discovery.md ("Layer-8 data-mart impl notes").

        #endregion
    }
}
