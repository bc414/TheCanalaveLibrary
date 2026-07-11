using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

// --- STORY ---
public sealed class StoryConfiguration : IEntityTypeConfiguration<Story>
{
    public void Configure(EntityTypeBuilder<Story> builder)
    {
        builder.Property(e => e.Rating).HasConversion<short>();
        builder.Property(e => e.StoryStatusId).HasConversion<short>();

        builder.HasMany(s => s.Chapters)
            .WithOne(c => c.Story)
            .HasForeignKey(c => c.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.StoryTags)
            .WithOne(st => st.Story)
            .HasForeignKey(st => st.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.StoryArcs)
            .WithOne(sa => sa.Story)
            .HasForeignKey(sa => sa.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.StoryCharacters)
            .WithOne(sc => sc.Story)
            .HasForeignKey(sc => sc.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.CoAuthors)
            .WithOne(c => c.Story)
            .HasForeignKey(c => c.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.BetaReaders)
            .WithOne(b => b.Story)
            .HasForeignKey(b => b.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.StoryAcknowledgments)
            .WithOne(a => a.Story)
            .HasForeignKey(a => a.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Recommendations)
            .WithOne(r => r.Story)
            .HasForeignKey(r => r.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.CommunitySpotlights)
            .WithOne(cs => cs.Story)
            .HasForeignKey(cs => cs.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.SeriesEntries)
            .WithOne(se => se.Story)
            .HasForeignKey(se => se.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.CustomListEntries)
            .WithOne(cle => cle.Story)
            .HasForeignKey(cle => cle.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.GroupStories)
            .WithOne(gs => gs.Story)
            .HasForeignKey(gs => gs.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.UserStoryInteractions)
            .WithOne(usi => usi.Story)
            .HasForeignKey(usi => usi.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // "Also posted on" links (Feature 53 reframe, WU38d) — many per story; deleting the story
        // deletes its links.
        builder.HasMany(s => s.ExternalLinks)
            .WithOne(sel => sel.Story)
            .HasForeignKey(sel => sel.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.SettingDetails)
            .WithOne(sd => sd.Story)
            .HasForeignKey(sd => sd.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.StoryRelationshipSourceStories)
            .WithOne(sr => sr.SourceStory)
            .HasForeignKey(sr => sr.SourceStoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.StoryRelationshipTargetStories)
            .WithOne(sr => sr.TargetStory)
            .HasForeignKey(sr => sr.TargetStoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.ProfileBlogPosts) // From ProfileBlogPost inheritance
            .WithOne(pbp => (pbp as ProfileBlogPost)!.Story)
            .HasForeignKey(pbp => (pbp as ProfileBlogPost)!.StoryId)
            .OnDelete(DeleteBehavior.SetNull); // A blog post can exist without a story

        // Story -> Lookup Tables (Restrict)
        builder.HasOne(s => s.StoryStatus)
            .WithMany(ss => ss.Stories)
            .HasForeignKey(s => s.StoryStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        // L6 (2026-07-07): the two discovery sort spines. DatePublished is /discover's default
        // sorted order; LastUpdated drives GetRecentListingsAsync + the Relevance tie-break.
        // Both are top-N pages under the global rating/is_taken_down filters — an ordered walk
        // with residual filtering beats sort-the-world as stories grow. Single-column (no
        // rating/is_taken_down prefix): the residuals are cheap and a prefixed index would
        // fragment across the rating ceiling variants.
        builder.HasIndex(e => e.PublishedDate)
            .HasDatabaseName("ix_stories_published_date");
        builder.HasIndex(e => e.LastUpdatedDate)
            .HasDatabaseName("ix_stories_last_updated_date");
    }
}

/// <summary>
/// "Also posted on" link rows (Feature 53 reframe, WU38d). Uniqueness is per-story-per-URL only —
/// global URL uniqueness (the old StoryImport rule) was deliberately dropped: whether two stories
/// claiming the same source URL is theft is a moderation judgment (WU39/Feature 46), not a schema
/// constraint that could let a thief squat a URL and block the real author.
/// </summary>
public sealed class StoryExternalLinkConfiguration : IEntityTypeConfiguration<StoryExternalLink>
{
    public void Configure(EntityTypeBuilder<StoryExternalLink> builder)
    {
        builder.Property(e => e.VerificationStatus).HasConversion<short>();
        builder.Property(e => e.DateAdded).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(e => new { e.StoryId, e.Url }).IsUnique();

        builder.HasOne(e => e.ExternalPlatform)
            .WithMany(p => p.StoryExternalLinks)
            .HasForeignKey(e => e.ExternalPlatformId)
            .OnDelete(DeleteBehavior.Restrict); // seeded lookup rows are never deleted from under links
    }
}

/// <summary>
/// Seeded platform lookup — deliberately NOT a hybrid enum (audit/Moderation.md F53: adding an
/// archive is a seed row, not a code change). "Other" (id 7) has no DomainPattern; the UI shows
/// the URL's host for it.
/// </summary>
public sealed class ExternalPlatformConfiguration : IEntityTypeConfiguration<ExternalPlatform>
{
    public void Configure(EntityTypeBuilder<ExternalPlatform> builder)
    {
        builder.HasIndex(e => e.Name).IsUnique();

        builder.HasData(
            new ExternalPlatform { ExternalPlatformId = 1, Name = "Archive of Our Own", DomainPattern = "archiveofourown.org" },
            new ExternalPlatform { ExternalPlatformId = 2, Name = "FanFiction.Net", DomainPattern = "fanfiction.net" },
            new ExternalPlatform { ExternalPlatformId = 3, Name = "Wattpad", DomainPattern = "wattpad.com" },
            new ExternalPlatform { ExternalPlatformId = 4, Name = "SpaceBattles", DomainPattern = "spacebattles.com" },
            new ExternalPlatform { ExternalPlatformId = 5, Name = "Sufficient Velocity", DomainPattern = "sufficientvelocity.com" },
            new ExternalPlatform { ExternalPlatformId = 6, Name = "Royal Road", DomainPattern = "royalroad.com" },
            new ExternalPlatform { ExternalPlatformId = 7, Name = "Other", DomainPattern = null }
        );
    }
}

public sealed class StoryListingConfiguration : IEntityTypeConfiguration<StoryListing>
{
    public void Configure(EntityTypeBuilder<StoryListing> builder)
    {
        // Configure the 1-to-1 relationship with Story
        builder.HasOne(p => p.Story)
            .WithOne(s => s.StoryListing)
            .HasForeignKey<StoryListing>(p => p.StoryId)
            .OnDelete(DeleteBehavior.Cascade); // Deleting a Story deletes its listing data

        // 1. Configure the SearchVector as a "generated" column.
        // This tells PostgreSQL to automatically build the vector from
        // the title and description, handling tokenization for you.
        builder.Property<NpgsqlTypes.NpgsqlTsVector>("SearchVector")
            .HasComputedColumnSql(
                // Combines title and description, using 'english' rules for tokenizing
                // and `coalesce` to handle nulls safely.
                "to_tsvector('english', coalesce(\"story_title\", '') || ' ' || coalesce(\"short_description\", ''))",
                stored: true); // 'stored: true' is required so we can index it.

        // 2. Create a GIN index on the new vector column.
        // This is the "magic" that makes FTS incredibly fast.
        builder.HasIndex("SearchVector")
            .HasMethod("gin")
            .HasDatabaseName("ix_story_listing_search_vector");
        // Future indexes for querying...
    }
}

public sealed class StoryDetailConfiguration : IEntityTypeConfiguration<StoryDetail>
{
    public void Configure(EntityTypeBuilder<StoryDetail> builder)
    {
        builder.Property(e => e.PostApprovalStatus).HasConversion<short>();

        // Configure the 1-to-1 relationship with Story
        builder.HasOne(d => d.Story)
            .WithOne(s => s.StoryDetail)
            .HasForeignKey<StoryDetail>(d => d.StoryId)
            .OnDelete(DeleteBehavior.Cascade); // Deleting a Story deletes its details

        // A story slug must be unique, but can also be null.
        builder.HasIndex(e => e.Slug).IsUnique()
            .HasFilter("\"slug\" IS NOT NULL");

        // Future indexes for querying (e.g., Full-Text on long_description)...
    }
}

public sealed class StoryStatusConfiguration : IEntityTypeConfiguration<StoryStatus>
{
    public void Configure(EntityTypeBuilder<StoryStatus> builder)
    {
        builder.Property(e => e.StoryStatusId).HasConversion<short>();

        builder.HasData(
            new { StoryStatusId = StoryStatusEnum.Draft, StatusName = "Draft", Description = "Story is a work in progress and not visible to the public." },
            new { StoryStatusId = StoryStatusEnum.PendingApproval, StatusName = "Pending Approval", Description = "Story has been submitted and is awaiting moderator approval." },
            new { StoryStatusId = StoryStatusEnum.InProgress, StatusName = "In Progress", Description = "Story is approved, public, and actively being updated." },
            new { StoryStatusId = StoryStatusEnum.Completed, StatusName = "Completed", Description = "The story is finished." },
            new { StoryStatusId = StoryStatusEnum.OnHiatus, StatusName = "On Hiatus", Description = "The author is taking a break from updating." },
            new { StoryStatusId = StoryStatusEnum.Cancelled, StatusName = "Cancelled", Description = "The story will not be continued." },
            new { StoryStatusId = StoryStatusEnum.Rewriting, StatusName = "Rewriting", Description = "The story is undergoing major revisions." },
            new { StoryStatusId = StoryStatusEnum.OpenBeta, StatusName = "Open Beta", Description = "Story is visible to beta readers for feedback." },
            new { StoryStatusId = StoryStatusEnum.Rejected, StatusName = "Rejected", Description = "Story was submitted but did not pass moderation." }
        );

        // Future indexes for querying...
    }
}

public sealed class SeriesConfiguration : IEntityTypeConfiguration<Series>
{
    public void Configure(EntityTypeBuilder<Series> builder)
    {
        builder.Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");

        // An author cannot have two series with the same name
        builder.HasIndex(e => new { e.AuthorId, e.Name }).IsUnique();
        // Future indexes for querying...
    }
}

public sealed class SeriesEntryConfiguration : IEntityTypeConfiguration<SeriesEntry>
{
    public void Configure(EntityTypeBuilder<SeriesEntry> builder)
    {
        builder.HasKey(e => new { e.SeriesId, e.StoryId });
        // Future indexes for querying (e.g., by StoryId)...
    }
}

public sealed class StoryArcConfiguration : IEntityTypeConfiguration<StoryArc>
{
    public void Configure(EntityTypeBuilder<StoryArc> builder)
    {
        // A story cannot have two arcs with the same title
        builder.HasIndex(e => new { e.StoryId, e.Title }).IsUnique();
        // A story cannot have two arcs with the same sort order
        builder.HasIndex(e => new { e.StoryId, e.SortOrder }).IsUnique();
        // Future indexes for querying...
    }
}

public sealed class StoryRelationshipConfiguration : IEntityTypeConfiguration<StoryRelationship>
{
    public void Configure(EntityTypeBuilder<StoryRelationship> builder)
    {
        builder.Property(e => e.StatusId).HasConversion<short>();
        builder.Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasKey(e => new { e.SourceStoryId, e.TargetStoryId, e.RelationshipTypeId });
        // Future indexes for querying (e.g., by TargetStoryId)...
    }
}

public sealed class StoryRelationshipTypeConfiguration : IEntityTypeConfiguration<StoryRelationshipType>
{
    public void Configure(EntityTypeBuilder<StoryRelationshipType> builder)
    {
        builder.HasMany(srt => srt.StoryRelationships)
            .WithOne(sr => sr.RelationshipType)
            .HasForeignKey(sr => sr.RelationshipTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new { RelationshipTypeId = (short)1, TypeName = "Inspired By" },
            new { RelationshipTypeId = (short)2, TypeName = "Prequel" },
            new { RelationshipTypeId = (short)3, TypeName = "Sequel" },
            new { RelationshipTypeId = (short)4, TypeName = "Companion Piece" }
        );

        // Future indexes for querying...
    }
}

// --- Collaboration (Story-owned junctions) ---
public sealed class StoryAcknowledgmentConfiguration : IEntityTypeConfiguration<StoryAcknowledgment>
{
    public void Configure(EntityTypeBuilder<StoryAcknowledgment> builder)
    {
        builder.Property(e => e.DateAcknowledged).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasKey(e => new { e.StoryId, e.AcknowledgedUserId, e.AcknowledgmentRoleId });
        // Future indexes for querying (e.g., by AcknowledgedUserId)...
    }
}

public sealed class AcknowledgmentRoleConfiguration : IEntityTypeConfiguration<AcknowledgmentRole>
{
    public void Configure(EntityTypeBuilder<AcknowledgmentRole> builder)
    {
        builder.HasMany(ar => ar.StoryAcknowledgments)
            .WithOne(sa => sa.AcknowledgmentRole)
            .HasForeignKey(sa => sa.AcknowledgmentRoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new { AcknowledgmentRoleId = (short)1, RoleName = "Beta Reader" },
            new { AcknowledgmentRoleId = (short)2, RoleName = "Planner" },
            new { AcknowledgmentRoleId = (short)3, RoleName = "Cover Artist" },
            new { AcknowledgmentRoleId = (short)4, RoleName = "Editor" },
            new { AcknowledgmentRoleId = (short)5, RoleName = "Inspiration" }
        );

        // Future indexes for querying...
    }
}

public sealed class CoAuthorConfiguration : IEntityTypeConfiguration<CoAuthor>
{
    public void Configure(EntityTypeBuilder<CoAuthor> builder)
    {
        builder.Property(e => e.DateAdded).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasKey(e => new { e.StoryId, e.CoAuthorUserId });
        // Future indexes for querying (e.g., by CoAuthorUserId)...
    }
}

public sealed class BetaReaderConfiguration : IEntityTypeConfiguration<BetaReader>
{
    public void Configure(EntityTypeBuilder<BetaReader> builder)
    {
        builder.Property(e => e.DateAdded).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasKey(e => new { e.StoryId, e.BetaReaderUserId });
        // Future indexes for querying (e.g., by BetaReaderUserId)...
    }
}
