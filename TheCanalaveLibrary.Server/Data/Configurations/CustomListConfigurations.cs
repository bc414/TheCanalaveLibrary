using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

public sealed class CustomListConfiguration : IEntityTypeConfiguration<CustomList>
{
    public void Configure(EntityTypeBuilder<CustomList> builder)
    {
        builder.Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasMany(l => l.CustomListEntries)
            .WithOne(e => e.List)
            .HasForeignKey(e => e.ListId)
            .OnDelete(DeleteBehavior.Cascade);

        // A user cannot have two custom lists with the same name
        builder.HasIndex(e => new { e.UserId, e.ListName }).IsUnique();
        // Future indexes for querying (e.g., by IsPublic)...
    }
}

public sealed class CustomListEntryConfiguration : IEntityTypeConfiguration<CustomListEntry>
{
    public void Configure(EntityTypeBuilder<CustomListEntry> builder)
    {
        builder.Property(e => e.DateAdded).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasKey(e => new { e.ListId, e.StoryId });
        // Future indexes for querying (e.g., by StoryId)...
    }
}
