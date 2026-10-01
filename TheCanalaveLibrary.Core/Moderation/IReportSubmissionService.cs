namespace TheCanalaveLibrary.Core;

/// <summary>
/// The member-facing half of the Moderation cluster (Feature 46): the reason lookup and report
/// submission — the only two operations a reporter performs. Split from
/// <see cref="IModerationReadService"/>/<see cref="IModerationWriteService"/> by owner ruling D9
/// (WU-ModerationIntegrity, 2026-09-30) so <c>ReportDialog</c>, a leaf on public pages, compiles against
/// this narrow surface and never against the hard-delete/ban one. On the server one concrete class
/// serves all three interfaces (registered once, forwarded — layer2-services.md §"Moderation
/// Services").
/// </summary>
public interface IReportSubmissionService
{
    /// <summary>
    /// Returns all report-reason lookup rows for the <c>ReportDialog</c> dropdown (and the moderator
    /// pages' reason pickers). Ungated: any reporter needs it.
    /// </summary>
    Task<ReportReasonDto[]> GetReportReasonsAsync();

    /// <summary>
    /// Files a report against a content item or user.
    /// <para>Order: the target-type allow-set (<see cref="ModerationValidationException"/>) → the
    /// throttle → the target must exist and be visible to the reporter unless only a takedown hides it
    /// (<see cref="KeyNotFoundException"/>) → one open report per reporter per target
    /// (<see cref="ModerationValidationException"/>, also enforced by a partial unique index) →
    /// <c>ReportedUserId</c> resolved and stored (a snapshot of the answerable account; NULL for
    /// anonymous or deleted-author content, which stays reportable) → the row is saved → the target's
    /// <c>ActiveReportCount</c> is incremented → a best-effort, null-sourced <c>ReportReceived</c>
    /// receipt carrying the report id (owner rulings D4/D8, service §2.4.4).</para>
    /// </summary>
    Task SubmitReportAsync(SubmitReportRequest request);
}
