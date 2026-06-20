using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

public sealed class TagTypeConfiguration : IEntityTypeConfiguration<TagType>
{
    public void Configure(EntityTypeBuilder<TagType> builder)
    {
        builder.Property(e => e.TagTypeId).HasConversion<short>();

        builder.HasMany(tt => tt.Tags)
            .WithOne(t => t.TagType)
            .HasForeignKey(t => t.TagTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new { TagTypeId = TagTypeEnum.Character, TypeName = "Character" },
            new { TagTypeId = TagTypeEnum.Setting, TypeName = "Setting" },
            new { TagTypeId = TagTypeEnum.Genre, TypeName = "Genre" },
            new { TagTypeId = TagTypeEnum.ContentWarning, TypeName = "Content Warning" },
            new { TagTypeId = TagTypeEnum.CrossoverFandom, TypeName = "Crossover Fandom" },
            new { TagTypeId = TagTypeEnum.Relationship, TypeName = "Relationship" }
        );

        builder.HasIndex(e => e.TypeName).IsUnique();
        // Future indexes for querying...
    }
}

public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.Property(e => e.TagTypeId).HasConversion<short>();

        builder.HasMany(t => t.InverseParentTag)
            .WithOne(t => t.ParentTag)
            .HasForeignKey(t => t.ParentTagId)
            .OnDelete(DeleteBehavior.SetNull); // Keep child tags as top-level tags

        builder.HasMany(t => t.StoryTags)
            .WithOne(st => st.Tag)
            .HasForeignKey(st => st.TagId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.StoryCharacters)
            .WithOne(sc => sc.CharacterTag)
            .HasForeignKey(sc => sc.CharacterTagId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.SettingDetails)
            .WithOne(sd => sd.BaseTag)
            .HasForeignKey(sd => sd.BaseTagId)
            .OnDelete(DeleteBehavior.Restrict);

        // Tag names must be unique across the site
        builder.HasIndex(e => e.TagName).IsUnique();
        // Future indexes for querying (e.g., by TagTypeId, IsFanon)...
    }
}

public sealed class StoryTagConfiguration : IEntityTypeConfiguration<StoryTag>
{
    public void Configure(EntityTypeBuilder<StoryTag> builder)
    {
        builder.Property(e => e.Priority).HasConversion<short>();

        builder.HasKey(e => new { e.StoryId, e.TagId });
        // Future indexes for querying (e.g., by TagId)...
    }
}

public sealed class StoryCharacterConfiguration : IEntityTypeConfiguration<StoryCharacter>
{
    public void Configure(EntityTypeBuilder<StoryCharacter> builder)
    {
        builder.Property(e => e.Priority).HasConversion<short>();
        // Future indexes for querying (e.g., by StoryId, CharacterTagId)...
    }
}

public sealed class StoryCharacterRelationshipConfiguration : IEntityTypeConfiguration<StoryCharacterRelationship>
{
    public void Configure(EntityTypeBuilder<StoryCharacterRelationship> builder)
    {
        builder.Property(e => e.Priority).HasConversion<short>();
        builder.Property(e => e.RelationshipType).HasConversion<short>();
        // Future indexes for querying (e.g., by StoryId)...
    }
}

public sealed class SettingDetailConfiguration : IEntityTypeConfiguration<SettingDetail>
{
    public void Configure(EntityTypeBuilder<SettingDetail> builder)
    {
        // Future indexes for querying (e.g., by StoryId, BaseTagId)...
    }
}

public sealed class SavedTagSelectionConfiguration : IEntityTypeConfiguration<SavedTagSelection>
{
    public void Configure(EntityTypeBuilder<SavedTagSelection> builder)
    {
        builder.Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");

        // A user cannot have two selections with the same name
        builder.HasIndex(e => new { e.UserId, e.Nickname }).IsUnique();

        // When a User is deleted, delete their saved selections
        builder.HasOne(e => e.User)
            .WithMany() // No nav property on User
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // When a selection is deleted, delete all its tag entries
        builder.HasMany(e => e.Entries)
            .WithOne(e => e.SavedTagSelection)
            .HasForeignKey(e => e.SavedTagSelectionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Future indexes for querying (e.g., by is_public, user_id)...
    }
}

public sealed class SavedTagSelectionEntryConfiguration : IEntityTypeConfiguration<SavedTagSelectionEntry>
{
    public void Configure(EntityTypeBuilder<SavedTagSelectionEntry> builder)
    {
        // A selection cannot have the same tag twice
        builder.HasIndex(e => new { e.SavedTagSelectionId, e.TagId }).IsUnique();

        // Don't allow a Tag to be deleted if it's in a saved selection
        builder.HasOne(e => e.Tag)
            .WithMany() // No nav property on Tag
            .HasForeignKey(e => e.TagId)
            .OnDelete(DeleteBehavior.Restrict);

        // Future indexes for querying (e.g., by tag_id)...
    }
}
