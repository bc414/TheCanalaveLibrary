using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

// --- BASE HIERARCHIES & SELF-REFERENCES ---
public sealed class BaseCommentConfiguration : IEntityTypeConfiguration<BaseComment>
{
    public void Configure(EntityTypeBuilder<BaseComment> builder)
    {
        // TPT Inheritance setup
        builder.ToTable("base_comments");

        builder.HasMany(c => c.InverseParentComment)
            .WithOne(c => c.ParentComment)
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.SetNull); // Keep replies as top-level comments

        // --- Diamond-Breaking SetNulls (Already covered by User SetNull) ---
        // Example: FeatureContribution.CommentId -> BaseComment
        builder.HasMany(c => c.FeatureContributions)
            .WithOne(fc => fc.Comment)
            .HasForeignKey(fc => fc.CommentId)
            .OnDelete(DeleteBehavior.SetNull); // Breaks diamond

        // Future indexes for querying (e.g., by AuthorId, DatePosted)...
    }
}

public sealed class ChapterCommentConfiguration : IEntityTypeConfiguration<ChapterComment>
{
    public void Configure(EntityTypeBuilder<ChapterComment> builder)
    {
        builder.ToTable("chapter_comments");
        builder.Property(e => e.DatePosted).HasDefaultValueSql("CURRENT_TIMESTAMP");
        // Golden index (L6, measured 2026-07-07): the roots page
        // (WHERE chapter_id = @c … ORDER BY date_posted DESC LIMIT n) walks this in order and
        // streams into the LIMIT — kills both the sort and the ~20 ms parallel-worker launch the
        // planner otherwise reaches for on hub chapters (p50 24 ms → sub-ms at 324k comments).
        // Supersedes the convention FK index on chapter_id (prefix-covered).
        builder.HasIndex(e => new { e.ChapterId, e.DatePosted })
            .HasDatabaseName("ix_chapter_comments_chapter_id_date_posted");
    }
}

public sealed class BlogPostCommentConfiguration : IEntityTypeConfiguration<BlogPostComment>
{
    public void Configure(EntityTypeBuilder<BlogPostComment> builder)
    {
        builder.ToTable("blog_post_comments");
        builder.Property(e => e.DatePosted).HasDefaultValueSql("CURRENT_TIMESTAMP");
        // Same golden shape as chapter_comments — the four comment contexts share a byte-identical
        // roots-page query (ServerCommentReadService).
        builder.HasIndex(e => new { e.BlogPostId, e.DatePosted })
            .HasDatabaseName("ix_blog_post_comments_blog_post_id_date_posted");
    }
}

public sealed class GroupCommentConfiguration : IEntityTypeConfiguration<GroupComment>
{
    public void Configure(EntityTypeBuilder<GroupComment> builder)
    {
        builder.ToTable("group_comments");
        builder.Property(e => e.DatePosted).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(e => new { e.GroupId, e.DatePosted })
            .HasDatabaseName("ix_group_comments_group_id_date_posted");
    }
}

public sealed class UserProfileCommentConfiguration : IEntityTypeConfiguration<UserProfileComment>
{
    public void Configure(EntityTypeBuilder<UserProfileComment> builder)
    {
        builder.ToTable("user_profile_comments");
        builder.Property(e => e.DatePosted).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(e => new { e.ProfileUserId, e.DatePosted })
            .HasDatabaseName("ix_user_profile_comments_profile_user_id_date_posted");
    }
}

// --- Explicit like junction (replaces EF implicit many-to-many) ---
public sealed class CommentLikeConfiguration : IEntityTypeConfiguration<CommentLike>
{
    public void Configure(EntityTypeBuilder<CommentLike> builder)
    {
        builder.HasKey(e => new { e.CommentId, e.UserId });
        builder.HasOne(cl => cl.Comment).WithMany(c => c.Likes)
            .HasForeignKey(cl => cl.CommentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(cl => cl.User).WithMany(u => u.CommentLikes)
            .HasForeignKey(cl => cl.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
