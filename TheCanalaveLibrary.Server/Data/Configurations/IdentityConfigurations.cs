using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

// --- USER (The most complex entity) ---
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // WU-SiteDailyStat (Feature 62): CreatedUtc sources new_users/total_users; LastActiveUtc
        // is signal-buffered (never a tracked write from request code — see UserActivityBuffer).
        builder.Property(u => u.CreatedUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(u => u.CreatedUtc).HasDatabaseName("ix_users_created_utc");

        // 1-to-1 Cascade (Personal data that MUST be deleted with the user)
        builder.HasOne(u => u.UserStat)
            .WithOne(s => s.User)
            .HasForeignKey<UserStat>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1-to-Many Cascade (Personal data owned by the user)
        builder.HasMany(u => u.CustomLists)
            .WithOne(l => l.User)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.UserBadges)
            .WithOne(b => b.User)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.UserNotificationSettings)
            .WithOne(s => s.User)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.ConversationParticipants)
            .WithOne(p => p.User)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.UserChapterInteractions)
            .WithOne(i => i.User)
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.UserStoryInteractions)
            .WithOne(i => i.User)
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.GroupMembers)
            .WithOne(m => m.User)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.UserCustomFilters)
            .WithOne(f => f.User)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.UserStoryInteractionFilterSettings)
            .WithOne(s => s.User)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.RecommendationSuccesses)
            .WithOne(r => r.User)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.StoryAcknowledgments)
            .WithOne(a => a.AcknowledgedUser)
            .HasForeignKey(a => a.AcknowledgedUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.CoAuthors)
            .WithOne(c => c.CoAuthorUser)
            .HasForeignKey(c => c.CoAuthorUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.BetaReaders)
            .WithOne(b => b.BetaReaderUser)
            .HasForeignKey(b => b.BetaReaderUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1-to-Many SetNull (Anonymize created content)
        // This policy is CRITICAL for breaking all "diamond" conflicts.
        builder.HasMany(u => u.Stories)
            .WithOne(s => s.Author)
            .HasForeignKey(s => s.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(u => u.BaseComments)
            .WithOne(c => c.Author)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(u => u.BlogPosts)
            .WithOne(b => b.Author)
            .HasForeignKey(b => b.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(u => u.Recommendations)
            .WithOne(r => r.Recommender)
            .HasForeignKey(r => r.RecommenderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(u => u.Series)
            .WithOne(s => s.Author)
            .HasForeignKey(s => s.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(u => u.Groups)
            .WithOne(g => g.Creator)
            .HasForeignKey(g => g.CreatorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(u => u.GroupStories)
            .WithOne(gs => gs.AddedByUser)
            .HasForeignKey(gs => gs.AddedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(u => u.PrivateMessages)
            .WithOne(p => p.SenderUser)
            .HasForeignKey(p => p.SenderUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(u => u.CommunitySpotlights)
            .WithOne(cs => cs.SponsoringUser)
            .HasForeignKey(cs => cs.SponsoringUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(u => u.ChapterContents)
            .WithOne(cc => cc.Author)
            .HasForeignKey(cc => cc.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(u => u.FeatureContributions)
            .WithOne(fc => fc.User)
            .HasForeignKey(fc => fc.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(u => u.ReportReporterUsers)
            .WithOne(r => r.ReporterUser)
            .HasForeignKey(r => r.ReporterUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(u => u.ReportModeratorUsers)
            .WithOne(r => r.ModeratorUser)
            .HasForeignKey(r => r.ModeratorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // 1-to-Many Restrict (Lookup tables or conflicts)
        builder.HasMany(u => u.UserProfileComments)
            .WithOne(c => c.ProfileUser)
            .HasForeignKey(c => c.ProfileUserId)
            .OnDelete(DeleteBehavior.Restrict); // CONFLICT: Solved with C# code.

        // --- DIRECT CONFLICTS (require C# code) ---
        builder.HasMany(u => u.FollowedUserUsers)
            .WithOne(f => f.User)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade); // The "follower"

        builder.HasMany(u => u.FollowedUserFollowedUserNavigations)
            .WithOne(f => f.FollowedUserNavigation)
            .HasForeignKey(f => f.FollowedUserId)
            .OnDelete(DeleteBehavior.Restrict); // CONFLICT: The "followed"

        builder.HasMany(u => u.NotificationRecipientUsers)
            .WithOne(n => n.RecipientUser)
            .HasForeignKey(n => n.RecipientUserId)
            .OnDelete(DeleteBehavior.Cascade); // Required FK

        builder.HasMany(u => u.NotificationSourceUsers)
            .WithOne(n => n.SourceUser)
            .HasForeignKey(n => n.SourceUserId)
            .OnDelete(DeleteBehavior.Restrict); // CONFLICT: Nullable FK

        // Identity indexes
        builder.HasIndex(e => e.NormalizedUserName).IsUnique();
        builder.HasIndex(e => e.NormalizedEmail).IsUnique();

        // Future indexes for querying (e.g., on show_mature_content)...

        // --- JSON complex types (EF Core 10 ComplexProperty + ToJson) ---
        builder.ComplexProperty(u => u.ReaderSettings, b =>
        {
            b.ToJson();
            b.Property(s => s.DefaultSearchSort).HasConversion<short>();
            b.Property(s => s.ReadingBackground).HasConversion<short>();
        });

        builder.ComplexProperty(u => u.PrivacySettings, b =>
        {
            b.ToJson();
            b.Property(s => s.ProfileVisibility).HasConversion<short>();
            b.Property(s => s.AllowProfileComments).HasConversion<short>();
            b.Property(s => s.AllowPrivateMessages).HasConversion<short>();
        });

        builder.ComplexProperty(u => u.AuthorSettings, b =>
        {
            b.ToJson();
            b.Property(s => s.DefaultStoryRating).HasConversion<short>();
        });
    }
}

public sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.HasIndex(e => e.NormalizedName).IsUnique();
        // Future indexes for querying...

        builder.HasData(
            new { Id = (int)SiteRoles.User, Name = "User", NormalizedName = "USER", ConcurrencyStamp = "1" },
            new { Id = (int)SiteRoles.Moderator, Name = "Moderator", NormalizedName = "MODERATOR", ConcurrencyStamp = "2" },
            new { Id = (int)SiteRoles.Admin, Name = "Admin", NormalizedName = "ADMIN", ConcurrencyStamp = "3" }
        );
    }
}
