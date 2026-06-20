using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

public sealed class BadgeConfiguration : IEntityTypeConfiguration<Badge>
{
    public void Configure(EntityTypeBuilder<Badge> builder)
    {
        builder.HasMany(b => b.UserBadges)
            .WithOne(ub => ub.BadgeKeyNavigation)
            .HasForeignKey(ub => ub.BadgeKey)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new { BadgeKey = SiteBadges.BetaReader, DisplayName = "Beta Reader", Description = "Acknowledged as a Beta Reader on stories.", IconBaseUrl = "icons/badges/beta.png", SortOrder = 1 },
            new { BadgeKey = SiteBadges.Patron, DisplayName = "Patron", Description = "Supported the site through Community Spotlight.", IconBaseUrl = "icons/badges/patron.png", SortOrder = 2 },
            new { BadgeKey = SiteBadges.Recommender, DisplayName = "Recommender", Description = "Has many successful recs", IconBaseUrl = "icons/badges/recommender.png", SortOrder = 3 },
            new { BadgeKey = SiteBadges.Architect, DisplayName = "Architect", Description = "Helped develop a site feature", IconBaseUrl = "icons/badges/architect.png", SortOrder = 4 },
            new { BadgeKey = SiteBadges.Artist, DisplayName = "Artist", Description = "Made cover art for others", IconBaseUrl = "icons/badges/artist.png", SortOrder = 5 }
            // ... add other badges
        );

        builder.HasIndex(e => e.DisplayName).IsUnique();
        // Future indexes for querying...
    }
}

public sealed class UserBadgeConfiguration : IEntityTypeConfiguration<UserBadge>
{
    public void Configure(EntityTypeBuilder<UserBadge> builder)
    {
        builder.Property(e => e.DateEarned).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasKey(e => new { e.UserId, e.BadgeKey });
        // Future indexes for querying...
    }
}
