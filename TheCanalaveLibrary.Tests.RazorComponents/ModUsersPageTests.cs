using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Regression test for a bug found live during WU-AccountEnforcement's browser verification
/// (2026-07-30) — unrelated to that WU's own scope, but fixed in the same session per
/// <c>debugging.md</c>'s "fix same-session" discipline. The <c>datetime-local</c> input backing
/// <c>_suspendUntil</c> produces a <see cref="DateTime"/> with <c>Kind=Unspecified</c>; passed
/// straight through to <c>ApplyAccountActionAsync</c>, Npgsql rejected it
/// (<c>ArgumentException: Cannot write DateTime with Kind=Unspecified to PostgreSQL type
/// 'timestamp with time zone'</c>) — this path had only ever been exercised via direct psql/fixture
/// dates before, never through the real form. Tier: RazorComponents (bUnit).
/// </summary>
public class ModUsersPageTests : BunitContext
{
    [Fact]
    public async Task SuspendUser_SubmitsUtcKindDateTime()
    {
        RecordingModerationWriteService writeService = new();
        Services.AddSingleton<IModerationReadService>(new StaticModerationReadService(
            new ReportQueueItemDto(
                ReportId: 1,
                EntityType: ReportedEntityType.User,
                EntityId: 42,
                TargetLabel: "SomeUser",
                TargetUrl: "/user/SomeUser",
                ReasonName: "Other",
                Notes: null,
                Status: ReportStatusEnum.UnderReview,
                ReporterUserName: "Reporter",
                ModeratorUserId: null,
                ActionTaken: null,
                DateReported: DateTime.UtcNow,
                DateResolved: null,
                TargetActiveReportCount: 1)));
        Services.AddSingleton<IModerationWriteService>(writeService);
        this.AddAuthorization().SetAuthorized("mod-user").SetRoles("Moderator");

        IRenderedComponent<ModUsersPage> cut = Render<ModUsersPage>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Suspend"));

        // AngleSharp compound-selector fragility (testing.md) — button text isn't a CSS selector;
        // locate by exact TextContent rather than a brittle :contains(), same as
        // GroupFolderManagementPageTests/ConfirmDialogTests.
        FindButton(cut, "Suspend").Click();
        cut.Find("#suspend-until").Change("2026-08-20T00:00:00");
        cut.Find("#action-reason").Change("Regression test for the Kind=Unspecified bug.");
        await FindButton(cut, "Confirm").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        writeService.LastSuspendedUntilUtc.Should().NotBeNull();
        writeService.LastSuspendedUntilUtc!.Value.Kind.Should().Be(DateTimeKind.Utc,
            "an Unspecified-kind DateTime crashes Npgsql's write to a timestamptz column — " +
            "the page must tag it before handing it to the write service");
        writeService.LastSuspendedUntilUtc.Value.Should().Be(new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc),
            "the label reads '(UTC)' — the moderator's entered clock value must pass through unshifted, only re-tagged");
    }

    private static AngleSharp.Dom.IElement FindButton(IRenderedComponent<ModUsersPage> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == text);

    private sealed class StaticModerationReadService(params ReportQueueItemDto[] reports) : IModerationReadService
    {
        public Task<ReportReasonDto[]> GetReportReasonsAsync() => Task.FromResult(Array.Empty<ReportReasonDto>());
        public Task<ReportQueueItemDto[]> GetReportQueueAsync(bool includeResolved = false) => Task.FromResult(reports);
        public Task<StorySubmissionQueueItemDto[]> GetPendingSubmissionsAsync() => Task.FromResult(Array.Empty<StorySubmissionQueueItemDto>());
    }

    private sealed class RecordingModerationWriteService : IModerationWriteService
    {
        public DateTime? LastSuspendedUntilUtc { get; private set; }

        public Task<ReportReasonDto[]> GetReportReasonsAsync() => throw new NotImplementedException();
        public Task<ReportQueueItemDto[]> GetReportQueueAsync(bool includeResolved = false) => throw new NotImplementedException();
        public Task<StorySubmissionQueueItemDto[]> GetPendingSubmissionsAsync() => throw new NotImplementedException();

        public Task SubmitReportAsync(SubmitReportRequest request) => throw new NotImplementedException();
        public Task ClaimReportAsync(long reportId) => throw new NotImplementedException();
        public Task ResolveNoActionAsync(long reportId, string? actionNotes) => throw new NotImplementedException();
        public Task ResolveWithRemovalAsync(long reportId, string removalReason, bool hardDelete = false) => throw new NotImplementedException();
        public Task ApproveStoryAsync(int storyId) => throw new NotImplementedException();
        public Task RejectStoryAsync(int storyId, string reason) => throw new NotImplementedException();

        public Task ApplyAccountActionAsync(long reportId, ModeratorActionType action,
            string reason, DateTime? suspendedUntilUtc = null)
        {
            LastSuspendedUntilUtc = suspendedUntilUtc;
            return Task.CompletedTask;
        }
    }
}
