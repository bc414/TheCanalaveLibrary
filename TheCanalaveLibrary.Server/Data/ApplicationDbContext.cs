using System.Reflection;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // first — sets up the Identity model

        // All entity configuration lives in IEntityTypeConfiguration<T> classes under Data/Configurations/,
        // grouped one file per folder-cluster but all colocated here (not split into feature cluster
        // folders) — see skills/canalave-conventions/layer1-data-model.md §"Fluent API Organization".
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
