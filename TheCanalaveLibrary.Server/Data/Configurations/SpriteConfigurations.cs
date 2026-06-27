using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

public sealed class ThemeConfiguration : IEntityTypeConfiguration<Theme>
{
    public void Configure(EntityTypeBuilder<Theme> builder)
    {
        builder.HasMany<User>() // A Theme can have many Users
            .WithOne(u => u.Theme) // A User has one Theme
            .HasForeignKey(u => u.ThemeId)
            .OnDelete(DeleteBehavior.Restrict); // Don't delete a theme in use.

        builder.HasIndex(e => e.Name).IsUnique();
        builder.HasIndex(e => e.Slug).IsUnique();

        builder.HasData(
            new { ThemeId = 1, Name = "Pokémon", Slug = "pokemon", Description = "The default Pokémon theme!" }
        );
    }
}
