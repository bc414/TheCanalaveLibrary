using System.Reflection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

// Explicit constructors, not primary-constructor syntax — settled WU12. This is the official EF Core
// pattern for a DbContext that is BOTH directly registered (AddDbContext<ApplicationDbContext>) AND a
// base class another registered context inherits from (ReadOnlyApplicationDbContext): a PUBLIC ctor
// typed to DbContextOptions<ApplicationDbContext> (what DI uses when constructing this type directly —
// without it, multiple registered DbContext types make the *non-generic* DbContextOptions an ambiguous
// "last AddDbContext<T> call wins" mapping), plus a PROTECTED ctor taking the non-generic DbContextOptions
// (invisible to DI's constructor selection, so it can't create ambiguity) that only a subclass can reach,
// letting ReadOnlyApplicationDbContext pass its OWN DbContextOptions<ReadOnlyApplicationDbContext> up
// through it. A primary constructor can't be declared protected, hence the explicit form here.
public class ApplicationDbContext : IdentityDbContext<User, ApplicationRole, int>, IDataProtectionKeyContext
{
    // protected so ReadOnlyApplicationDbContext can close over it in its own OnModelCreating to
    // register the display/visibility query filters (ContentRating, GroupAudience, IsTakenDown).
    // The write context (this class) carries no filters — it sees ground truth. See
    // cross-cutting.md "Content Rating Filtering" for the principle.
    protected readonly IActiveUserContext _activeUser;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IActiveUserContext activeUser)
        : this((DbContextOptions)options, activeUser)
    {
    }

    protected ApplicationDbContext(DbContextOptions options, IActiveUserContext activeUser)
        : base(options)
    {
        _activeUser = activeUser;
    }

    #region DbSets


    //The fundamentals
    //Users is in base class
    public DbSet<UserProfile> UserProfiles { get; set; }

    // Data Protection keyring (WU-DataProtection) — never delete rows; old keys must remain
    // decryptable. Respawn ignores this table in integration tests. See security.md.
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

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
    public DbSet<RecommendationLike> RecommendationLikes { get; set; }

    //Tags
    public DbSet<TagType> TagTypes { get; set; }
    public DbSet<Tag> Tags { get; set; } //The tags must be prepopulated here by site staff
    public DbSet<StoryTag> StoryTags { get; set; } //Contains the tags on a story which are not character or setting
    public DbSet<StoryCharacter> StoryCharacters { get; set; } //Contains the characters in a story
    public DbSet<StoryCharacterPairing> StoryCharacterPairings { get; set; }
    public DbSet<StoryCharacterPairingMember> StoryCharacterPairingMembers { get; set; }
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
    public DbSet<ProfileBlogPost> ProfileBlogPosts { get; set; }
    public DbSet<GroupBlogPost> GroupBlogPosts { get; set; }

    //Advanced Search
    public DbSet<SearchMode> SearchModes { get; set; } //discovery surfaces (SearchPage, TreeSearch, etc.)
    public DbSet<UserStoryInteractionFilterType> UserStoryInteractionFilterTypes { get; set; } //catalog of filterable interaction kinds

    public DbSet<DefaultUserStoryInteractionFilterSetting> DefaultUserStoryInteractionFilterSettings { get; set; } //system default matrix: (SearchMode × filter kind) → IsEnabled
    public DbSet<UserStoryInteractionFilterSetting> UserStoryInteractionFilterSettings { get; set; } //sparse per-user overrides of the default matrix

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
    public DbSet<ChapterComment> ChapterComments { get; set; }
    public DbSet<BlogPostComment> BlogPostComments { get; set; }
    public DbSet<GroupComment> GroupComments { get; set; }           // WU32 — typed set for per-context group comment queries
    public DbSet<UserProfileComment> UserProfileComments { get; set; } // WU30 — typed set for profile-wall comment queries

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

        // No display/visibility query filters here. The write context sees ground truth — it must load
        // entities by id regardless of rating, takedown state, or audience gating. All named filters
        // (ContentRating, GroupAudience, IsTakenDown) live on ReadOnlyApplicationDbContext.OnModelCreating,
        // which closes over _activeUser (exposed as protected above). See cross-cutting.md
        // "Content Rating Filtering" for the principle and rationale.
    }
}
