using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

// --- GROUP & LISTS ---
public sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        // AudienceRating renamed from Rating (WU32) — column rename migration is data-preserving.
        builder.Property(e => e.AudienceRating).HasConversion<short>().HasColumnName("audience_rating");
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

        // RESTRICT (D10): the service deletes TPT base rows first via TptDelete; the DB refuses a partial delete.
        // No group-delete path exists yet (D47(b)); one must add a group-scope TptDelete method first.
        builder.HasMany(g => g.GroupComments)
            .WithOne(gc => gc.Group)
            .HasForeignKey(gc => gc.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(g => g.GroupFolders)
            .WithOne(f => f.Group)
            .HasForeignKey(f => f.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // RESTRICT (D10): the service deletes TPT base rows first via TptDelete; the DB refuses a partial delete.
        builder.HasMany(g => g.GroupBlogPosts)
            .WithOne(gbp => (gbp as GroupBlogPost)!.Group)
            .HasForeignKey(gbp => (gbp as GroupBlogPost)!.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

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
        // GroupRole stored as short — keeps the column type consistent with other enum-backed short columns.
        builder.Property(e => e.Role).HasConversion<short>();

        builder.HasKey(e => new { e.UserId, e.GroupId });
        // Index by GroupId to speed up member-list and admin-check lookups.
        builder.HasIndex(e => e.GroupId);
    }
}

public sealed class GroupStoryConfiguration : IEntityTypeConfiguration<GroupStory>
{
    public void Configure(EntityTypeBuilder<GroupStory> builder)
    {
        builder.Property(e => e.DateAdded).HasDefaultValueSql("CURRENT_TIMESTAMP");

        // A group's stories are a set (owner ruling D23: "membership is set semantics"). AddStoryAsync
        // keeps its AnyAsync check as the fast path; this index refuses the concurrent double add, which
        // the service then treats as already added (layer2-services.md §"Group Folder Integrity"). It
        // leads with group_id, so it also replaces the convention FK index ix_group_stories_group_id.
        builder.HasIndex(e => new { e.GroupId, e.StoryId })
            .IsUnique()
            .HasDatabaseName("ix_group_stories_group_id_story_id");
    }
}

public sealed class GroupFolderConfiguration : IEntityTypeConfiguration<GroupFolder>
{
    public void Configure(EntityTypeBuilder<GroupFolder> builder)
    {
        builder.Property(e => e.MaxRating).HasConversion<short>();

        // Self-FK (spec §Group Tables; schema audit §2.4): deleting a folder moves its sub-folders to the
        // top level. Before WU-SchemaHardening the column was a bare integer, so a deleted folder's
        // sub-folders pointed at nothing and dropped out of the tree. No navigations: the tree is built
        // from the flat ParentFolderId column (ServerGroupReadService.BuildFolderTreeAsync). EF adds the
        // convention index ix_group_folders_parent_folder_id, which the SET NULL lookup needs.
        builder.HasOne<GroupFolder>()
            .WithMany()
            .HasForeignKey(f => f.ParentFolderId)
            .OnDelete(DeleteBehavior.SetNull);

        // Sibling names are unique: same group, same parent, and the top level (NULL parent) is one
        // sibling set — hence NULLS NOT DISTINCT (precedent: StoryCharacterConfiguration). Before
        // WU-SchemaHardening the default NULLS DISTINCT let two top-level folders share a name. The
        // comparison is byte-wise; case-insensitive names are worksheet D29 (pending). The name is
        // load-bearing: ServerGroupWriteService catches a 23505 on it by name.
        builder.HasIndex(e => new { e.GroupId, e.ParentFolderId, e.Name })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("ix_group_folders_group_id_parent_folder_id_name");

        // A folder is never its own parent (schema audit §2.6). No operation re-parents a folder, so
        // longer cycles cannot form.
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_group_folders_no_self", "parent_folder_id IS NULL OR parent_folder_id <> group_folder_id"));
    }
}
