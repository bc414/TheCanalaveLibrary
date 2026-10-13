using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

public sealed class BaseBlogPostConfiguration : IEntityTypeConfiguration<BaseBlogPost>
{
    public void Configure(EntityTypeBuilder<BaseBlogPost> builder)
    {
        builder.ToTable("base_blog_posts");

        // RESTRICT (D10): the service deletes TPT base rows first via TptDelete; the DB refuses a partial delete.
        builder.HasMany(b => b.BlogPostComments)
            .WithOne(c => c.BlogPost)
            .HasForeignKey(c => c.BlogPostId)
            .OnDelete(DeleteBehavior.Restrict);
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

public sealed class SiteBlogPostConfiguration : IEntityTypeConfiguration<SiteBlogPost>
{
    public void Configure(EntityTypeBuilder<SiteBlogPost> builder)
    {
        builder.Property(e => e.Rating).HasConversion<short>();
        builder.Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.ToTable("site_blog_posts");
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

        // TPT Inheritance setup. A poll never closes before it opens (schema audit §2.6). Declared on
        // the base table's ToTable: EF puts it on base_polls only (probed WU-SchemaHardening), not on
        // the child tables, which lack the columns. ServerPollWriteService checks the effective open
        // date first, so a bad pair is a PollValidationException, not this constraint's 500.
        builder.ToTable("base_polls", t => t.HasCheckConstraint(
            "ck_base_polls_dates", "date_closed IS NULL OR date_closed >= date_opened"));
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
        // RESTRICT (D10): the service deletes TPT base rows first via TptDelete; the DB refuses a partial delete.
        // (A cascade here removed only the blog_post_polls child row and orphaned base_polls; deleting
        // the base_polls row instead takes the child row, options and votes with it.)
        builder.HasOne(p => p.BlogPost)
            .WithMany(b => b.Polls)
            .HasForeignKey(p => p.BlogPostId)
            .OnDelete(DeleteBehavior.Restrict);

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
        // Surrogate key (owner ruling D13, WU-UserDeletion 2026-10-13): user_id is nullable so a deleted
        // account's votes survive anonymized, and a nullable column cannot be part of a PK.
        builder.HasKey(e => e.PollVoteId);

        // One live vote per (option, user). NULLS DISTINCT is deliberate — the opposite of group_folders'
        // name index: any number of anonymized (NULL-user) rows must coexist on one option. Never
        // AreNullsDistinct(false) here (layer1-data-model.md §"Column Conventions").
        builder.HasIndex(e => new { e.PollOptionId, e.UserId })
            .IsUnique()
            .HasDatabaseName("ix_poll_votes_poll_option_id_user_id");

        builder.HasOne(pv => pv.PollOption).WithMany(o => o.Votes)
            .HasForeignKey(pv => pv.PollOptionId).OnDelete(DeleteBehavior.Cascade);
        // SET NULL, not CASCADE (D13): a poll is a point-in-time record, and nothing denormalizes its
        // tally, so cascading a voter's rows rewrote a closed poll's published result. The FK's own
        // index (ix_poll_votes_user_id) backs this SET NULL scan and "my votes".
        builder.HasOne(pv => pv.User).WithMany(u => u.PollVotes)
            .HasForeignKey(pv => pv.UserId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
    }
}
