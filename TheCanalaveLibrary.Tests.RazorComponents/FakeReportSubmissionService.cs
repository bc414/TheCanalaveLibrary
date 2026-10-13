using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// No-op stand-in for <see cref="IReportSubmissionService"/> — what <c>ReportDialog</c> injects (owner
/// ruling D9's split). Register it for any component tree containing a <c>ReportDialog</c>: a missing
/// registration is a render-time DI failure, not a compile error. Reasons are empty;
/// <see cref="SubmitReportAsync"/> throws <see cref="NotImplementedException"/> so an unexpected
/// submission is surfaced immediately. Tests that drive the dialog can set <see cref="Reasons"/>.
/// </summary>
public class FakeReportSubmissionService : IReportSubmissionService
{
    public ReportReasonDto[] Reasons { get; set; } = [];

    /// <summary>When set, <see cref="SubmitReportAsync"/> throws it (ReportDialogTests' refusal paths).</summary>
    public Exception? SubmitException { get; set; }

    public Task<ReportReasonDto[]> GetReportReasonsAsync() => Task.FromResult(Reasons);

    public Task SubmitReportAsync(SubmitReportRequest request) =>
        throw SubmitException
              ?? new NotImplementedException("FakeReportSubmissionService.SubmitReportAsync not expected in this test.");
}
