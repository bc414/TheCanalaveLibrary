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
        builder.Property(e => e.DatePosted) // This configuration maps the column to this table
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        // Future indexes for querying (e.g., by ChapterId, DatePosted)...
    }
}

public sealed class BlogPostCommentConfiguration : IEntityTypeConfiguration<BlogPostComment>
{
    public void Configure(EntityTypeBuilder<BlogPostComment> builder)
    {
        builder.ToTable("blog_post_comments");
        builder.Property(e => e.DatePosted) // This configuration maps the column to this table
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        // Future indexes for querying (e.g., by BlogPostId)...
    }
}

public sealed class GroupCommentConfiguration : IEntityTypeConfiguration<GroupComment>
{
    public void Configure(EntityTypeBuilder<GroupComment> builder)
    {
        builder.ToTable("group_comments");
        builder.Property(e => e.DatePosted) // This configuration maps the column to this table
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        // Future indexes for querying (e.g., by GroupId)...
    }
}

public sealed class UserProfileCommentConfiguration : IEntityTypeConfiguration<UserProfileComment>
{
    public void Configure(EntityTypeBuilder<UserProfileComment> builder)
    {
        builder.ToTable("user_profile_comments");
        builder.Property(e => e.DatePosted) // This configuration maps the column to this table
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        // Future indexes for querying (e.g., by ProfileUserId)...
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
