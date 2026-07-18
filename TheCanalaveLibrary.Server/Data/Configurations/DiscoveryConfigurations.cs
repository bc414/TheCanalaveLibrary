using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

public sealed class SearchModeConfiguration : IEntityTypeConfiguration<SearchMode>
{
    public void Configure(EntityTypeBuilder<SearchMode> builder)
    {
        builder.HasMany(sm => sm.UserStoryInteractionFilterSettings)
            .WithOne(us => us.SearchModeKeyNavigation)
            .HasForeignKey(us => us.SearchModeKey)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(sm => sm.UserCustomFilters)
            .WithOne(ucf => ucf.SearchModeKeyNavigation)
            .HasForeignKey(ucf => ucf.SearchModeKey)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(sm => sm.DefaultUserStoryInteractionFilterSettings)
            .WithOne(dss => dss.SearchModeKeyNavigation)
            .HasForeignKey(dss => dss.SearchModeKey)
            .OnDelete(DeleteBehavior.Restrict);

        // Search modes are discovery surfaces (§5.3) — NOT sources/sorts. "Random" is Source=All+Sort=Random
        // on the SearchPage surface, so it is not a mode.
        builder.HasData(
            new { SearchModeKey = SiteSearchModes.SearchPage, Name = "Search Page", Description = "The main discovery surface (Source=All) with tags, text search, and result ordering." },
            new { SearchModeKey = SiteSearchModes.TreeSearch, Name = "Tree Search", Description = "Discover stories through connections: favorites, recommendations, and author follows." },
            new { SearchModeKey = SiteSearchModes.AutoTreeSearch, Name = "Automatic Tree Search", Description = "Automatically surfaced connections from the tree-search data mart." },
            new { SearchModeKey = SiteSearchModes.AlsoFavorited, Name = "Also Favorited", Description = "Stories favorited by users who also favorited your selection." },
            new { SearchModeKey = SiteSearchModes.AlsoRecommended, Name = "Also Recommended", Description = "Stories recommended by users who also recommended your selection." },
            new { SearchModeKey = SiteSearchModes.ProfilePublishedStories, Name = "Profile: Published Stories", Description = "A profile's authored-stories tab." },
            new { SearchModeKey = SiteSearchModes.ProfileFavorites, Name = "Profile: Favorites", Description = "A profile's public-favorites tab." },
            new { SearchModeKey = SiteSearchModes.ProfileRecommendations, Name = "Profile: Recommendations", Description = "A profile's recommendations tab." }
        );

        builder.HasIndex(e => e.Name).IsUnique();
        // Future indexes for querying...
    }
}

public sealed class UserStoryInteractionFilterTypeConfiguration : IEntityTypeConfiguration<UserStoryInteractionFilterType>
{
    public void Configure(EntityTypeBuilder<UserStoryInteractionFilterType> builder)
    {
        builder.HasMany(uif => uif.UserStoryInteractionFilterSettings)
            .WithOne(us => us.UserStoryInteractionFilterType)
            .HasForeignKey(us => us.UserStoryInteractionFilterKey)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(uif => uif.DefaultUserStoryInteractionFilterSettings)
            .WithOne(dss => dss.UserStoryInteractionFilterType)
            .HasForeignKey(dss => dss.UserStoryInteractionFilterKey)
            .OnDelete(DeleteBehavior.Restrict);

        // One filter per UserStoryInteraction boolean column (1:1, no compounds).
        builder.HasData(
            new { UserStoryInteractionFilterKey = UserStoryInteractionFilters.Ignored, Name = "Ignored", Description = "Exclude stories you have marked as 'Ignored'." },
            new { UserStoryInteractionFilterKey = UserStoryInteractionFilters.Completed, Name = "Completed", Description = "Exclude stories you have already finished." },
            new { UserStoryInteractionFilterKey = UserStoryInteractionFilters.HasStarted, Name = "Started", Description = "Exclude stories you have already started reading." },
            new { UserStoryInteractionFilterKey = UserStoryInteractionFilters.ReadItLater, Name = "Read It Later", Description = "Exclude stories on your 'Read It Later' list." },
            new { UserStoryInteractionFilterKey = UserStoryInteractionFilters.Favorited, Name = "Favorited", Description = "Exclude stories on your 'Favorite' list." },
            new { UserStoryInteractionFilterKey = UserStoryInteractionFilters.HiddenFavorited, Name = "Hidden Favorite", Description = "Exclude stories on your 'Hidden Favorite' list." },
            new { UserStoryInteractionFilterKey = UserStoryInteractionFilters.Followed, Name = "Followed", Description = "Exclude stories you are 'Following'." }
        );

        builder.HasIndex(e => e.Name).IsUnique();
        // Future indexes for querying...
    }
}

public sealed class DefaultUserStoryInteractionFilterSettingConfiguration : IEntityTypeConfiguration<DefaultUserStoryInteractionFilterSetting>
{
    public void Configure(EntityTypeBuilder<DefaultUserStoryInteractionFilterSetting> builder)
    {
        // DefaultUserStoryInteractionFilterSetting requires SearchMode and UserStoryInteractionFilterType seeded first.
        // Minimal sane defaults: exclude already-Ignored stories on every discovery surface. Profile
        // surfaces intentionally have no default exclusions (they show the user's full lists).
        builder.HasData(
            new { SearchModeKey = SiteSearchModes.SearchPage, UserStoryInteractionFilterKey = UserStoryInteractionFilters.Ignored, IsEnabled = true },
            new { SearchModeKey = SiteSearchModes.TreeSearch, UserStoryInteractionFilterKey = UserStoryInteractionFilters.Ignored, IsEnabled = true },
            new { SearchModeKey = SiteSearchModes.AutoTreeSearch, UserStoryInteractionFilterKey = UserStoryInteractionFilters.Ignored, IsEnabled = true },
            new { SearchModeKey = SiteSearchModes.AlsoFavorited, UserStoryInteractionFilterKey = UserStoryInteractionFilters.Ignored, IsEnabled = true },
            new { SearchModeKey = SiteSearchModes.AlsoRecommended, UserStoryInteractionFilterKey = UserStoryInteractionFilters.Ignored, IsEnabled = true }
        );

        builder.HasKey(e => new { e.SearchModeKey, e.UserStoryInteractionFilterKey });
        // Future indexes for querying...
    }
}

public sealed class UserStoryInteractionFilterSettingConfiguration : IEntityTypeConfiguration<UserStoryInteractionFilterSetting>
{
    public void Configure(EntityTypeBuilder<UserStoryInteractionFilterSetting> builder)
    {
        // A user can only have one setting for a specific filter/mode
        builder.HasIndex(e => new { e.UserId, e.SearchModeKey, e.UserStoryInteractionFilterKey }).IsUnique();
        // Future indexes for querying...
    }
}

public sealed class UserCustomFilterConfiguration : IEntityTypeConfiguration<UserCustomFilter>
{
    public void Configure(EntityTypeBuilder<UserCustomFilter> builder)
    {
        builder.Property(e => e.FilterEntityType).HasConversion<short>();
        // Future indexes for querying (e.g., by UserId)...
    }
}
