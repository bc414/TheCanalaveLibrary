using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

public sealed class CommunitySpotlightConfiguration : IEntityTypeConfiguration<CommunitySpotlight>
{
    public void Configure(EntityTypeBuilder<CommunitySpotlight> builder)
    {
        builder.Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");
        // Future indexes for querying (e.g., by StoryId, EndDate)...
    }
}
