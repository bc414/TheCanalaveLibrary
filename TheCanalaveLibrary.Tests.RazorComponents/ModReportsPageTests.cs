using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// <see cref="ModReportsPage"/>'s action panel (WU-ModerationIntegrity, 2026-09-30): a <c>User</c>-typed
/// report is resolved with an account action, never a removal — the service refuses
/// <c>ResolveWithRemovalAsync</c> for it (owner ruling D7 forbids bulk-closing a user's reports, and
/// D47(a)'s floor says removal must be unreachable for a target it cannot act on) — so "Hide content"
/// is not offered for one. The service refusal itself is Integration-covered
/// (<c>ModerationIntegrityTests.RemovalOfAUserReport_IsRefused_AndTheReportStaysOpen</c>).
/// Tier: RazorComponents (bUnit).
/// </summary>
public class ModReportsPageTests : BunitContext
{
    private static ReportQueueItemDto Claimed(long reportId, ReportedEntityType type, string label) => new(
        ReportId: reportId,
        EntityType: type,
        EntityId: 42,
        TargetLabel: label,
        TargetUrl: null,
        ReasonName: "Harassment",
        Notes: null,
        Status: ReportStatusEnum.UnderReview,
        ReporterUserName: "Reporter",
        ModeratorUserId: 1,
        ActionTaken: null,
        DateReported: DateTime.UtcNow,
        DateResolved: null,
        TargetActiveReportCount: 1);

    private IRenderedComponent<ModReportsPage> RenderWithQueue(params ReportQueueItemDto[] queue) =>
        RenderWithQueue(removalFailure: null, queue);

    private IRenderedComponent<ModReportsPage> RenderWithQueue(Exception? removalFailure, params ReportQueueItemDto[] queue)
    {
        Services.AddSingleton<IModerationWriteService>(new QueueOnlyModerationWriteService(queue, removalFailure));
        return Render<ModReportsPage>();
    }

    [Fact]
    public void UserReport_OffersNoHideContent_ButKeepsTheAccountActions()
    {
        IRenderedComponent<ModReportsPage> cut = RenderWithQueue(Claimed(1, ReportedEntityType.User, "SomeUser"));

        FindButton(cut, "Act").Click();

        ButtonTexts(cut).Should().NotContain("Hide content",
            "a User report cannot be resolved by removal — the service refuses it");
        ButtonTexts(cut).Should().Contain(["No action", "Warn user", "Suspend user", "Ban user"]);
    }

    [Fact]
    public void StoryReport_OffersHideContent()
    {
        IRenderedComponent<ModReportsPage> cut = RenderWithQueue(Claimed(2, ReportedEntityType.Story, "A Story"));

        FindButton(cut, "Act").Click();

        ButtonTexts(cut).Should().Contain("Hide content");
    }

    [Fact]
    public async Task HideContent_AnExpiredSession_ThenAnEmptyReason_OffersNoStaleSignIn()
    {
        // The flag travels with the message (the WU-ErrorHandling2 follow-up, WU-TrackerFixes): the
        // early-return "A removal reason is required." used to inherit the previous failure's Sign in.
        IRenderedComponent<ModReportsPage> cut = RenderWithQueue(
            new SessionExpiredException(), Claimed(3, ReportedEntityType.Story, "A Story"));
        FindButton(cut, "Act").Click();

        cut.Find("#action-notes").Change("Spam.");
        await FindButton(cut, "Hide content").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        SignInLinks(cut).Should().Be(1, "the removal failed on an expired session");

        cut.Find("#action-notes").Change("");
        await FindButton(cut, "Hide content").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.Markup.Should().Contain("A removal reason is required.");
        SignInLinks(cut).Should().Be(0, "a validation message never carries the last failure's Sign in");
    }

    private static int SignInLinks(IRenderedComponent<ModReportsPage> cut) =>
        cut.FindAll("a").Count(a => a.TextContent.Trim() == "Sign in");

    private static IEnumerable<string> ButtonTexts(IRenderedComponent<ModReportsPage> cut) =>
        cut.FindAll("button").Select(b => b.TextContent.Trim()).ToList();

    // AngleSharp compound-selector fragility (testing.md) — button text isn't a CSS selector.
    private static AngleSharp.Dom.IElement FindButton(IRenderedComponent<ModReportsPage> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == text);

    /// <summary>Serves a fixed queue; every action throws (none is expected here).</summary>
    private sealed class QueueOnlyModerationWriteService(ReportQueueItemDto[] queue, Exception? removalFailure = null)
        : IModerationWriteService
    {
        public Task<ReportQueueItemDto[]> GetReportQueueAsync(bool includeResolved = false) => Task.FromResult(queue);
        public Task<StorySubmissionQueueItemDto[]> GetPendingSubmissionsAsync() => throw new NotImplementedException();
        public Task<UserModerationHistoryDto?> GetUserModerationHistoryAsync(int userId) => throw new NotImplementedException();
        public Task ClaimReportAsync(long reportId) => throw new NotImplementedException();
        public Task ResolveNoActionAsync(long reportId, string? actionNotes) => throw new NotImplementedException();
        public Task ResolveWithRemovalAsync(long reportId, string removalReason, bool hardDelete = false) =>
            throw removalFailure ?? new NotImplementedException();
        public Task ApplyAccountActionAsync(long reportId, ModeratorActionType action, string reason, DateTime? suspendedUntilUtc = null) => throw new NotImplementedException();
        public Task ApplyAccountActionToUserAsync(int targetUserId, short reasonId, ModeratorActionType action, string reason, DateTime? suspendedUntilUtc = null) => throw new NotImplementedException();
        public Task ReinstateUserAsync(int targetUserId, string reason) => throw new NotImplementedException();
        public Task AssignGroupAdminAsync(int groupId, int targetUserId, string notes) => throw new NotImplementedException();
        public Task ApproveStoryAsync(int storyId) => throw new NotImplementedException();
        public Task RejectStoryAsync(int storyId, string reason) => throw new NotImplementedException();
        public Task SetCanAutoApproveAsync(int targetUserId, bool canAutoApprove, short reasonId, string reason) => throw new NotImplementedException();
    }
}
