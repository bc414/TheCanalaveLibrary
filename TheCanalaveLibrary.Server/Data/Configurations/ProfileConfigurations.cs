using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

// This block is for the "cold" vertical partition
public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        // Configure the 1-to-1 relationship with User
        builder.HasOne(p => p.User)
            .WithOne(u => u.UserProfile)
            .HasForeignKey<UserProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade); // Deleting a User deletes their profile

        // Future indexes for full-text search on 'profile_text'...
    }
}

public sealed class UserStatConfiguration : IEntityTypeConfiguration<UserStat>
{
    public void Configure(EntityTypeBuilder<UserStat> builder)
    {
        builder.HasKey(e => e.UserId);
        // Future indexes for querying...
    }
}
