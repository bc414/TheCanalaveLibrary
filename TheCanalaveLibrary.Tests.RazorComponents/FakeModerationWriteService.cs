using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// No-op stand-in for <see cref="IModerationWriteService"/> used by any test that renders a
/// component tree containing <c>ReportDialog</c> (which injects this service).
/// <c>ReportDialog</c> is only interactive when the user clicks the trigger button; bUnit tests
/// that don't exercise the dialog just need it in the DI container so the component can be
/// instantiated. Methods that are called throw <see cref="NotImplementedException"/> so any
/// unexpected invocation is surfaced immediately.
/// </summary>
public class FakeModerationWriteService : IModerationWriteService
{
    // ── Read ─────────────────────────────────────────────────────────────────────────────────────

    public Task<ReportReasonDto[]> GetReportReasonsAsync() =>
        Task.FromResult(Array.Empty<ReportReasonDto>());

    public Task<ReportQueueItemDto[]> GetReportQueueAsync(bool includeResolved = false) =>
        Task.FromResult(Array.Empty<ReportQueueItemDto>());

    public Task<StorySubmissionQueueItemDto[]> GetPendingSubmissionsAsync() =>
        Task.FromResult(Array.Empty<StorySubmissionQueueItemDto>());

    // ── Write ────────────────────────────────────────────────────────────────────────────────────

    public Task SubmitReportAsync(SubmitReportRequest request) =>
        throw new NotImplementedException("FakeModerationWriteService.SubmitReportAsync not expected in this test.");

    public Task ClaimReportAsync(long reportId) =>
        throw new NotImplementedException("FakeModerationWriteService.ClaimReportAsync not expected in this test.");

    public Task ResolveNoActionAsync(long reportId, string? actionNotes) =>
        throw new NotImplementedException("FakeModerationWriteService.ResolveNoActionAsync not expected in this test.");

    public Task ResolveWithRemovalAsync(long reportId, string removalReason, bool hardDelete = false) =>
        throw new NotImplementedException("FakeModerationWriteService.ResolveWithRemovalAsync not expected in this test.");

    public Task ApplyAccountActionAsync(long reportId, ModeratorActionType action,
        string reason, DateTime? suspendedUntilUtc = null) =>
        throw new NotImplementedException("FakeModerationWriteService.ApplyAccountActionAsync not expected in this test.");

    public Task ApproveStoryAsync(int storyId) =>
        throw new NotImplementedException("FakeModerationWriteService.ApproveStoryAsync not expected in this test.");

    public Task RejectStoryAsync(int storyId, string reason) =>
        throw new NotImplementedException("FakeModerationWriteService.RejectStoryAsync not expected in this test.");
}
