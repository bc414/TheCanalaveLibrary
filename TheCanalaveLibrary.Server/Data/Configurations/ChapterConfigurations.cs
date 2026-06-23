using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

// --- CHAPTER ---
public sealed class ChapterConfiguration : IEntityTypeConfiguration<Chapter>
{
    public void Configure(EntityTypeBuilder<Chapter> builder)
    {
        // A story cannot have two chapters with the same number
        builder.HasIndex(e => new { e.StoryId, e.ChapterNumber }).IsUnique();

        // 1. Define the 1-to-many for "all versions"
        builder.HasMany(c => c.ChapterContents)
            .WithOne(cc => cc.Chapter)
            .HasForeignKey(cc => cc.ChapterId)
            .OnDelete(DeleteBehavior.Cascade);

        // 2. Define the separate 1-to-1 for "primary version".
        // PrimaryContentId is nullable (long?) so the Chapter row can be inserted before its first
        // ChapterContent, breaking the circular FK dependency (see Chapter.PrimaryContentId doc).
        // IsRequired(false) generates nullable: true in the migration (WU17).
        // OnDelete Restrict: once a primary version is set, it cannot be deleted until another
        // version is promoted first (SetPrimaryVersionAsync).
        builder.HasOne(c => c.PrimaryContent)
            .WithMany() // No inverse navigation property
            .HasForeignKey(c => c.PrimaryContentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.ChapterComments)
            .WithOne(cc => cc.Chapter)
            .HasForeignKey(cc => cc.ChapterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.UserChapterInteractions)
            .WithOne(uci => uci.Chapter)
            .HasForeignKey(uci => uci.ChapterId)
            .OnDelete(DeleteBehavior.Cascade);

        // Future indexes for querying...
    }
}

public sealed class ChapterContentConfiguration : IEntityTypeConfiguration<ChapterContent>
{
    public void Configure(EntityTypeBuilder<ChapterContent> builder)
    {
        builder.Property(e => e.Rating).HasConversion<short>();

        //sort order can't be duplicated for a chapter
        builder.HasIndex(e => new { e.ChapterId, e.SortOrder }).IsUnique();
        // Future indexes for querying (e.g., by AuthorId)...
    }
}

public sealed class UserChapterInteractionConfiguration : IEntityTypeConfiguration<UserChapterInteraction>
{
    public void Configure(EntityTypeBuilder<UserChapterInteraction> builder)
    {
        builder.HasKey(e => new { e.UserId, e.ChapterId });
        // Future indexes for querying (e.g., by ChapterId, IsRead)...
    }
}
