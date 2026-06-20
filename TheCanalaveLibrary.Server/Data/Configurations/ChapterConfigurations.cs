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

        // 2. Define the separate 1-to-1 for "primary version"
        // This tells EF Core that PrimaryContentId is a special
        // required link to one of the ChapterContents.
        builder.HasOne(c => c.PrimaryContent)
            .WithMany() // No inverse navigation property
            .HasForeignKey(c => c.PrimaryContentId)
            .OnDelete(DeleteBehavior.Restrict); // Don't let a "primary" version be deleted

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
