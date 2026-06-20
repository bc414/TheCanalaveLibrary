using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

// --- GROUP & LISTS ---
public sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.Property(e => e.Rating).HasConversion<short>();
        builder.Property(e => e.MaxContentRating).HasConversion<short>();
        builder.Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasMany(g => g.GroupMembers)
            .WithOne(m => m.Group)
            .HasForeignKey(m => m.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.GroupStories)
            .WithOne(gs => gs.Group)
            .HasForeignKey(gs => gs.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.GroupComments)
            .WithOne(gc => gc.Group)
            .HasForeignKey(gc => gc.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.GroupFolders)
            .WithOne(f => f.Group)
            .HasForeignKey(f => f.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.GroupBlogPosts)
            .WithOne(gbp => (gbp as GroupBlogPost)!.Group)
            .HasForeignKey(gbp => (gbp as GroupBlogPost)!.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // Group names must be unique across the site
        builder.HasIndex(e => e.GroupName).IsUnique();
        // Future indexes for querying (e.g., by CreatorId, Rating)...
    }
}

public sealed class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> builder)
    {
        builder.Property(e => e.DateJoined).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasKey(e => new { e.UserId, e.GroupId });
        // Future indexes for querying (e.g., by GroupId, Role)...
    }
}

public sealed class GroupStoryConfiguration : IEntityTypeConfiguration<GroupStory>
{
    public void Configure(EntityTypeBuilder<GroupStory> builder)
    {
        builder.Property(e => e.DateAdded).HasDefaultValueSql("CURRENT_TIMESTAMP");
        // Future indexes for querying (e.g., by GroupId, StoryId, DateAdded)...
    }
}

public sealed class GroupFolderConfiguration : IEntityTypeConfiguration<GroupFolder>
{
    public void Configure(EntityTypeBuilder<GroupFolder> builder)
    {
        builder.Property(e => e.MaxRating).HasConversion<short>();

        // A folder's name must be unique within its parent folder (or at the root)
        builder.HasIndex(e => new { e.GroupId, e.ParentFolderId, e.Name }).IsUnique();
        // Future indexes for querying...
    }
}
