using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// <c>/mod/users/{UserId:int?}</c> renders two different surfaces off one route: a lookup view with
/// no id, a per-user history + account-action view with one. The parameter was declared and never
/// read from WU34 until WU-UserModeration wired it (tracker item B13), so these are the first tests
/// to drive either branch.
///
/// <para>The UTC-Kind regression test that used to live here moved to
/// <see cref="AccountActionPanelTests"/> along with the panel itself — the fix now has one
/// implementation shared by this page and <c>/mod/reports</c>.</para>
///
/// <para>Picking a user through <c>UserPicker</c> is deliberately not driven here: it needs
/// JS-level typeahead interaction bUnit doesn't drive reliably, the same documented limitation as
/// <c>StoryTitlePickerTests</c>/<c>ModSpotlightPageTests</c>. The search itself is covered at the
/// Integration tier (<c>UserProfileEndpointsTests</c>), and the action path by
/// <c>ModerationServiceTests</c>.</para>
///
/// Tier: RazorComponents (bUnit).
/// </summary>
public class ModUsersPageTests : BunitContext
{
    private static readonly ReportQueueItemDto UserReport = new(
        ReportId: 1,
        EntityType: ReportedEntityType.User,
        EntityId: 42,
        TargetLabel: "SomeUser",
        TargetUrl: "/user/SomeUser",
        ReasonName: "Harassment",
        Notes: null,
        Status: ReportStatusEnum.Open,
        ReporterUserName: "Reporter",
        ModeratorUserId: null,
        ActionTaken: null,
        DateReported: DateTime.UtcNow,
        DateResolved: null,
        TargetActiveReportCount: 1);

    private RecordingModerationWriteService Arrange(UserModerationHistoryDto? history = null)
    {
        // CanalaveTypeahead (inside UserPicker) touches typeahead.js on render.
        JSInterop.Mode = JSRuntimeMode.Loose;

        RecordingModerationWriteService writeService = new();
        Services.AddSingleton<IModerationReadService>(new StaticModerationReadService(history, UserReport));
        Services.AddSingleton<IModerationWriteService>(writeService);
        Services.AddSingleton<IUserProfileReadService>(new FakeUserProfileReadService());
        this.AddAuthorization().SetAuthorized("mod-user").SetRoles("Moderator");
        return writeService;
    }

    [Fact]
    public void NoUserId_RendersLookupPicker_AndReportedUsersTable()
    {
        Arrange();

        IRenderedComponent<ModUsersPage> cut = Render<ModUsersPage>();

        cut.WaitForAssertion(() =>
            cut.Find("input[type=text]").GetAttribute("placeholder").Should().Be("Type a username..."));

        cut.Markup.Should().Contain("Reported users");
        cut.Markup.Should().Contain("SomeUser", "the already-reported triage list stays on the lookup view");
        cut.Markup.Should().Contain("/mod/users/42", "each triage row links into that user's history");
    }

    [Fact]
    public void WithUserId_RendersAccountStandingAndHistory()
    {
        Arrange(new UserModerationHistoryDto(
            UserId: 42,
            Username: "SomeUser",
            AvatarUrl: null,
            AccountStatus: AccountStatusEnum.Suspended,
            SuspendedUntilUtc: new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc),
            ActiveReportCount: 3,
            Reports: [UserReport]));

        IRenderedComponent<ModUsersPage> cut = Render<ModUsersPage>(p => p.Add(c => c.UserId, 42));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Report history"));

        cut.Markup.Should().Contain("SomeUser");
        cut.Markup.Should().Contain("Suspended", "the current account standing is the headline fact");
        cut.Markup.Should().Contain("2026-08-20", "a suspension's end date is what makes it not a ban");
        cut.Markup.Should().Contain("3 active report(s)");
        cut.Markup.Should().Contain("Harassment");

        // The scope caveat must be visible, not just documented on the DTO: an empty history here
        // does NOT mean nobody has complained about this person's content.
        cut.Markup.Should().Contain("Reports against content they wrote are not listed here.");
    }

    [Fact]
    public void WithUnknownUserId_RendersNotFoundState()
    {
        Arrange(history: null);

        IRenderedComponent<ModUsersPage> cut = Render<ModUsersPage>(p => p.Add(c => c.UserId, 999));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("No user with id 999 exists."));
        cut.Markup.Should().NotContain("Report history");
    }

    [Fact]
    public async Task WithUserId_BanSubmitsModeratorInitiatedAction()
    {
        RecordingModerationWriteService writeService = Arrange(new UserModerationHistoryDto(
            UserId: 42,
            Username: "SomeUser",
            AvatarUrl: null,
            AccountStatus: AccountStatusEnum.Active,
            SuspendedUntilUtc: null,
            ActiveReportCount: 0,
            Reports: []));

        IRenderedComponent<ModUsersPage> cut = Render<ModUsersPage>(p => p.Add(c => c.UserId, 42));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Account action"));

        FindButton(cut, "Ban").Click();
        cut.Find("textarea").Change("Ban evasion.");
        await FindButton(cut, "Confirm").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // The user-keyed overload, not the report-keyed one — this page has no report to act on.
        writeService.LastUserAction.Should().NotBeNull();
        writeService.LastUserAction!.Value.TargetUserId.Should().Be(42);
        writeService.LastUserAction.Value.Action.Should().Be(ModeratorActionType.BanUser);
        writeService.LastUserAction.Value.Reason.Should().Be("Ban evasion.");
        writeService.LastUserAction.Value.ReasonId.Should().Be(4, "the first seeded reason the fake offers");
    }

    // AngleSharp compound-selector fragility (testing.md) — button text isn't a CSS selector.
    private static AngleSharp.Dom.IElement FindButton(IRenderedComponent<ModUsersPage> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == text);

    private sealed class StaticModerationReadService(
        UserModerationHistoryDto? history,
        params ReportQueueItemDto[] reports) : IModerationReadService
    {
        public Task<ReportReasonDto[]> GetReportReasonsAsync() =>
            Task.FromResult<ReportReasonDto[]>([new ReportReasonDto(4, "Harassment", null)]);
        public Task<ReportQueueItemDto[]> GetReportQueueAsync(bool includeResolved = false) => Task.FromResult(reports);
        public Task<StorySubmissionQueueItemDto[]> GetPendingSubmissionsAsync() => Task.FromResult(Array.Empty<StorySubmissionQueueItemDto>());
        public Task<UserModerationHistoryDto?> GetUserModerationHistoryAsync(int userId) => Task.FromResult(history);
    }

    private sealed class RecordingModerationWriteService : IModerationWriteService
    {
        public (int TargetUserId, short ReasonId, ModeratorActionType Action, string Reason, DateTime? Until)? LastUserAction { get; private set; }

        public Task<ReportReasonDto[]> GetReportReasonsAsync() => throw new NotImplementedException();
        public Task<ReportQueueItemDto[]> GetReportQueueAsync(bool includeResolved = false) => throw new NotImplementedException();
        public Task<StorySubmissionQueueItemDto[]> GetPendingSubmissionsAsync() => throw new NotImplementedException();
        public Task<UserModerationHistoryDto?> GetUserModerationHistoryAsync(int userId) => throw new NotImplementedException();

        public Task SubmitReportAsync(SubmitReportRequest request) => throw new NotImplementedException();
        public Task ClaimReportAsync(long reportId) => throw new NotImplementedException();
        public Task ResolveNoActionAsync(long reportId, string? actionNotes) => throw new NotImplementedException();
        public Task ResolveWithRemovalAsync(long reportId, string removalReason, bool hardDelete = false) => throw new NotImplementedException();
        public Task ApproveStoryAsync(int storyId) => throw new NotImplementedException();
        public Task RejectStoryAsync(int storyId, string reason) => throw new NotImplementedException();

        public Task ApplyAccountActionAsync(long reportId, ModeratorActionType action,
            string reason, DateTime? suspendedUntilUtc = null) =>
            throw new NotImplementedException("/mod/users acts on a user, never on a report id.");

        public Task ApplyAccountActionToUserAsync(int targetUserId, short reasonId,
            ModeratorActionType action, string reason, DateTime? suspendedUntilUtc = null)
        {
            LastUserAction = (targetUserId, reasonId, action, reason, suspendedUntilUtc);
            return Task.CompletedTask;
        }
    }
}
