using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// In-memory stand-in for <see cref="IReportSubmissionService"/> — what <c>ReportDialog</c> injects (owner
/// ruling D9's split). Register it for any component tree containing a <c>ReportDialog</c>: a missing
/// registration is a render-time DI failure, not a compile error. Reasons are empty unless a test sets
/// <see cref="Reasons"/>. <see cref="SubmitReportAsync"/> records each request in <see cref="Submitted"/>,
/// so a host test can assert the entity type and id its wiring sends (WU-TrackerFixes review fixes), and
/// succeeds unless <see cref="SubmitException"/> is set.
/// </summary>
public class FakeReportSubmissionService : IReportSubmissionService
{
    public ReportReasonDto[] Reasons { get; set; } = [];

    /// <summary>When set, <see cref="SubmitReportAsync"/> throws it (ReportDialogTests' refusal paths).</summary>
    public Exception? SubmitException { get; set; }

    /// <summary>Every request <see cref="SubmitReportAsync"/> received, refused ones included.</summary>
    public List<SubmitReportRequest> Submitted { get; } = [];

    /// <summary>When set, <see cref="GetReportReasonsAsync"/> throws it: a WASM tab whose cookie expired
    /// before the dialog's first open (WU-TrackerFixes browser pass, 2026-10-13).</summary>
    public Exception? ReasonsException { get; set; }

    public Task<ReportReasonDto[]> GetReportReasonsAsync() =>
        ReasonsException is null ? Task.FromResult(Reasons) : throw ReasonsException;

    public Task SubmitReportAsync(SubmitReportRequest request)
    {
        Submitted.Add(request);
        return SubmitException is null ? Task.CompletedTask : throw SubmitException;
    }
}
