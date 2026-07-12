using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

public sealed class BaseBlogPostConfiguration : IEntityTypeConfiguration<BaseBlogPost>
{
    public void Configure(EntityTypeBuilder<BaseBlogPost> builder)
    {
        // --- Diamond-Breaking SetNulls (Already covered by User SetNull) ---
        // Example: FeatureContribution.BlogPostId -> BaseBlogPost
        builder.HasMany(b => b.FeatureContributions)
            .WithOne(fc => fc.BlogPost)
            .HasForeignKey(fc => fc.BlogPostId)
            .OnDelete(DeleteBehavior.SetNull); // Breaks diamond

        builder.ToTable("base_blog_posts");
    }
}

public sealed class ProfileBlogPostConfiguration : IEntityTypeConfiguration<ProfileBlogPost>
{
    public void Configure(EntityTypeBuilder<ProfileBlogPost> builder)
    {
        builder.Property(e => e.Rating).HasConversion<short>();
        builder.Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.ToTable("profile_blog_posts");
    }
}

public sealed class GroupBlogPostConfiguration : IEntityTypeConfiguration<GroupBlogPost>
{
    public void Configure(EntityTypeBuilder<GroupBlogPost> builder)
    {
        builder.Property(e => e.Rating).HasConversion<short>();
        builder.Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.ToTable("group_blog_posts");
    }
}

public sealed class BasePollConfiguration : IEntityTypeConfiguration<BasePoll>
{
    public void Configure(EntityTypeBuilder<BasePoll> builder)
    {
        builder.Property(e => e.DateOpened).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(e => e.ResultsVisibility).HasConversion<short>();
        builder.Property(e => e.AnonymityMode).HasConversion<short>();

        builder.HasMany(p => p.PollOptions)
            .WithOne(o => o.Poll)
            .HasForeignKey(o => o.PollId)
            .OnDelete(DeleteBehavior.Cascade);

        // Edit-notification sweep: polls where last_edited_at is set scan via this partial index
        // (PollEditNotificationWorker, 30-min quiet period).
        builder.HasIndex(e => e.LastEditedAt)
            .HasFilter("last_edited_at IS NOT NULL")
            .HasDatabaseName("ix_base_polls_last_edited_at");

        // TPT Inheritance setup
        builder.ToTable("base_polls");
    }
}

public sealed class SitePollConfiguration : IEntityTypeConfiguration<SitePoll>
{
    public void Configure(EntityTypeBuilder<SitePoll> builder)
    {
        builder.ToTable("site_polls");
    }
}

public sealed class BlogPostPollConfiguration : IEntityTypeConfiguration<BlogPostPoll>
{
    public void Configure(EntityTypeBuilder<BlogPostPoll> builder)
    {
        // Explicit pairing with BaseBlogPost.Polls (ICollection<BlogPostPoll>) — kills the
        // spurious shadow-FK relationship the old ICollection<BasePoll> typing created.
        // Cascade: deleting a blog post deletes its polls (options + votes cascade from there).
        builder.HasOne(p => p.BlogPost)
            .WithMany(b => b.Polls)
            .HasForeignKey(p => p.BlogPostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("blog_post_polls");
    }
}

public sealed class PollOptionConfiguration : IEntityTypeConfiguration<PollOption>
{
    public void Configure(EntityTypeBuilder<PollOption> builder)
    {
        // An option's text must be unique within that poll
        builder.HasIndex(e => new { e.PollId, e.Text }).IsUnique();
        // An option's sort order must be unique within that poll
        builder.HasIndex(e => new { e.PollId, e.SortOrder }).IsUnique();
        // Future indexes for querying...
    }
}

// --- Explicit like / vote junctions (replace EF implicit many-to-many) ---
public sealed class BlogPostLikeConfiguration : IEntityTypeConfiguration<BlogPostLike>
{
    public void Configure(EntityTypeBuilder<BlogPostLike> builder)
    {
        builder.HasKey(e => new { e.BlogPostId, e.UserId });
        builder.HasOne(bl => bl.BlogPost).WithMany(b => b.Likes)
            .HasForeignKey(bl => bl.BlogPostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(bl => bl.User).WithMany(u => u.BlogPostLikes)
            .HasForeignKey(bl => bl.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PollVoteConfiguration : IEntityTypeConfiguration<PollVote>
{
    public void Configure(EntityTypeBuilder<PollVote> builder)
    {
        builder.HasKey(e => new { e.PollOptionId, e.UserId });
        builder.HasOne(pv => pv.PollOption).WithMany(o => o.Votes)
            .HasForeignKey(pv => pv.PollOptionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(pv => pv.User).WithMany(u => u.PollVotes)
            .HasForeignKey(pv => pv.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

// NOTE: FeatureContribution has no FeatureContributionConfiguration class. Its delete-breaking
// relationships are configured from the principal side: BaseBlogPostConfiguration (above) and
// BaseCommentConfiguration (CommentConfigurations.cs) declare the SetNull FKs to it. It carries no
// configuration of its own (no composite key, no indexes beyond convention) — only a DbSet mapping.
