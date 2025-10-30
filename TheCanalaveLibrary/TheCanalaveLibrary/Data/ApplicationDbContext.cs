using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Models;

namespace TheCanalaveLibrary.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<User, ApplicationRole, int>(options)
{
    public virtual DbSet<AcknowledgmentRole> AcknowledgmentRoles { get; set; }
    
    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Badge> Badges { get; set; }

    public virtual DbSet<BaseComment> BaseComments { get; set; }

    public virtual DbSet<BetaReader> BetaReaders { get; set; }

    public virtual DbSet<BlogPost> BlogPosts { get; set; }

    public virtual DbSet<BlogPostComment> BlogPostComments { get; set; }

    public virtual DbSet<BlogPostLike> BlogPostLikes { get; set; }

    public virtual DbSet<Chapter> Chapters { get; set; }

    public virtual DbSet<ChapterComment> ChapterComments { get; set; }

    public virtual DbSet<ChapterContent> ChapterContents { get; set; }

    public virtual DbSet<CoAuthor> CoAuthors { get; set; }

    public virtual DbSet<CommentLike> CommentLikes { get; set; }

    public virtual DbSet<CommunitySpotlight> CommunitySpotlights { get; set; }

    public virtual DbSet<Conversation> Conversations { get; set; }

    public virtual DbSet<ConversationParticipant> ConversationParticipants { get; set; }

    public virtual DbSet<CustomList> CustomLists { get; set; }

    public virtual DbSet<CustomListEntry> CustomListEntries { get; set; }

    public virtual DbSet<DailyStoryStat> DailyStoryStats { get; set; }

    public virtual DbSet<DefaultSearchSetting> DefaultSearchSettings { get; set; }

    public virtual DbSet<FeatureContribution> FeatureContributions { get; set; }

    public virtual DbSet<FollowedUser> FollowedUsers { get; set; }

    public virtual DbSet<Group> Groups { get; set; }

    public virtual DbSet<GroupComment> GroupComments { get; set; }

    public virtual DbSet<GroupMember> GroupMembers { get; set; }

    public virtual DbSet<GroupStory> GroupStories { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<NotificationType> NotificationTypes { get; set; }

    public virtual DbSet<PrivateMessage> PrivateMessages { get; set; }

    public virtual DbSet<Recommendation> Recommendations { get; set; }

    public virtual DbSet<RecommendationStatus> RecommendationStatuses { get; set; }

    public virtual DbSet<RecommendationSuccess> RecommendationSuccesses { get; set; }

    public virtual DbSet<Report> Reports { get; set; }

    public virtual DbSet<ReportReason> ReportReasons { get; set; }

    public virtual DbSet<ReportStatus> ReportStatuses { get; set; }

    public virtual DbSet<SearchMode> SearchModes { get; set; }

    public virtual DbSet<Series> Series { get; set; }

    public virtual DbSet<SeriesEntry> SeriesEntries { get; set; }

    public virtual DbSet<SettingDetail> SettingDetails { get; set; }

    public virtual DbSet<SiteDailyStat> SiteDailyStats { get; set; }

    public virtual DbSet<Story> Stories { get; set; }

    public virtual DbSet<StoryAcknowledgment> StoryAcknowledgments { get; set; }

    public virtual DbSet<StoryArc> StoryArcs { get; set; }

    public virtual DbSet<StoryCharacter> StoryCharacters { get; set; }

    public virtual DbSet<StoryCharacterRelationship> StoryCharacterRelationships { get; set; }

    public virtual DbSet<StoryImport> StoryImports { get; set; }

    public virtual DbSet<StoryRelationship> StoryRelationships { get; set; }

    public virtual DbSet<StoryRelationshipType> StoryRelationshipTypes { get; set; }

    public virtual DbSet<StoryStatus> StoryStatuses { get; set; }

    public virtual DbSet<StoryTag> StoryTags { get; set; }

    public virtual DbSet<Tag> Tags { get; set; }

    public virtual DbSet<TagType> TagTypes { get; set; }

    public virtual DbSet<UserBadge> UserBadges { get; set; }

    public virtual DbSet<UserChapterInteraction> UserChapterInteractions { get; set; }

    public virtual DbSet<UserCustomFilter> UserCustomFilters { get; set; }

    public virtual DbSet<UserInteractionFilter> UserInteractionFilters { get; set; }

    public virtual DbSet<UserNotificationSetting> UserNotificationSettings { get; set; }

    public virtual DbSet<UserProfileComment> UserProfileComments { get; set; }

    public virtual DbSet<UserSearchSetting> UserSearchSettings { get; set; }

    public virtual DbSet<UserStat> UserStats { get; set; }

    public virtual DbSet<UserStoryInteraction> UserStoryInteractions { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<AcknowledgmentRole>(entity =>
        {
            entity.HasKey(e => e.AcknowledgmentRoleId).HasName("PK__Acknowle__BFE746B6F9FA6285");

            entity.HasIndex(e => e.RoleName, "UQ__Acknowle__8A2B6160FB71EA47").IsUnique();

            entity.Property(e => e.AcknowledgmentRoleId)
                .ValueGeneratedOnAdd()
                .HasColumnName("AcknowledgmentRoleID");
            entity.Property(e => e.RoleName).HasMaxLength(100);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.ProfilePictureUrl).HasMaxLength(500);
            entity.Property(e => e.Tagline).HasMaxLength(256);
            entity.Property(e => e.ThemeName).HasMaxLength(50);
            entity.Property(e => e.UserName).HasMaxLength(256);
        });

        modelBuilder.Entity<Badge>(entity =>
        {
            entity.HasKey(e => e.BadgeKey).HasName("PK__Badges__F2F51BC6CA3A6C82");

            entity.Property(e => e.BadgeKey).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IconUrl)
                .HasMaxLength(500)
                .HasColumnName("IconURL");
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<BaseComment>(entity =>
        {
            entity.HasKey(e => e.CommentId).HasName("PK__BaseComm__C3B4DFAA9FDEA9D3");

            entity.Property(e => e.CommentId).HasColumnName("CommentID");
            entity.Property(e => e.CommentType).HasMaxLength(50);
            entity.Property(e => e.DatePosted).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.ParentCommentId).HasColumnName("ParentCommentID");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.ParentComment).WithMany(p => p.InverseParentComment)
                .HasForeignKey(d => d.ParentCommentId)
                .HasConstraintName("FK_Comments_ParentComment");

            entity.HasOne(d => d.User).WithMany(p => p.BaseComments)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Comments_Users");
        });

        modelBuilder.Entity<BetaReader>(entity =>
        {
            entity.HasKey(e => new { e.StoryId, e.BetaReaderUserId }).HasName("PK_StoryBetaReaders");

            entity.Property(e => e.StoryId).HasColumnName("StoryID");
            entity.Property(e => e.BetaReaderUserId).HasColumnName("BetaReaderUserID");
            entity.Property(e => e.DateAdded).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.BetaReaderUser).WithMany(p => p.BetaReaders)
                .HasForeignKey(d => d.BetaReaderUserId)
                .HasConstraintName("FK_StoryBetaReaders_User");

            entity.HasOne(d => d.Story).WithMany(p => p.BetaReaders)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("FK_StoryBetaReaders_Story");
        });

        modelBuilder.Entity<BlogPost>(entity =>
        {
            entity.HasKey(e => e.BlogPostId).HasName("PK__BlogPost__32174149FA338561");

            entity.Property(e => e.BlogPostId).HasColumnName("BlogPostID");
            entity.Property(e => e.AuthorId).HasColumnName("AuthorID");
            entity.Property(e => e.DateCreated).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.GroupId).HasColumnName("GroupID");
            entity.Property(e => e.LastUpdatedDate).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.StoryId).HasColumnName("StoryID");
            entity.Property(e => e.Title).HasMaxLength(255);

            entity.HasOne(d => d.Author).WithMany(p => p.BlogPosts)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_BlogPosts_Users");

            entity.HasOne(d => d.Group).WithMany(p => p.BlogPosts)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_BlogPosts_Groups");

            entity.HasOne(d => d.Story).WithMany(p => p.BlogPosts)
                .HasForeignKey(d => d.StoryId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_BlogPosts_Stories");
        });

        modelBuilder.Entity<BlogPostComment>(entity =>
        {
            entity.HasKey(e => e.CommentId).HasName("PK__BlogPost__C3B4DFAAC4B32034");

            entity.Property(e => e.CommentId)
                .ValueGeneratedNever()
                .HasColumnName("CommentID");
            entity.Property(e => e.BlogPostId).HasColumnName("BlogPostID");

            entity.HasOne(d => d.BlogPost).WithMany(p => p.BlogPostComments)
                .HasForeignKey(d => d.BlogPostId)
                .HasConstraintName("FK_BlogPostComments_BlogPost");

            entity.HasOne(d => d.Comment).WithOne(p => p.BlogPostComment)
                .HasForeignKey<BlogPostComment>(d => d.CommentId)
                .HasConstraintName("FK_BlogPostComments_BaseComment");
        });

        modelBuilder.Entity<BlogPostLike>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.BlogPostId });

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.BlogPostId).HasColumnName("BlogPostID");
            entity.Property(e => e.DateLiked).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.BlogPost).WithMany(p => p.BlogPostLikes)
                .HasForeignKey(d => d.BlogPostId)
                .HasConstraintName("FK_BlogPostLikes_BlogPosts");

            entity.HasOne(d => d.User).WithMany(p => p.BlogPostLikes)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_BlogPostLikes_Users");
        });

        modelBuilder.Entity<Chapter>(entity =>
        {
            entity.HasKey(e => e.ChapterId).HasName("PK__Chapters__0893A34AB53A2A7A");

            entity.HasIndex(e => new { e.StoryId, e.ChapterNumber }, "UQ_Story_ChapterNumber").IsUnique();

            entity.Property(e => e.ChapterId).HasColumnName("ChapterID");
            entity.Property(e => e.PrimaryContentId).HasColumnName("PrimaryContentID");
            entity.Property(e => e.StoryId).HasColumnName("StoryID");
            entity.Property(e => e.Title).HasMaxLength(255);

            entity.HasOne(d => d.PrimaryContent).WithMany(p => p.Chapters)
                .HasForeignKey(d => d.PrimaryContentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Chapters_PrimaryVersion");

            entity.HasOne(d => d.Story).WithMany(p => p.Chapters)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("FK_Chapters_Stories");
        });

        modelBuilder.Entity<ChapterComment>(entity =>
        {
            entity.HasKey(e => e.CommentId).HasName("PK__ChapterC__C3B4DFAA36BF489D");

            entity.Property(e => e.CommentId)
                .ValueGeneratedNever()
                .HasColumnName("CommentID");
            entity.Property(e => e.ChapterId).HasColumnName("ChapterID");

            entity.HasOne(d => d.Chapter).WithMany(p => p.ChapterComments)
                .HasForeignKey(d => d.ChapterId)
                .HasConstraintName("FK_ChapterComments_Chapter");

            entity.HasOne(d => d.Comment).WithOne(p => p.ChapterComment)
                .HasForeignKey<ChapterComment>(d => d.CommentId)
                .HasConstraintName("FK_ChapterComments_BaseComment");
        });

        modelBuilder.Entity<ChapterContent>(entity =>
        {
            entity.HasKey(e => e.ChapterContentId).HasName("PK__ChapterC__09DC5F277A7A4B24");

            entity.Property(e => e.ChapterContentId).HasColumnName("ChapterContentID");
            entity.Property(e => e.AuthorId).HasColumnName("AuthorID");
            entity.Property(e => e.ChapterId).HasColumnName("ChapterID");
            entity.Property(e => e.PublishDate).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.VersionName).HasMaxLength(100);

            entity.HasOne(d => d.Author).WithMany(p => p.ChapterContents)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ChapterVersions_User");

            entity.HasOne(d => d.Chapter).WithMany(p => p.ChapterContents)
                .HasForeignKey(d => d.ChapterId)
                .HasConstraintName("FK_ChapterVersions_Chapter");
        });

        modelBuilder.Entity<CoAuthor>(entity =>
        {
            entity.HasKey(e => new { e.StoryId, e.CoAuthorUserId }).HasName("PK_StoryCoAuthors");

            entity.Property(e => e.StoryId).HasColumnName("StoryID");
            entity.Property(e => e.CoAuthorUserId).HasColumnName("CoAuthorUserID");
            entity.Property(e => e.DateAdded).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.CoAuthorUser).WithMany(p => p.CoAuthors)
                .HasForeignKey(d => d.CoAuthorUserId)
                .HasConstraintName("FK_StoryCoAuthors_User");

            entity.HasOne(d => d.Story).WithMany(p => p.CoAuthors)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("FK_StoryCoAuthors_Story");
        });

        modelBuilder.Entity<CommentLike>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.CommentId });

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.CommentId).HasColumnName("CommentID");
            entity.Property(e => e.DateLiked).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.Comment).WithMany(p => p.CommentLikes)
                .HasForeignKey(d => d.CommentId)
                .HasConstraintName("FK_CommentLikes_Comment");

            entity.HasOne(d => d.User).WithMany(p => p.CommentLikes)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_CommentLikes_User");
        });

        modelBuilder.Entity<CommunitySpotlight>(entity =>
        {
            entity.HasKey(e => e.SpotlightId).HasName("PK__Communit__FFC7D012B2915523");

            entity.ToTable("CommunitySpotlight");

            entity.Property(e => e.SpotlightId).HasColumnName("SpotlightID");
            entity.Property(e => e.DateCreated).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.PaymentId)
                .HasMaxLength(255)
                .HasColumnName("PaymentID");
            entity.Property(e => e.SponsorComment).HasMaxLength(280);
            entity.Property(e => e.SponsoringUserId).HasColumnName("SponsoringUserID");
            entity.Property(e => e.StoryId).HasColumnName("StoryID");

            entity.HasOne(d => d.SponsoringUser).WithMany(p => p.CommunitySpotlights)
                .HasForeignKey(d => d.SponsoringUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_CommunitySpotlight_Users");

            entity.HasOne(d => d.Story).WithMany(p => p.CommunitySpotlights)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("FK_CommunitySpotlight_Stories");
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.ConversationId).HasName("PK__Conversa__C050D8979E7D93C9");

            entity.Property(e => e.ConversationId).HasColumnName("ConversationID");
            entity.Property(e => e.DateCreated).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Subject).HasMaxLength(255);
        });

        modelBuilder.Entity<ConversationParticipant>(entity =>
        {
            entity.HasKey(e => new { e.ConversationId, e.UserId });

            entity.Property(e => e.ConversationId).HasColumnName("ConversationID");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.Conversation).WithMany(p => p.ConversationParticipants)
                .HasForeignKey(d => d.ConversationId)
                .HasConstraintName("FK_ConversationParticipants_Conversation");

            entity.HasOne(d => d.User).WithMany(p => p.ConversationParticipants)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_ConversationParticipants_User");
        });

        modelBuilder.Entity<CustomList>(entity =>
        {
            entity.HasKey(e => e.ListId).HasName("PK__CustomLi__E3832865FB7AA699");

            entity.HasIndex(e => new { e.UserId, e.ListName }, "UQ_User_ListName").IsUnique();

            entity.Property(e => e.ListId).HasColumnName("ListID");
            entity.Property(e => e.DateCreated).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.ListName).HasMaxLength(100);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.User).WithMany(p => p.CustomLists)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_UserLists_Users");
        });

        modelBuilder.Entity<CustomListEntry>(entity =>
        {
            entity.HasKey(e => new { e.ListId, e.StoryId }).HasName("PK_UserListEntries");

            entity.Property(e => e.ListId).HasColumnName("ListID");
            entity.Property(e => e.StoryId).HasColumnName("StoryID");
            entity.Property(e => e.DateAdded).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.List).WithMany(p => p.CustomListEntries)
                .HasForeignKey(d => d.ListId)
                .HasConstraintName("FK_UserListEntries_UserLists");

            entity.HasOne(d => d.Story).WithMany(p => p.CustomListEntries)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("FK_UserListEntries_Stories");
        });

        modelBuilder.Entity<DailyStoryStat>(entity =>
        {
            entity.HasKey(e => new { e.StoryId, e.StatDate });

            entity.Property(e => e.StoryId).HasColumnName("StoryID");

            entity.HasOne(d => d.Story).WithMany(p => p.DailyStoryStats)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("FK_DailyStoryStats_Stories");
        });

        modelBuilder.Entity<DefaultSearchSetting>(entity =>
        {
            entity.HasKey(e => new { e.SearchModeKey, e.InteractionFilterKey });

            entity.Property(e => e.SearchModeKey).HasMaxLength(50);
            entity.Property(e => e.InteractionFilterKey).HasMaxLength(50);
            entity.Property(e => e.DefaultValue).HasMaxLength(100);

            entity.HasOne(d => d.InteractionFilterKeyNavigation).WithMany(p => p.DefaultSearchSettings)
                .HasForeignKey(d => d.InteractionFilterKey)
                .HasConstraintName("FK_DefaultSearchSettings_Filter");

            entity.HasOne(d => d.SearchModeKeyNavigation).WithMany(p => p.DefaultSearchSettings)
                .HasForeignKey(d => d.SearchModeKey)
                .HasConstraintName("FK_DefaultSearchSettings_Mode");
        });

        modelBuilder.Entity<FeatureContribution>(entity =>
        {
            entity.HasKey(e => e.ContributionId).HasName("PK__FeatureC__6EDA21E482690BF0");

            entity.Property(e => e.ContributionId).HasColumnName("ContributionID");
            entity.Property(e => e.BlogPostId).HasColumnName("BlogPostID");
            entity.Property(e => e.CommentId).HasColumnName("CommentID");
            entity.Property(e => e.DateAwarded).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.FeatureName).HasMaxLength(255);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.BlogPost).WithMany(p => p.FeatureContributions)
                .HasForeignKey(d => d.BlogPostId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_FeatureContributions_BlogPost");

            entity.HasOne(d => d.Comment).WithMany(p => p.FeatureContributions)
                .HasForeignKey(d => d.CommentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_FeatureContributions_Comment");

            entity.HasOne(d => d.User).WithMany(p => p.FeatureContributions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_FeatureContributions_User");
        });

        modelBuilder.Entity<FollowedUser>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.FollowedUserId });

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.FollowedUserId).HasColumnName("FollowedUserID");
            entity.Property(e => e.DateFollowed).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.ReceiveAlerts).HasDefaultValue(true);

            entity.HasOne(d => d.FollowedUserNavigation).WithMany(p => p.FollowedUserFollowedUserNavigations)
                .HasForeignKey(d => d.FollowedUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FollowedUsers_Following");

            entity.HasOne(d => d.User).WithMany(p => p.FollowedUserUsers)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_FollowedUsers_Follower");
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.HasKey(e => e.GroupId).HasName("PK__Groups__149AF30AB0C72705");

            entity.HasIndex(e => e.GroupName, "UQ__Groups__6EFCD4346000DDEF").IsUnique();

            entity.Property(e => e.GroupId).HasColumnName("GroupID");
            entity.Property(e => e.CreatorId).HasColumnName("CreatorID");
            entity.Property(e => e.DateCreated).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.GroupName).HasMaxLength(100);

            entity.HasOne(d => d.Creator).WithMany(p => p.Groups)
                .HasForeignKey(d => d.CreatorId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Groups_Users_Creator");
        });

        modelBuilder.Entity<GroupComment>(entity =>
        {
            entity.HasKey(e => e.CommentId).HasName("PK__GroupCom__C3B4DFAA6B4480C0");

            entity.Property(e => e.CommentId)
                .ValueGeneratedNever()
                .HasColumnName("CommentID");
            entity.Property(e => e.GroupId).HasColumnName("GroupID");

            entity.HasOne(d => d.Comment).WithOne(p => p.GroupComment)
                .HasForeignKey<GroupComment>(d => d.CommentId)
                .HasConstraintName("FK_GroupComments_BaseComment");

            entity.HasOne(d => d.Group).WithMany(p => p.GroupComments)
                .HasForeignKey(d => d.GroupId)
                .HasConstraintName("FK_GroupComments_Group");
        });

        modelBuilder.Entity<GroupMember>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.GroupId });

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.GroupId).HasColumnName("GroupID");
            entity.Property(e => e.DateJoined).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.Group).WithMany(p => p.GroupMembers)
                .HasForeignKey(d => d.GroupId)
                .HasConstraintName("FK_GroupMembers_Groups");

            entity.HasOne(d => d.User).WithMany(p => p.GroupMembers)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_GroupMembers_Users");
        });

        modelBuilder.Entity<GroupStory>(entity =>
        {
            entity.HasKey(e => new { e.GroupId, e.StoryId });

            entity.Property(e => e.GroupId).HasColumnName("GroupID");
            entity.Property(e => e.StoryId).HasColumnName("StoryID");
            entity.Property(e => e.AddedByUserId).HasColumnName("AddedByUserID");
            entity.Property(e => e.DateAdded).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.AddedByUser).WithMany(p => p.GroupStories)
                .HasForeignKey(d => d.AddedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_GroupStories_Users_AddedBy");

            entity.HasOne(d => d.Group).WithMany(p => p.GroupStories)
                .HasForeignKey(d => d.GroupId)
                .HasConstraintName("FK_GroupStories_Groups");

            entity.HasOne(d => d.Story).WithMany(p => p.GroupStories)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("FK_GroupStories_Stories");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E32F0D59736");

            entity.Property(e => e.NotificationId).HasColumnName("NotificationID");
            entity.Property(e => e.DateCreated).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.NotificationTypeId).HasColumnName("NotificationTypeID");
            entity.Property(e => e.RecipientUserId).HasColumnName("RecipientUserID");
            entity.Property(e => e.RelatedEntityId).HasColumnName("RelatedEntityID");
            entity.Property(e => e.SourceUserId).HasColumnName("SourceUserID");

            entity.HasOne(d => d.NotificationType).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.NotificationTypeId)
                .HasConstraintName("FK_Notifications_NotificationType");

            entity.HasOne(d => d.RecipientUser).WithMany(p => p.NotificationRecipientUsers)
                .HasForeignKey(d => d.RecipientUserId)
                .HasConstraintName("FK_Notifications_RecipientUser");

            entity.HasOne(d => d.SourceUser).WithMany(p => p.NotificationSourceUsers)
                .HasForeignKey(d => d.SourceUserId)
                .HasConstraintName("FK_Notifications_SourceUser");
        });

        modelBuilder.Entity<NotificationType>(entity =>
        {
            entity.HasKey(e => e.NotificationTypeId).HasName("PK__Notifica__299002A1316B19D1");

            entity.HasIndex(e => e.NotificationKey, "UQ__Notifica__BEEDDC564E587A3F").IsUnique();

            entity.Property(e => e.NotificationTypeId)
                .ValueGeneratedOnAdd()
                .HasColumnName("NotificationTypeID");
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.DisplayName).HasMaxLength(50);
            entity.Property(e => e.NotificationKey).HasMaxLength(50);
        });

        modelBuilder.Entity<PrivateMessage>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("PK__PrivateM__C87C037C5A08F781");

            entity.Property(e => e.MessageId).HasColumnName("MessageID");
            entity.Property(e => e.ConversationId).HasColumnName("ConversationID");
            entity.Property(e => e.DateSent).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.SenderUserId).HasColumnName("SenderUserID");

            entity.HasOne(d => d.Conversation).WithMany(p => p.PrivateMessages)
                .HasForeignKey(d => d.ConversationId)
                .HasConstraintName("FK_PrivateMessages_Conversation");

            entity.HasOne(d => d.SenderUser).WithMany(p => p.PrivateMessages)
                .HasForeignKey(d => d.SenderUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_PrivateMessages_SenderUser");
        });

        modelBuilder.Entity<Recommendation>(entity =>
        {
            entity.HasKey(e => e.RecommendationId).HasName("PK__Recommen__AA15BEC4A5154F02");

            entity.HasIndex(e => new { e.RecommenderId, e.StoryId }, "UQ_User_Story_Recommendation").IsUnique();

            entity.Property(e => e.RecommendationId).HasColumnName("RecommendationID");
            entity.Property(e => e.DatePosted).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.IsHighlightedByAuthor).HasDefaultValue(true);
            entity.Property(e => e.RecommenderId).HasColumnName("RecommenderID");
            entity.Property(e => e.StatusId)
                .HasDefaultValue((byte)1)
                .HasColumnName("StatusID");
            entity.Property(e => e.StoryId).HasColumnName("StoryID");

            entity.HasOne(d => d.Recommender).WithMany(p => p.Recommendations)
                .HasForeignKey(d => d.RecommenderId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Recommendations_User");

            entity.HasOne(d => d.Status).WithMany(p => p.Recommendations)
                .HasForeignKey(d => d.StatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Recommendations_Status");

            entity.HasOne(d => d.Story).WithMany(p => p.Recommendations)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("FK_Recommendations_Story");
        });

        modelBuilder.Entity<RecommendationStatus>(entity =>
        {
            entity.HasKey(e => e.StatusId).HasName("PK__Recommen__C8EE2043FD94E2C8");

            entity.HasIndex(e => e.StatusName, "UQ__Recommen__05E7698A13C66F32").IsUnique();

            entity.Property(e => e.StatusId)
                .ValueGeneratedOnAdd()
                .HasColumnName("StatusID");
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.StatusName).HasMaxLength(50);
        });

        modelBuilder.Entity<RecommendationSuccess>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RecommendationId }).HasName("PK_RecommendationLikes");

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.RecommendationId).HasColumnName("RecommendationID");
            entity.Property(e => e.DateRecorded).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.Recommendation).WithMany(p => p.RecommendationSuccesses)
                .HasForeignKey(d => d.RecommendationId)
                .HasConstraintName("FK_RecommendationLikes_Recommendations");

            entity.HasOne(d => d.User).WithMany(p => p.RecommendationSuccesses)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_RecommendationLikes_Users");
        });

        modelBuilder.Entity<Report>(entity =>
        {
            entity.HasKey(e => e.ReportId).HasName("PK__Reports__D5BD48E5DAF97383");

            entity.Property(e => e.ReportId).HasColumnName("ReportID");
            entity.Property(e => e.ActionTaken).HasMaxLength(255);
            entity.Property(e => e.DateReported).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.ModeratorUserId).HasColumnName("ModeratorUserID");
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.ReportReasonId).HasColumnName("ReportReasonID");
            entity.Property(e => e.ReportStatusId)
                .HasDefaultValue((byte)1)
                .HasColumnName("ReportStatusID");
            entity.Property(e => e.ReportedEntityId).HasColumnName("ReportedEntityID");
            entity.Property(e => e.ReportedEntityTypeId).HasColumnName("ReportedEntityTypeID");
            entity.Property(e => e.ReporterUserId).HasColumnName("ReporterUserID");

            entity.HasOne(d => d.ModeratorUser).WithMany(p => p.ReportModeratorUsers)
                .HasForeignKey(d => d.ModeratorUserId)
                .HasConstraintName("FK_Reports_ModeratorUser");

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

        modelBuilder.Entity<ReportReason>(entity =>
        {
            entity.HasKey(e => e.ReportReasonId).HasName("PK__ReportRe__20581B8E6278EE05");

            entity.HasIndex(e => e.ReasonName, "UQ__ReportRe__9D4D92B5B755151C").IsUnique();

            entity.Property(e => e.ReportReasonId)
                .ValueGeneratedOnAdd()
                .HasColumnName("ReportReasonID");
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.ReasonName).HasMaxLength(100);
        });

        modelBuilder.Entity<ReportStatus>(entity =>
        {
            entity.HasKey(e => e.ReportStatusId).HasName("PK__ReportSt__9683C126DD1A262B");

            entity.HasIndex(e => e.StatusName, "UQ__ReportSt__05E7698AF4224429").IsUnique();

            entity.Property(e => e.ReportStatusId)
                .ValueGeneratedOnAdd()
                .HasColumnName("ReportStatusID");
            entity.Property(e => e.StatusName).HasMaxLength(50);
        });

        modelBuilder.Entity<SearchMode>(entity =>
        {
            entity.HasKey(e => e.SearchModeKey).HasName("PK__SearchMo__E4FB3A97846B77B8");

            entity.Property(e => e.SearchModeKey).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Series>(entity =>
        {
            entity.HasKey(e => e.SeriesId).HasName("PK__Series__F3A1C101E30376F6");

            entity.Property(e => e.SeriesId).HasColumnName("SeriesID");
            entity.Property(e => e.AuthorId).HasColumnName("AuthorID");
            entity.Property(e => e.DateCreated).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Name).HasMaxLength(255);

            entity.HasOne(d => d.Author).WithMany(p => p.Series)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Series_AspNetUsers");
        });

        modelBuilder.Entity<SeriesEntry>(entity =>
        {
            entity.HasKey(e => new { e.SeriesId, e.StoryId });

            entity.Property(e => e.SeriesId).HasColumnName("SeriesID");
            entity.Property(e => e.StoryId).HasColumnName("StoryID");

            entity.HasOne(d => d.Series).WithMany(p => p.SeriesEntries)
                .HasForeignKey(d => d.SeriesId)
                .HasConstraintName("FK_SeriesEntries_Series");

            entity.HasOne(d => d.Story).WithMany(p => p.SeriesEntries)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("FK_SeriesEntries_Stories");
        });

        modelBuilder.Entity<SettingDetail>(entity =>
        {
            entity.HasKey(e => e.SettingDetailId).HasName("PK__SettingD__30C3B74C6FC953CC");

            entity.HasIndex(e => new { e.StoryId, e.BaseTagId }, "UQ_SettingDetails_Story_Tag").IsUnique();

            entity.Property(e => e.SettingDetailId).HasColumnName("SettingDetailID");
            entity.Property(e => e.BaseTagId).HasColumnName("BaseTagID");
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.StoryId).HasColumnName("StoryID");

            entity.HasOne(d => d.BaseTag).WithMany(p => p.SettingDetails)
                .HasForeignKey(d => d.BaseTagId)
                .HasConstraintName("FK_SettingDetails_Tag");

            entity.HasOne(d => d.Story).WithMany(p => p.SettingDetails)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("FK_SettingDetails_Story");
        });

        modelBuilder.Entity<SiteDailyStat>(entity =>
        {
            entity.HasKey(e => e.StatDate).HasName("PK__SiteDail__255A932C4D3FD608");
        });

        modelBuilder.Entity<Story>(entity =>
        {
            entity.HasKey(e => e.StoryId).HasName("PK__Stories__3E82C028CCB92BA7");

            entity.HasIndex(e => e.Slug, "IX_Stories_Slug")
                .IsUnique()
                .HasFilter("([Slug] IS NOT NULL)");

            entity.Property(e => e.StoryId).HasColumnName("StoryID");
            entity.Property(e => e.AuthorId).HasColumnName("AuthorID");
            entity.Property(e => e.CoverArtUrl)
                .HasMaxLength(500)
                .HasColumnName("CoverArtURL");
            entity.Property(e => e.LastUpdatedDate).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.PostApprovalStatus).HasMaxLength(30);
            entity.Property(e => e.PublishedDate).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Rating).HasDefaultValue((byte)1);
            entity.Property(e => e.ShortDescription).HasMaxLength(500);
            entity.Property(e => e.Slug).HasMaxLength(255);
            entity.Property(e => e.StoryStatusId)
                .HasDefaultValue((byte)1)
                .HasColumnName("StoryStatusID");
            entity.Property(e => e.StoryTitle).HasMaxLength(255);

            entity.HasOne(d => d.Author).WithMany(p => p.Stories)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Stories_AspNetUsers");

            entity.HasOne(d => d.StoryStatus).WithMany(p => p.Stories)
                .HasForeignKey(d => d.StoryStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Stories_StoryStatus");
        });

        modelBuilder.Entity<StoryAcknowledgment>(entity =>
        {
            entity.HasKey(e => new { e.StoryId, e.AcknowledgedUserId, e.AcknowledgmentRoleId });

            entity.Property(e => e.StoryId).HasColumnName("StoryID");
            entity.Property(e => e.AcknowledgedUserId).HasColumnName("AcknowledgedUserID");
            entity.Property(e => e.AcknowledgmentRoleId).HasColumnName("AcknowledgmentRoleID");
            entity.Property(e => e.DateAcknowledged).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.AcknowledgedUser).WithMany(p => p.StoryAcknowledgments)
                .HasForeignKey(d => d.AcknowledgedUserId)
                .HasConstraintName("FK_StoryAcknowledgments_User");

            entity.HasOne(d => d.AcknowledgmentRole).WithMany(p => p.StoryAcknowledgments)
                .HasForeignKey(d => d.AcknowledgmentRoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StoryAcknowledgments_Role");

            entity.HasOne(d => d.Story).WithMany(p => p.StoryAcknowledgments)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("FK_StoryAcknowledgments_Story");
        });

        modelBuilder.Entity<StoryArc>(entity =>
        {
            entity.HasKey(e => e.StoryArcId).HasName("PK__StoryArc__2DA1A084F45F43E5");

            entity.Property(e => e.StoryArcId).HasColumnName("StoryArcID");
            entity.Property(e => e.StoryId).HasColumnName("StoryID");
            entity.Property(e => e.Title).HasMaxLength(255);

            entity.HasOne(d => d.Story).WithMany(p => p.StoryArcs)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("FK_StoryArcs_Story");
        });

        modelBuilder.Entity<StoryCharacter>(entity =>
        {
            entity.HasKey(e => e.StoryCharacterId).HasName("PK__StoryCha__241D2E22DD4B8658");

            entity.ToTable(tb => tb.HasTrigger("TR_StoryCharacters_EnforceOCLogic"));

            entity.HasIndex(e => new { e.StoryId, e.CharacterTagId }, "UQ_StoryCharacters_StoryTag").IsUnique();

            entity.Property(e => e.StoryCharacterId).HasColumnName("StoryCharacterID");
            entity.Property(e => e.CharacterTagId).HasColumnName("CharacterTagID");
            entity.Property(e => e.IsOc).HasColumnName("IsOC");
            entity.Property(e => e.OcBio)
                .HasMaxLength(1000)
                .HasColumnName("OC_Bio");
            entity.Property(e => e.OcName)
                .HasMaxLength(100)
                .HasColumnName("OC_Name");
            entity.Property(e => e.StoryId).HasColumnName("StoryID");

            entity.HasOne(d => d.CharacterTag).WithMany(p => p.StoryCharacters)
                .HasForeignKey(d => d.CharacterTagId)
                .HasConstraintName("FK_StoryCharacters_Tag");

            entity.HasOne(d => d.Story).WithMany(p => p.StoryCharacters)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("FK_StoryCharacters_Story");
        });

        modelBuilder.Entity<StoryCharacterRelationship>(entity =>
        {
            entity.HasKey(e => e.StoryCharacterRelationshipId).HasName("PK__StoryCha__C9328EC700D331A7");

            entity.Property(e => e.StoryCharacterRelationshipId).HasColumnName("StoryCharacterRelationshipID");
            entity.Property(e => e.StoryId).HasColumnName("StoryID");

            entity.HasOne(d => d.Story).WithMany(p => p.StoryCharacterRelationships)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("FK_StoryCharacterRelationships_Story");

            entity.HasMany(d => d.StoryCharacters).WithMany(p => p.StoryCharacterRelationships)
                .UsingEntity<Dictionary<string, object>>(
                    "StoryCharacterRelationshipMember",
                    r => r.HasOne<StoryCharacter>().WithMany()
                        .HasForeignKey("StoryCharacterId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_StoryCharRelationshipMembers_Char"),
                    l => l.HasOne<StoryCharacterRelationship>().WithMany()
                        .HasForeignKey("StoryCharacterRelationshipId")
                        .HasConstraintName("FK_StoryCharRelationshipMembers_Rel"),
                    j =>
                    {
                        j.HasKey("StoryCharacterRelationshipId", "StoryCharacterId");
                        j.ToTable("StoryCharacterRelationshipMembers");
                        j.IndexerProperty<int>("StoryCharacterRelationshipId").HasColumnName("StoryCharacterRelationshipID");
                        j.IndexerProperty<int>("StoryCharacterId").HasColumnName("StoryCharacterID");
                    });
        });

        modelBuilder.Entity<StoryImport>(entity =>
        {
            entity.HasKey(e => e.ImportId).HasName("PK__StoryImp__8697678AA5B82205");

            entity.HasIndex(e => e.StoryId, "UQ_StoryImports_StoryID").IsUnique();

            entity.Property(e => e.ImportId).HasColumnName("ImportID");
            entity.Property(e => e.DateImported).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.SourcePlatform).HasMaxLength(50);
            entity.Property(e => e.SourceUrl)
                .HasMaxLength(500)
                .HasColumnName("SourceURL");
            entity.Property(e => e.StoryId).HasColumnName("StoryID");

            entity.HasOne(d => d.Story).WithOne(p => p.StoryImport)
                .HasForeignKey<StoryImport>(d => d.StoryId)
                .HasConstraintName("FK_StoryImports_Story");
        });

        modelBuilder.Entity<StoryRelationship>(entity =>
        {
            entity.HasKey(e => new { e.SourceStoryId, e.TargetStoryId, e.RelationshipTypeId });

            entity.Property(e => e.SourceStoryId).HasColumnName("SourceStoryID");
            entity.Property(e => e.TargetStoryId).HasColumnName("TargetStoryID");
            entity.Property(e => e.RelationshipTypeId).HasColumnName("RelationshipTypeID");
            entity.Property(e => e.DateCreated).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.StatusId)
                .HasDefaultValue((byte)1)
                .HasColumnName("StatusID");

            entity.HasOne(d => d.RelationshipType).WithMany(p => p.StoryRelationships)
                .HasForeignKey(d => d.RelationshipTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StoryRelationships_Type");

            entity.HasOne(d => d.SourceStory).WithMany(p => p.StoryRelationshipSourceStories)
                .HasForeignKey(d => d.SourceStoryId)
                .HasConstraintName("FK_StoryRelationships_ParentStory");

            entity.HasOne(d => d.TargetStory).WithMany(p => p.StoryRelationshipTargetStories)
                .HasForeignKey(d => d.TargetStoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StoryRelationships_ChildStory");
        });

        modelBuilder.Entity<StoryRelationshipType>(entity =>
        {
            entity.HasKey(e => e.RelationshipTypeId).HasName("PK__StoryRel__20FE5F6141F68546");

            entity.HasIndex(e => e.TypeName, "UQ__StoryRel__D4E7DFA8777A2359").IsUnique();

            entity.Property(e => e.RelationshipTypeId)
                .ValueGeneratedOnAdd()
                .HasColumnName("RelationshipTypeID");
            entity.Property(e => e.TypeName).HasMaxLength(50);
        });

        modelBuilder.Entity<StoryStatus>(entity =>
        {
            entity.HasKey(e => e.StoryStatusId).HasName("PK__StorySta__4D3117367BBF5BDF");

            entity.Property(e => e.StoryStatusId)
                .ValueGeneratedOnAdd()
                .HasColumnName("StoryStatusID");
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.StatusName).HasMaxLength(50);
        });

        modelBuilder.Entity<StoryTag>(entity =>
        {
            entity.HasKey(e => new { e.StoryId, e.TagId });

            entity.ToTable(tb => tb.HasTrigger("TR_StoryTags_EnforcePriorityLogic"));

            entity.Property(e => e.StoryId).HasColumnName("StoryID");
            entity.Property(e => e.TagId).HasColumnName("TagID");

            entity.HasOne(d => d.Story).WithMany(p => p.StoryTags)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("FK_StoryTags_Story");

            entity.HasOne(d => d.Tag).WithMany(p => p.StoryTags)
                .HasForeignKey(d => d.TagId)
                .HasConstraintName("FK_StoryTags_Tag");
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.TagId).HasName("PK__Tags__657CFA4C49577C66");

            entity.HasIndex(e => new { e.TagName, e.TagTypeId }, "UK_Tags_Name_Type").IsUnique();

            entity.Property(e => e.TagId).HasColumnName("TagID");
            entity.Property(e => e.AllowOcdetails).HasColumnName("AllowOCDetails");
            entity.Property(e => e.AnimatedSpriteUrl)
                .HasMaxLength(500)
                .HasColumnName("AnimatedSpriteURL");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.ParentTagId).HasColumnName("ParentTagID");
            entity.Property(e => e.SpriteUrl)
                .HasMaxLength(500)
                .HasColumnName("SpriteURL");
            entity.Property(e => e.TagName).HasMaxLength(100);
            entity.Property(e => e.TagTypeId).HasColumnName("TagTypeID");

            entity.HasOne(d => d.ParentTag).WithMany(p => p.InverseParentTag)
                .HasForeignKey(d => d.ParentTagId)
                .HasConstraintName("FK_Tags_ParentTag");

            entity.HasOne(d => d.TagType).WithMany(p => p.Tags)
                .HasForeignKey(d => d.TagTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Tags_TagType");
        });

        modelBuilder.Entity<TagType>(entity =>
        {
            entity.HasKey(e => e.TagTypeId).HasName("PK__TagTypes__BEE8E8CB7CFEA1F8");

            entity.HasIndex(e => e.TypeName, "UQ__TagTypes__D4E7DFA871A634EE").IsUnique();

            entity.Property(e => e.TagTypeId)
                .ValueGeneratedOnAdd()
                .HasColumnName("TagTypeID");
            entity.Property(e => e.TypeName).HasMaxLength(50);
        });

        modelBuilder.Entity<UserBadge>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.BadgeKey });

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.BadgeKey).HasMaxLength(50);
            entity.Property(e => e.DateEarned).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.BadgeKeyNavigation).WithMany(p => p.UserBadges)
                .HasForeignKey(d => d.BadgeKey)
                .HasConstraintName("FK_UserBadges_Badge");

            entity.HasOne(d => d.User).WithMany(p => p.UserBadges)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_UserBadges_User");
        });

        modelBuilder.Entity<UserChapterInteraction>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.ChapterId });

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.ChapterId).HasColumnName("ChapterID");
            entity.Property(e => e.LastInteractionDate).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.Chapter).WithMany(p => p.UserChapterInteractions)
                .HasForeignKey(d => d.ChapterId)
                .HasConstraintName("FK_UserChapterInteractions_Chapters");

            entity.HasOne(d => d.User).WithMany(p => p.UserChapterInteractions)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_UserChapterInteractions_Users");
        });

        modelBuilder.Entity<UserCustomFilter>(entity =>
        {
            entity.HasKey(e => e.UserCustomFilterId).HasName("PK__UserCust__0EFEF340DCC6B614");

            entity.Property(e => e.UserCustomFilterId).HasColumnName("UserCustomFilterID");
            entity.Property(e => e.EntityId).HasColumnName("EntityID");
            entity.Property(e => e.FilterTypeId).HasColumnName("FilterTypeID");
            entity.Property(e => e.Include).HasDefaultValue(true);
            entity.Property(e => e.SearchModeKey).HasMaxLength(50);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.SearchModeKeyNavigation).WithMany(p => p.UserCustomFilters)
                .HasForeignKey(d => d.SearchModeKey)
                .HasConstraintName("FK_UserCustomFilters_SearchMode");

            entity.HasOne(d => d.User).WithMany(p => p.UserCustomFilters)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_UserCustomFilters_User");
        });

        modelBuilder.Entity<UserInteractionFilter>(entity =>
        {
            entity.HasKey(e => e.InteractionFilterKey).HasName("PK__UserInte__623CCDF96E2E3748");

            entity.Property(e => e.InteractionFilterKey).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<UserNotificationSetting>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.NotificationTypeId });

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.NotificationTypeId).HasColumnName("NotificationTypeID");

            entity.HasOne(d => d.NotificationType).WithMany(p => p.UserNotificationSettings)
                .HasForeignKey(d => d.NotificationTypeId)
                .HasConstraintName("FK_UserNotificationSettings_Type");

            entity.HasOne(d => d.User).WithMany(p => p.UserNotificationSettings)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_UserNotificationSettings_User");
        });

        modelBuilder.Entity<UserProfileComment>(entity =>
        {
            entity.HasKey(e => e.CommentId).HasName("PK__UserProf__C3B4DFAA54D9BD04");

            entity.Property(e => e.CommentId)
                .ValueGeneratedNever()
                .HasColumnName("CommentID");
            entity.Property(e => e.ProfileUserId).HasColumnName("ProfileUserID");

            entity.HasOne(d => d.Comment).WithOne(p => p.UserProfileComment)
                .HasForeignKey<UserProfileComment>(d => d.CommentId)
                .HasConstraintName("FK_UserProfileComments_BaseComment");

            entity.HasOne(d => d.ProfileUser).WithMany(p => p.UserProfileComments)
                .HasForeignKey(d => d.ProfileUserId)
                .HasConstraintName("FK_UserProfileComments_User");
        });

        modelBuilder.Entity<UserSearchSetting>(entity =>
        {
            entity.HasKey(e => e.UserSearchSettingId).HasName("PK__UserSear__DBC2857F71BD56A2");

            entity.HasIndex(e => new { e.UserId, e.SearchModeKey, e.InteractionFilterKey }, "UK_UserSearchSettings").IsUnique();

            entity.Property(e => e.UserSearchSettingId).HasColumnName("UserSearchSettingID");
            entity.Property(e => e.InteractionFilterKey).HasMaxLength(50);
            entity.Property(e => e.SearchModeKey).HasMaxLength(50);
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.Value).HasMaxLength(100);

            entity.HasOne(d => d.InteractionFilterKeyNavigation).WithMany(p => p.UserSearchSettings)
                .HasForeignKey(d => d.InteractionFilterKey)
                .HasConstraintName("FK_UserSearchSettings_Filter");

            entity.HasOne(d => d.SearchModeKeyNavigation).WithMany(p => p.UserSearchSettings)
                .HasForeignKey(d => d.SearchModeKey)
                .HasConstraintName("FK_UserSearchSettings_Mode");

            entity.HasOne(d => d.User).WithMany(p => p.UserSearchSettings)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_UserSearchSettings_User");
        });

        modelBuilder.Entity<UserStat>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__UserStat__1788CCACF82F3157");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("UserID");

            entity.HasOne(d => d.User).WithOne(p => p.UserStat)
                .HasForeignKey<UserStat>(d => d.UserId)
                .HasConstraintName("FK_UserStats_User");
        });

        modelBuilder.Entity<UserStoryInteraction>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.StoryId });

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.StoryId).HasColumnName("StoryID");
            entity.Property(e => e.SourceRecommendationId).HasColumnName("SourceRecommendationID");

            entity.HasOne(d => d.SourceRecommendation).WithMany(p => p.UserStoryInteractions)
                .HasForeignKey(d => d.SourceRecommendationId)
                .HasConstraintName("FK_UserStoryInteractions_SourceRecommendation");

            entity.HasOne(d => d.Story).WithMany(p => p.UserStoryInteractions)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("FK_UserStoryInteractions_Stories");

            entity.HasOne(d => d.User).WithMany(p => p.UserStoryInteractions)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_UserStoryInteractions_Users");
        });
    }
}