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
            new { TagTypeId = TagTypeEnum.CrossoverFandom, TypeName = "Crossover Fandom" }
        );

        builder.HasIndex(e => e.TypeName).IsUnique();
    }
}

public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.Property(e => e.TagTypeId).HasConversion<short>();

        builder.HasMany(t => t.ChildTags)
            .WithOne(t => t.ParentTag)
            .HasForeignKey(t => t.ParentTagId)
            .OnDelete(DeleteBehavior.SetNull);

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

        // Tag names must be unique within a type — (TagName, TagTypeId) is the natural key.
        // "Paris" can be both a Character and a Setting; a single-column unique on TagName would prevent that.
        builder.HasIndex(e => new { e.TagName, e.TagTypeId }).IsUnique()
            .HasDatabaseName("ix_tags_tag_name_tag_type_id");
    }
}

public sealed class StoryTagConfiguration : IEntityTypeConfiguration<StoryTag>
{
    public void Configure(EntityTypeBuilder<StoryTag> builder)
    {
        builder.Property(e => e.Priority).HasConversion<short>();

        builder.HasKey(e => new { e.StoryId, e.TagId });
    }
}

public sealed class StoryCharacterConfiguration : IEntityTypeConfiguration<StoryCharacter>
{
    public void Configure(EntityTypeBuilder<StoryCharacter> builder)
    {
        builder.Property(e => e.Priority).HasConversion<short>();
    }
}

public sealed class StoryCharacterPairingConfiguration : IEntityTypeConfiguration<StoryCharacterPairing>
{
    public void Configure(EntityTypeBuilder<StoryCharacterPairing> builder)
    {
        builder.Property(e => e.Priority).HasConversion<short>();
        builder.Property(e => e.PairingType).HasConversion<short>();

        builder.HasMany(p => p.Members)
            .WithOne(m => m.Pairing)
            .HasForeignKey(m => m.StoryCharacterPairingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class StoryCharacterPairingMemberConfiguration : IEntityTypeConfiguration<StoryCharacterPairingMember>
{
    public void Configure(EntityTypeBuilder<StoryCharacterPairingMember> builder)
    {
        builder.HasKey(m => new { m.StoryCharacterPairingId, m.StoryCharacterId });

        builder.HasOne(m => m.StoryCharacter)
            .WithMany(sc => sc.PairingMemberships)
            .HasForeignKey(m => m.StoryCharacterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SettingDetailConfiguration : IEntityTypeConfiguration<SettingDetail>
{
    public void Configure(EntityTypeBuilder<SettingDetail> builder)
    {
        builder.HasIndex(e => new { e.StoryId, e.BaseTagId }).IsUnique()
            .HasDatabaseName("ix_setting_details_story_id_base_tag_id");
    }
}

public sealed class SavedTagSelectionConfiguration : IEntityTypeConfiguration<SavedTagSelection>
{
    public void Configure(EntityTypeBuilder<SavedTagSelection> builder)
    {
        builder.Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");

        // A user cannot have two selections with the same name
        builder.HasIndex(e => new { e.UserId, e.Nickname }).IsUnique();

        // WU43 — the table aggregates every user's rows and every read query is UserId-scoped, so
        // sort/lookup axes need their own indexes (see layer6-indexes.md "Saved Tag Selections").
        // Default sort (DateCreatedDesc) and its ascending counterpart.
        builder.HasIndex(e => new { e.UserId, e.DateCreated })
            .HasDatabaseName("ix_saved_tag_selections_user_id_date_created");

        // Profile Tag Selections tab: GetPublicSelectionsByUserAsync filters WHERE UserId = ? AND IsPublic.
        builder.HasIndex(e => new { e.UserId, e.IsPublic })
            .HasDatabaseName("ix_saved_tag_selections_user_id_is_public");

        // When a User is deleted, delete their saved selections
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // When a selection is deleted, delete all its tag entries
        builder.HasMany(e => e.Entries)
            .WithOne(e => e.SavedTagSelection)
            .HasForeignKey(e => e.SavedTagSelectionId)
            .OnDelete(DeleteBehavior.Cascade);
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
            .WithMany()
            .HasForeignKey(e => e.TagId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
