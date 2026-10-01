using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// No-op stand-in for <see cref="IModerationWriteService"/> for tests that render a moderator page
/// but do not exercise its actions. (Component trees containing <c>ReportDialog</c> register
/// <see cref="FakeReportSubmissionService"/> instead — the dialog injects the narrow
/// <see cref="IReportSubmissionService"/> since owner ruling D9's split.) Reads return empty; writes
/// throw <see cref="NotImplementedException"/> so any unexpected invocation is surfaced immediately.
/// </summary>
public class FakeModerationWriteService : IModerationWriteService
{
    // ── Read ─────────────────────────────────────────────────────────────────────────────────────

    public Task<ReportQueueItemDto[]> GetReportQueueAsync(bool includeResolved = false) =>
        Task.FromResult(Array.Empty<ReportQueueItemDto>());

    public Task<StorySubmissionQueueItemDto[]> GetPendingSubmissionsAsync() =>
        Task.FromResult(Array.Empty<StorySubmissionQueueItemDto>());

    public Task<UserModerationHistoryDto?> GetUserModerationHistoryAsync(int userId) =>
        Task.FromResult<UserModerationHistoryDto?>(null);

    // ── Write ────────────────────────────────────────────────────────────────────────────────────

    public Task ClaimReportAsync(long reportId) =>
        throw new NotImplementedException("FakeModerationWriteService.ClaimReportAsync not expected in this test.");

    public Task ResolveNoActionAsync(long reportId, string? actionNotes) =>
        throw new NotImplementedException("FakeModerationWriteService.ResolveNoActionAsync not expected in this test.");

    public Task ResolveWithRemovalAsync(long reportId, string removalReason, bool hardDelete = false) =>
        throw new NotImplementedException("FakeModerationWriteService.ResolveWithRemovalAsync not expected in this test.");

    public Task ApplyAccountActionAsync(long reportId, ModeratorActionType action,
        string reason, DateTime? suspendedUntilUtc = null) =>
        throw new NotImplementedException("FakeModerationWriteService.ApplyAccountActionAsync not expected in this test.");

    public Task ApplyAccountActionToUserAsync(int targetUserId, short reasonId,
        ModeratorActionType action, string reason, DateTime? suspendedUntilUtc = null) =>
        throw new NotImplementedException("FakeModerationWriteService.ApplyAccountActionToUserAsync not expected in this test.");

    public Task ApproveStoryAsync(int storyId) =>
        throw new NotImplementedException("FakeModerationWriteService.ApproveStoryAsync not expected in this test.");

    public Task RejectStoryAsync(int storyId, string reason) =>
        throw new NotImplementedException("FakeModerationWriteService.RejectStoryAsync not expected in this test.");

    public Task SetCanAutoApproveAsync(int targetUserId, bool canAutoApprove, short reasonId, string reason) =>
        throw new NotImplementedException("FakeModerationWriteService.SetCanAutoApproveAsync not expected in this test.");

    public Task ReinstateUserAsync(int targetUserId, string reason) =>
        throw new NotImplementedException("FakeModerationWriteService.ReinstateUserAsync not expected in this test.");
}
