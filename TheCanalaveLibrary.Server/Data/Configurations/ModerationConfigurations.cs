using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.Property(e => e.ReportedEntityType).HasConversion<short>();
        builder.Property(e => e.ReportStatusId).HasConversion<short>();
        builder.Property(e => e.DateReported).HasDefaultValueSql("CURRENT_TIMESTAMP");

        // ReporterUserId / ModeratorUserId both set null on user deletion rather than
        // cascading (the report record must survive for audit purposes).
        builder.HasOne(r => r.ReporterUser)
            .WithMany(u => u.ReportReporterUsers)
            .HasForeignKey(r => r.ReporterUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.ModeratorUser)
            .WithMany(u => u.ReportModeratorUsers)
            .HasForeignKey(r => r.ModeratorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Moderator queue primary sort: open reports ordered by ActiveReportCount desc.
        builder.HasIndex(e => e.ReportStatusId)
            .HasDatabaseName("ix_reports_report_status_id");

        // Polymorphic target lookup: find all reports against a given entity.
        builder.HasIndex(e => new { e.ReportedEntityType, e.ReportedEntityId })
            .HasDatabaseName("ix_reports_reported_entity_type_reported_entity_id");
    }
}

public sealed class ReportReasonConfiguration : IEntityTypeConfiguration<ReportReason>
{
    public void Configure(EntityTypeBuilder<ReportReason> builder)
    {
        builder.HasMany(rr => rr.Reports)
            .WithOne(r => r.ReportReason)
            .HasForeignKey(r => r.ReportReasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new { ReportReasonId = (short)1, ReasonName = "Other", Description = "A reason not covered by other categories." },
            new { ReportReasonId = (short)2, ReasonName = "Spam", Description = "Unsolicited advertising or repeated, low-effort content." },
            new { ReportReasonId = (short)3, ReasonName = "Hate Speech", Description = "Content that attacks a person or group based on race, ethnicity, religion, etc." },
            new { ReportReasonId = (short)4, ReasonName = "Harassment", Description = "Targeted abuse, bullying, or intimidation of a user." },
            new { ReportReasonId = (short)5, ReasonName = "Illegal Content", Description = "Content violating laws, such as child pornography or piracy." },
            new { ReportReasonId = (short)6, ReasonName = "Plagiarism", Description = "Posting content that is not your own without attribution." }
        );

        builder.HasIndex(e => e.ReasonName).IsUnique();
        // Future indexes for querying...
    }
}

public sealed class ReportStatusConfiguration : IEntityTypeConfiguration<ReportStatus>
{
    public void Configure(EntityTypeBuilder<ReportStatus> builder)
    {
        builder.Property(e => e.ReportStatusId).HasConversion<short>();

        builder.HasMany(rs => rs.Reports)
            .WithOne(r => r.ReportStatus)
            .HasForeignKey(r => r.ReportStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new { ReportStatusId = ReportStatusEnum.Open, StatusName = "Open" },
            new { ReportStatusId = ReportStatusEnum.UnderReview, StatusName = "Under Review" },
            new { ReportStatusId = ReportStatusEnum.ResolvedNoAction, StatusName = "Resolved - No Action" },
            new { ReportStatusId = ReportStatusEnum.ResolvedActionTaken, StatusName = "Resolved - Action Taken" }
        );

        builder.HasIndex(e => e.StatusName).IsUnique();
        // Future indexes for querying...
    }
}

/// <summary>
/// Feature 62 — the one Layer-8 exception with an EF model (layer8-data-marts.md
/// §"site_daily_stats"). <c>stat_date</c> is the PK and is never database-generated: the
/// aggregator's raw upsert always supplies it explicitly (the previous completed UTC day).
/// </summary>
public sealed class SiteDailyStatConfiguration : IEntityTypeConfiguration<SiteDailyStat>
{
    public void Configure(EntityTypeBuilder<SiteDailyStat> builder)
    {
        builder.HasKey(s => s.StatDate);
        builder.Property(s => s.StatDate).ValueGeneratedNever();
    }
}

// StoryImportConfiguration removed 2026-07-11 (WU38d): StoryImport was remodeled into
// StoryExternalLink + ExternalPlatform (Feature 53 reframe — many "Also posted on" links per
// story). The entities live in Core/Stories/, so their configurations live in
// StoryConfigurations.cs per the one-file-per-folder-cluster rule.
