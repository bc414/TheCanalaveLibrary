using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Models;

namespace TheCanalaveLibrary.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<User, ApplicationRole, int>(options)
{
    //The fundamentals
    //Users is in base class
    public DbSet<Story> Stories { get; set; }
    public DbSet<StoryStatus> StoryStatuses { get; set; }
    public DbSet<Chapter> Chapters { get; set; }
    public DbSet<ChapterContent> ChapterContents { get; set; }
    
    //User relationships
    public DbSet<FollowedUser> FollowedUsers { get; set; }
    
    //Recommendations
    public DbSet<Recommendation> Recommendations { get; set; }
    public DbSet<RecommendationStatus> RecommendationStatuses { get; set; }
    public DbSet<RecommendationSuccess> RecommendationSuccesses { get; set; }
    
    //Tags
    public DbSet<TagType> TagTypes { get; set; }
    public DbSet<Tag> Tags { get; set; } //The tags must be prepopulated here by site staff
    public DbSet<StoryTag> StoryTags { get; set; } //Contains the tags on a story which are not character or setting
    public DbSet<StoryCharacter> StoryCharacters { get; set; } //Contains the characters in a story
    public DbSet<StoryCharacterRelationship> StoryCharacterRelationships { get; set; }
    public DbSet<SettingDetail> SettingDetails { get; set; } //For specifying what setting the story is in, as well as original settings
    
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
    public DbSet<UserStoryInteraction> UserStoryInteractions { get; set; }
    //stores which chapters have been read and read progress for returning to last read portion
    public DbSet<UserChapterInteraction> UserChapterInteractions { get; set; }
    
    //Tables that get recalculated by a background worker. Uses SYNONYMS
    public DbSet<AlsoFavoritedScore> AlsoFavoritedScores { get; set; }
    public DbSet<AlsoRecommendedScore> AlsoRecommendedScores { get; set; }
    public DbSet<UserStoryTreeSearchEntry> UserStoryTreeSearchEntries { get; set; }
    
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
    public DbSet<DailyStoryStat> DailyStoryStats { get; set; }
    public DbSet<SiteDailyStat> SiteDailyStats { get; set; }
    public DbSet<UserStat> UserStats { get; set; }

    //Other
    public DbSet<CommunitySpotlight> CommunitySpotlights { get; set; }
    
    protected override void OnModelCreating(ModelBuilder m)
    {
        base.OnModelCreating(m);
        
        //1. Enum to TINYINT conversions
        
        //1.1 magic bytes
        m.Entity<Story>().Property(e => e.Rating).HasConversion<byte>();
        m.Entity<ChapterContent>().Property(e => e.Rating).HasConversion<byte>();
        m.Entity<Group>().Property(e => e.Rating).HasConversion<byte>();
        m.Entity<Group>().Property(e => e.MaxContentRating).HasConversion<byte>();
        m.Entity<BaseBlogPost>().Property(e => e.Rating).HasConversion<byte>();
        m.Entity<GroupFolder>().Property(e => e.MaxRating).HasConversion<byte>();
        
        m.Entity<Report>().Property(e => e.ReportedEntityType).HasConversion<byte>();
        
        m.Entity<UserStoryInteraction>().Property(e => e.ReadStatus).HasConversion<byte>();
        
        m.Entity<UserStoryInteraction>().Property(e => e.FavoriteStatus).HasConversion<byte>();

        m.Entity<UserCustomFilter>().Property(e => e.FilterEntityType).HasConversion<byte>();

        m.Entity<StoryTag>().Property(e => e.Priority).HasConversion<byte>();
        m.Entity<StoryCharacter>().Property(e => e.Priority).HasConversion<byte>();
        m.Entity<StoryCharacterRelationship>().Property(e => e.Priority).HasConversion<byte>();
        m.Entity<StoryCharacterRelationship>().Property(e => e.RelationshipType).HasConversion<byte>();

        m.Entity<StoryRelationship>().Property(e => e.StatusId).HasConversion<byte>();
        
        //1.2 Hybrid - lookup table for UI/description + enum foreign key for application logic
        
        m.Entity<StoryStatus>().Property(e => e.StoryStatusId).HasConversion<byte>();
        m.Entity<Story>().Property(e => e.StoryStatusId).HasConversion<byte>();
        m.Entity<Story>().Property(e => e.PostApprovalStatus).HasConversion<byte>();
        
        m.Entity<TagType>().Property(e => e.TagTypeId).HasConversion<byte>();
        m.Entity<Tag>().Property(e => e.TagTypeId).HasConversion<byte>();

        m.Entity<ReportStatus>().Property(e => e.ReportStatusId).HasConversion<byte>();
        m.Entity<Report>().Property(e => e.ReportStatusId).HasConversion<byte>();
        
        m.Entity<NotificationCategory>().Property(e => e.NotificationCategoryId).HasConversion<byte>();
        m.Entity<NotificationType>().Property(e => e.NotificationCategory).HasConversion<byte>();
        m.Entity<NotificationType>().Property(e => e.NotificationTypeId).HasConversion<byte>();
        m.Entity<Notification>().Property(e => e.NotificationTypeId).HasConversion<byte>();
        m.Entity<UserNotificationSetting>().Property(e => e.NotificationTypeId).HasConversion<byte>();

        //2. 
        
        // --- THIS IS THE GLOBAL RULE ---
        // This loop finds EVERY 'DateTime' property in your entire model
        // and sets its default column type to 'datetime2(2)'.
        // This is much cleaner than setting it 20+ times.

        foreach (var property in m.Model.GetEntityTypes()
                     .SelectMany(e => e.GetProperties())
                     .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?)))
        {
            property.SetColumnType("datetime2(2)");
        }
        

        m.Entity<BaseComment>(entity =>
        {
            entity.Property(e => e.DatePosted).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.Author).WithMany(p => p.BaseComments)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        m.Entity<ChapterContent>(entity =>
        {
            entity.Property(e => e.PublishDate).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.Author).WithMany(p => p.ChapterContents)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        m.Entity<CoAuthor>(entity =>
        {
            entity.Property(e => e.DateAdded).HasDefaultValueSql("(getutcdate())");
        });

        m.Entity<CommunitySpotlight>(entity =>
        {
            entity.Property(e => e.DateCreated).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.SponsoringUser).WithMany(p => p.CommunitySpotlights)
                .HasForeignKey(d => d.SponsoringUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        m.Entity<Conversation>(entity =>
        {
            entity.Property(e => e.DateCreated).HasDefaultValueSql("(getutcdate())");
        });

        m.Entity<CustomList>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.ListName }, "UQ_User_ListName").IsUnique();
        });

        m.Entity<CustomListEntry>(entity =>
        {
            entity.Property(e => e.DateAdded).HasDefaultValueSql("(getutcdate())");
        });

        m.Entity<FeatureContribution>(entity =>
        {
            entity.Property(e => e.DateAwarded).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.BlogPost).WithMany(p => p.FeatureContributions)
                .HasForeignKey(d => d.BlogPostId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.Comment).WithMany(p => p.FeatureContributions)
                .HasForeignKey(d => d.CommentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.User).WithMany(p => p.FeatureContributions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        m.Entity<FollowedUser>(entity =>
        {
            entity.Property(e => e.DateFollowed).HasDefaultValueSql("(getutcdate())");
            entity.HasOne(d => d.FollowedUserNavigation).WithMany(p => p.FollowedUserFollowedUserNavigations)
                .HasForeignKey(d => d.FollowedUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_FollowedUsers_Following");
        });

        m.Entity<Group>(entity =>
        {
            entity.Property(e => e.DateCreated).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.Creator).WithMany(p => p.Groups)
                .HasForeignKey(d => d.CreatorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        m.Entity<GroupMember>(entity =>
        {
            entity.Property(e => e.DateJoined).HasDefaultValueSql("(getutcdate())");
        });

        m.Entity<GroupStory>(entity =>
        {
            entity.Property(e => e.DateAdded).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.AddedByUser).WithMany(p => p.GroupStories)
                .HasForeignKey(d => d.AddedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        
        m.Entity<Notification>(entity =>
        {
            entity.Property(e => e.DateCreated).HasDefaultValueSql("(getutcdate())");
        });

        //TODO: Need to specify primary key here or not?
        m.Entity<NotificationType>(entity =>
        {
            entity.HasIndex(e => e.NotificationKey, "UQ__Notifica__BEEDDC564E587A3F").IsUnique();
        });

        m.Entity<PrivateMessage>(entity =>
        {
            entity.Property(e => e.DateSent).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.SenderUser).WithMany(p => p.PrivateMessages)
                .HasForeignKey(d => d.SenderUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        m.Entity<Recommendation>(entity =>
        {
            entity.Property(e => e.DatePosted).HasDefaultValueSql("(getutcdate())");
            entity.HasOne(d => d.Recommender).WithMany(p => p.Recommendations)
                .HasForeignKey(d => d.RecommenderId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Recommendations_User");

            entity.HasOne(d => d.Status).WithMany(p => p.Recommendations)
                .HasForeignKey(d => d.StatusId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Recommendations_Status");
        });

        m.Entity<RecommendationStatus>(entity =>
        {
            entity.HasIndex(e => e.StatusName, "UQ__Recommen__05E7698A13C66F32").IsUnique();
        });

        m.Entity<RecommendationSuccess>(entity =>
        {
            entity.Property(e => e.DateRecorded).HasDefaultValueSql("(getutcdate())");
        });

        m.Entity<Report>(entity =>
        {
            entity.Property(e => e.DateReported).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.ReportReason).WithMany(p => p.Reports)
                .HasForeignKey(d => d.ReportReasonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reports_Reason");

            entity.HasOne(d => d.ReportStatus).WithMany(p => p.Reports)
                .HasForeignKey(d => d.ReportStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reports_Status");

            entity.HasOne(d => d.ReporterUser).WithMany(p => p.ReportReporterUsers)
                .HasForeignKey(d => d.ReporterUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Reports_ReporterUser");
        });

        m.Entity<ReportReason>(entity =>
        {
            entity.HasIndex(e => e.ReasonName, "UQ__ReportRe__9D4D92B5B755151C").IsUnique();
        });

        m.Entity<ReportStatus>(entity =>
        {
            entity.HasIndex(e => e.StatusName, "UQ__ReportSt__05E7698AF4224429").IsUnique();
        });

        m.Entity<Series>(entity =>
        {
            entity.Property(e => e.DateCreated).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.Author).WithMany(p => p.Series)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        m.Entity<Story>(entity =>
        {
            entity.HasIndex(e => e.Slug, "IX_Stories_Slug")
                .IsUnique()
                .HasFilter("([Slug] IS NOT NULL)");
            entity.Property(e => e.LastUpdatedDate).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.PublishedDate).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.Author).WithMany(p => p.Stories)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.StoryStatus).WithMany(p => p.Stories)
                .HasForeignKey(d => d.StoryStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        m.Entity<StoryAcknowledgment>(entity =>
        {
            entity.Property(e => e.DateAcknowledged).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.AcknowledgmentRole).WithMany(p => p.StoryAcknowledgments)
                .HasForeignKey(d => d.AcknowledgmentRoleId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        m.Entity<StoryCharacter>(entity =>
        {
            entity.HasIndex(e => new { e.StoryId, e.CharacterTagId }, "UQ_StoryCharacters_StoryTag").IsUnique();
        });

        m.Entity<StoryImport>(entity =>
        {
            entity.HasIndex(e => e.StoryId, "UQ_StoryImports_StoryID").IsUnique();
        });

        m.Entity<StoryRelationship>(entity =>
        {
            entity.Property(e => e.DateCreated).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.RelationshipType).WithMany(p => p.StoryRelationships)
                .HasForeignKey(d => d.RelationshipTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StoryRelationships_Type");

            entity.HasOne(d => d.TargetStory).WithMany(p => p.StoryRelationshipTargetStories)
                .HasForeignKey(d => d.TargetStoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StoryRelationships_ChildStory");
        });

        m.Entity<StoryRelationshipType>(entity =>
        {
            entity.HasIndex(e => e.TypeName, "UQ__StoryRel__D4E7DFA8777A2359").IsUnique();
        });

        m.Entity<StoryTag>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("TR_StoryTags_EnforcePriorityLogic"));
        });

        m.Entity<Tag>(entity =>
        {
            entity.HasIndex(e => new { e.TagName, e.TagTypeId }, "UK_Tags_Name_Type").IsUnique();

            entity.HasOne(d => d.TagType).WithMany(p => p.Tags)
                .HasForeignKey(d => d.TagTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Tags_TagType");
        });

        m.Entity<TagType>(entity =>
        {
            entity.HasIndex(e => e.TypeName, "UQ__TagTypes__D4E7DFA871A634EE").IsUnique();
        });

        m.Entity<UserBadge>(entity =>
        {
            entity.Property(e => e.DateEarned).HasDefaultValueSql("(getutcdate())");
        });

        m.Entity<UserChapterInteraction>(entity =>
        {
            entity.Property(e => e.LastInteractionDate).HasDefaultValueSql("(getutcdate())");
        });
        
        m.Entity<UserSearchSetting>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.SearchModeKey, e.InteractionFilterKey }, "UK_UserSearchSettings").IsUnique();
        });
    }
}