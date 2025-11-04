using System.Diagnostics.CodeAnalysis;
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
    public virtual DbSet<UserStoryInteraction> UserStoryInteractions { get; set; }
    public virtual DbSet<UserStoryInteractionDate> UserStoryInteractionDates { get; set; }
    public virtual DbSet<UserStoryRecommendationSource> UserStoryRecommendationSources { get; set; }
    
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
    
    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        
        #region Enums
        //1. Enum to SMALLINT conversions
        modelBuilder.Entity<Story>().Property(e => e.Rating).HasConversion<byte>();
        modelBuilder.Entity<ChapterContent>().Property(e => e.Rating).HasConversion<byte>();
        modelBuilder.Entity<Group>().Property(e => e.Rating).HasConversion<byte>();
        modelBuilder.Entity<Group>().Property(e => e.MaxContentRating).HasConversion<byte>();
        modelBuilder.Entity<BaseBlogPost>().Property(e => e.Rating).HasConversion<byte>();
        modelBuilder.Entity<GroupFolder>().Property(e => e.MaxRating).HasConversion<byte>();
        
        modelBuilder.Entity<Report>().Property(e => e.ReportedEntityType).HasConversion<byte>();

        modelBuilder.Entity<UserCustomFilter>().Property(e => e.FilterEntityType).HasConversion<byte>();

        modelBuilder.Entity<StoryTag>().Property(e => e.Priority).HasConversion<byte>();
        modelBuilder.Entity<StoryCharacter>().Property(e => e.Priority).HasConversion<byte>();
        modelBuilder.Entity<StoryCharacterRelationship>().Property(e => e.Priority).HasConversion<byte>();
        modelBuilder.Entity<StoryCharacterRelationship>().Property(e => e.RelationshipType).HasConversion<byte>();

        modelBuilder.Entity<StoryRelationship>().Property(e => e.StatusId).HasConversion<byte>();
        
        //1.2 Hybrid - lookup table for UI/description + enum foreign key for application logic
        
        modelBuilder.Entity<StoryStatus>().Property(e => e.StoryStatusId).HasConversion<byte>();
        modelBuilder.Entity<Story>().Property(e => e.StoryStatusId).HasConversion<byte>();
        modelBuilder.Entity<Story>().Property(e => e.PostApprovalStatus).HasConversion<byte>();
        
        modelBuilder.Entity<TagType>().Property(e => e.TagTypeId).HasConversion<byte>();
        modelBuilder.Entity<Tag>().Property(e => e.TagTypeId).HasConversion<byte>();

        modelBuilder.Entity<ReportStatus>().Property(e => e.ReportStatusId).HasConversion<byte>();
        modelBuilder.Entity<Report>().Property(e => e.ReportStatusId).HasConversion<byte>();
        
        modelBuilder.Entity<NotificationCategory>().Property(e => e.NotificationCategoryId).HasConversion<byte>();
        modelBuilder.Entity<NotificationType>().Property(e => e.NotificationCategory).HasConversion<byte>();
        modelBuilder.Entity<NotificationType>().Property(e => e.NotificationTypeId).HasConversion<byte>();
        modelBuilder.Entity<Notification>().Property(e => e.NotificationTypeId).HasConversion<byte>();
        modelBuilder.Entity<UserNotificationSetting>().Property(e => e.NotificationTypeId).HasConversion<byte>();
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
            .HasMany(s => s.DailyStoryStats)
            .WithOne(dss => dss.Story)
            .HasForeignKey(dss => dss.StoryId)
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
            .HasMany(s => s.BlogPosts) // From ProfileBlogPost inheritance
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
            .HasMany(g => g.BlogPosts)
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
            
        // --- Many-to-Many Join Tables (using EF default Cascade) ---
        // User <-> BaseComment (Likes)
        modelBuilder.Entity<BaseComment>()
            .HasMany(c => c.LikedByUsers)
            .WithMany(u => u.LikedComments);
            
        // User <-> BaseBlogPost (Likes)
        modelBuilder.Entity<BaseBlogPost>()
            .HasMany(b => b.LikedByUsers)
            .WithMany(u => u.LikedBlogPosts);

        // User <-> PollOption (Voters)
        modelBuilder.Entity<PollOption>()
            .HasMany(o => o.Voters)
            .WithMany(); // Assuming no nav property on User
            
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
        // Set default for "creation" or "posted" timestamps.
        
        modelBuilder.Entity<BaseBlogPost>(entity =>
        {
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<BaseComment>(entity =>
        {
            entity.Property(e => e.DatePosted)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<BetaReader>(entity =>
        {
            entity.Property(e => e.DateAdded)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<CoAuthor>(entity =>
        {
            entity.Property(e => e.DateAdded)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<CommunitySpotlight>(entity =>
        {
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<CustomList>(entity =>
        {
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<CustomListEntry>(entity =>
        {
            entity.Property(e => e.DateAdded)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<FollowedUser>(entity =>
        {
            entity.Property(e => e.DateFollowed)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        
        modelBuilder.Entity<GroupMember>(entity =>
        {
            entity.Property(e => e.DateJoined)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        
        modelBuilder.Entity<GroupStory>(entity =>
        {
            entity.Property(e => e.DateAdded)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<PrivateMessage>(entity =>
        {
            entity.Property(e => e.DateSent)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        
        modelBuilder.Entity<Recommendation>(entity =>
        {
            entity.Property(e => e.DatePosted)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        
        modelBuilder.Entity<RecommendationSuccess>(entity =>
        {
            entity.Property(e => e.DateRecorded)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Report>(entity =>
        {
            entity.Property(e => e.DateReported)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Series>(entity =>
        {
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<StoryAcknowledgment>(entity =>
        {
            entity.Property(e => e.DateAcknowledged)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        
        modelBuilder.Entity<StoryImport>(entity =>
        {
            entity.Property(e => e.DateImported)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        
        modelBuilder.Entity<StoryRelationship>(entity =>
        {
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        
        modelBuilder.Entity<UserBadge>(entity =>
        {
            entity.Property(e => e.DateEarned)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });


        // --- PART 2: Global Type Configuration Loops ---

        // This loop sets all DateTime properties to 'timestamp with time zone'
        // for proper PostgreSQL handling.
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(e => e.GetProperties())
                     .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?)))
        {
            // Using timestamp(2) for a reasonable precision of 2 decimal places
            property.SetColumnType("timestamp(2) with time zone");
        }
        
        // This loop explicitly sets all DateOnly properties to 'date',
        // which is the correct PostgreSQL type.
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(e => e.GetProperties())
                     .Where(p => p.ClrType == typeof(DateOnly) || p.ClrType == typeof(DateOnly?)))
        {
            property.SetColumnType("date");
        }

        #endregion

        #region Composite Keys, Unique constraints, Table Per Type, and Indexes (to be added by query need)

        modelBuilder.Entity<AcknowledgmentRole>(entity =>
        {
            // Future indexes for querying...
        });

        modelBuilder.Entity<AlsoFavoritedScore>(entity =>
        {
            entity.HasKey(e => new { e.StoryId, e.AlsoFavoritedStoryId });
            // Future indexes for querying...
        });

        modelBuilder.Entity<AlsoRecommendedScore>(entity =>
        {
            entity.HasKey(e => new { e.StoryId, e.AlsoRecommendedStoryId });
            // Future indexes for querying...
        });

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
            // Future indexes for querying...
        });

        modelBuilder.Entity<ChapterComment>(entity =>
        {
            entity.ToTable("chapter_comments");
            // Future indexes for querying (e.g., by ChapterId, DatePosted)...
        });

        modelBuilder.Entity<ChapterContent>(entity =>
        {
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

        modelBuilder.Entity<DailyStoryStat>(entity =>
        {
            entity.HasKey(e => new { e.StoryId, e.StatDate });
            // Future indexes for querying (e.g., by StatDate)...
        });

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

        modelBuilder.Entity<SiteDailyStat>(entity =>
        {
            // This table's PK is the date, so it's already indexed for time-series.
        });

        modelBuilder.Entity<SitePoll>(entity =>
        {
            entity.ToTable("site_polls");
            // Future indexes for querying (e.g., by IsArchived)...
        });

        modelBuilder.Entity<Story>(entity =>
        {
            // A story slug must be unique, but can also be null.
            // A filtered index is perfect for this.
            entity.HasIndex(e => e.Slug).IsUnique()
                .HasFilter("\"Slug\" IS NOT NULL");
            
            // This table will have MANY indexes for searching.
            // Future indexes for querying (e.g., by AuthorId, Rating, StatusId, Dates)...
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
            // Identity handles these, but good to be explicit
            entity.HasIndex(e => e.NormalizedUserName).IsUnique();
            entity.HasIndex(e => e.NormalizedEmail).IsUnique();
            // Future indexes for querying...
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
                .HasFilter("\"is_in_progress\" = true").HasDatabaseName("ix_user_story_interactions_in_progress");
        });

        modelBuilder.Entity<UserStoryInteractionDate>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.StoryId });
            // This table will have filtered indexes on each date column for sorting.
        });

        modelBuilder.Entity<UserStoryRecommendationSource>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.StoryId });
            // Future indexes for querying (e.Full
        });

        modelBuilder.Entity<UserStoryTreeSearchEntry>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.StoryId });
            // This is a data mart. Indexes are critical.
            // Future indexes for querying (e.g., by UserId, by StoryId)...
            // --- Mirrored Graph Indexes (Corrected for snake_case) ---
            // Pattern 1: User -> Stories
            entity.HasIndex(e => e.UserId).IncludeProperties(e => e.StoryId)
                .HasFilter("\"is_authored_by_user\" = true").HasDatabaseName("ix_user_tree_user_authored");
            entity.HasIndex(e => e.UserId).IncludeProperties(e => e.StoryId)
                .HasFilter("\"is_public_favorite\" = true").HasDatabaseName("ix_user_tree_user_public_favorite");
            entity.HasIndex(e => e.UserId).IncludeProperties(e => e.StoryId)
                .HasFilter("\"is_recommendation\" = true").HasDatabaseName("ix_user_tree_user_recommendation");
            entity.HasIndex(e => e.UserId).IncludeProperties(e => e.StoryId)
                .HasFilter("\"is_hidden_gem\" = true").HasDatabaseName("ix_user_tree_user_hidden_gem");
            entity.HasIndex(e => e.UserId).IncludeProperties(e => e.StoryId)
                .HasFilter("\"is_hidden_favorite\" = true").HasDatabaseName("ix_user_tree_user_hidden_favorite");

            // Pattern 2: Story -> Users
            entity.HasIndex(e => e.StoryId).IncludeProperties(e => e.UserId)
                .HasFilter("\"is_authored_by_user\" = true").HasDatabaseName("ix_user_tree_story_authored");
            entity.HasIndex(e => e.StoryId).IncludeProperties(e => e.UserId)
                .HasFilter("\"is_public_favorite\" = true").HasDatabaseName("ix_user_tree_story_public_favorite");
            entity.HasIndex(e => e.StoryId).IncludeProperties(e => e.UserId)
                .HasFilter("\"is_recommendation\" = true").HasDatabaseName("ix_user_tree_story_recommendation");
            entity.HasIndex(e => e.StoryId).IncludeProperties(e => e.UserId)
                .HasFilter("\"is_author_spotlighted\" = true").HasDatabaseName("ix_user_tree_story_spotlighted");
            entity.HasIndex(e => e.StoryId).IncludeProperties(e => e.UserId)
                .HasFilter("\"is_hidden_favorite\" = true").HasDatabaseName("ix_user_tree_story_hidden_favorite");
        });

        #endregion
    }
}
