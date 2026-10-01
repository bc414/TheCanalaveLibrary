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
        // The reason picker reads the member-facing submission service since D9's split.
        Services.AddSingleton<IReportSubmissionService>(new FakeReportSubmissionService
        {
            Reasons = [new ReportReasonDto(4, "Harassment", null)],
        });
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

        // D8/B18 (WU-ModerationIntegrity): the history now reads ReportedUserId across every target
        // type, so the scope caveat it used to carry is deleted, not narrowed.
        cut.Markup.Should().NotContain("not listed here");
    }

    [Fact]
    public void WithUserId_HistoryShowsATargetColumn_ForContentReports_AndDeletedTargets()
    {
        ReportQueueItemDto storyReport = UserReport with
        {
            ReportId = 2, EntityType = ReportedEntityType.Story, EntityId = 7,
            TargetLabel = "The Reported Story", TargetUrl = "/story/7/the-reported-story",
        };
        ReportQueueItemDto deletedComment = UserReport with
        {
            ReportId = 3, EntityType = ReportedEntityType.Comment, EntityId = 99,
            TargetLabel = "[deleted Comment]", TargetUrl = null,
        };
        Arrange(new UserModerationHistoryDto(
            UserId: 42, Username: "SomeUser", AvatarUrl: null, AccountStatus: AccountStatusEnum.Active,
            SuspendedUntilUtc: null, ActiveReportCount: 1, Reports: [storyReport, deletedComment, UserReport]));

        IRenderedComponent<ModUsersPage> cut = Render<ModUsersPage>(p => p.Add(c => c.UserId, 42));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Report history"));

        var targets = cut.FindAll("[data-testid=history-target]");
        targets.Should().HaveCount(3);
        targets[0].TextContent.Should().Contain("Story").And.Contain("The Reported Story");
        targets[0].QuerySelector("a")!.GetAttribute("href").Should().Be("/story/7/the-reported-story",
            "a report about the user's content links to that content");
        targets[1].TextContent.Should().Contain("[deleted Comment]");
        targets[1].QuerySelector("a").Should().BeNull("a deleted target has nothing to link to");
        targets[2].TextContent.Should().Contain("User");
    }

    [Fact]
    public void WithUserId_Active_OffersNoReinstate()
    {
        Arrange(new UserModerationHistoryDto(
            UserId: 42, Username: "SomeUser", AvatarUrl: null, AccountStatus: AccountStatusEnum.Active,
            SuspendedUntilUtc: null, ActiveReportCount: 0, Reports: []));

        IRenderedComponent<ModUsersPage> cut = Render<ModUsersPage>(p => p.Add(c => c.UserId, 42));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Account action"));

        cut.FindAll("button").Should().NotContain(b => b.TextContent.Trim() == "Reinstate",
            "an Active account has nothing to reinstate");
    }

    [Fact]
    public async Task WithUserId_Banned_ReinstateSubmitsReinstateUserAsync()
    {
        RecordingModerationWriteService writeService = Arrange(new UserModerationHistoryDto(
            UserId: 42, Username: "SomeUser", AvatarUrl: null, AccountStatus: AccountStatusEnum.Banned,
            SuspendedUntilUtc: null, ActiveReportCount: 0, Reports: []));

        IRenderedComponent<ModUsersPage> cut = Render<ModUsersPage>(p => p.Add(c => c.UserId, 42));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Account action"));

        FindButton(cut, "Reinstate").Click();
        cut.FindAll("input[type=datetime-local]").Should().BeEmpty("Reinstate has no end date");
        cut.Find("textarea").Change("Appeal upheld.");
        await FindButton(cut, "Confirm").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        writeService.LastReinstate.Should().Be((42, "Appeal upheld."),
            "Reinstate has its own method — the only path out of a ban (service §2.1.3)");
        writeService.LastUserAction.Should().BeNull("Reinstate never goes through the account-action overload");
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

    // ── Story-approval trust (WU-StoryLifecycle, D1) ──────────────────────────────

    [Fact]
    public void WithUserId_ShowsTrustLine()
    {
        Arrange(new UserModerationHistoryDto(
            UserId: 42, Username: "SomeUser", AvatarUrl: null, AccountStatus: AccountStatusEnum.Active,
            SuspendedUntilUtc: null, ActiveReportCount: 0, Reports: [],
            ApprovedStorySubmissions: 3, CanAutoApprove: false));

        IRenderedComponent<ModUsersPage> cut = Render<ModUsersPage>(p => p.Add(c => c.UserId, 42));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Account action"));

        cut.Find("[data-testid=trust-line]").TextContent.Should()
            .Contain("3 approved submissions").And.Contain("auto-approve off");
        FindButton(cut, "Restore auto-approve").Should().NotBeNull(
            "with the waiver revoked, the control offers the reverse move");
    }

    [Fact]
    public async Task WithUserId_RevokeAutoApprove_CallsServiceWithReasonAndFlippedFlag()
    {
        RecordingModerationWriteService writeService = Arrange(new UserModerationHistoryDto(
            UserId: 42, Username: "SomeUser", AvatarUrl: null, AccountStatus: AccountStatusEnum.Active,
            SuspendedUntilUtc: null, ActiveReportCount: 0, Reports: [],
            ApprovedStorySubmissions: 1, CanAutoApprove: true));

        IRenderedComponent<ModUsersPage> cut = Render<ModUsersPage>(p => p.Add(c => c.UserId, 42));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Account action"));

        FindButton(cut, "Revoke auto-approve").Click();
        cut.Find("#auto-approve-reason").Change("Spam after approval.");
        await FindButton(cut, "Confirm").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        writeService.LastAutoApprove.Should().Be((42, false, (short)4, "Spam after approval."));
    }

    [Fact]
    public async Task WithUserId_RevokeAutoApprove_WithoutReason_DoesNotCallService()
    {
        RecordingModerationWriteService writeService = Arrange(new UserModerationHistoryDto(
            UserId: 42, Username: "SomeUser", AvatarUrl: null, AccountStatus: AccountStatusEnum.Active,
            SuspendedUntilUtc: null, ActiveReportCount: 0, Reports: []));

        IRenderedComponent<ModUsersPage> cut = Render<ModUsersPage>(p => p.Add(c => c.UserId, 42));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Account action"));

        FindButton(cut, "Revoke auto-approve").Click();
        await FindButton(cut, "Confirm").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        writeService.LastAutoApprove.Should().BeNull();
        cut.Markup.Should().Contain("A reason is required.");
    }

    // AngleSharp compound-selector fragility (testing.md) — button text isn't a CSS selector.
    private static AngleSharp.Dom.IElement FindButton(IRenderedComponent<ModUsersPage> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == text);

    private sealed class StaticModerationReadService(
        UserModerationHistoryDto? history,
        params ReportQueueItemDto[] reports) : IModerationReadService
    {
        public Task<ReportQueueItemDto[]> GetReportQueueAsync(bool includeResolved = false) => Task.FromResult(reports);
        public Task<StorySubmissionQueueItemDto[]> GetPendingSubmissionsAsync() => Task.FromResult(Array.Empty<StorySubmissionQueueItemDto>());
        public Task<UserModerationHistoryDto?> GetUserModerationHistoryAsync(int userId) => Task.FromResult(history);
    }

    private sealed class RecordingModerationWriteService : IModerationWriteService
    {
        public (int TargetUserId, short ReasonId, ModeratorActionType Action, string Reason, DateTime? Until)? LastUserAction { get; private set; }
        public (int TargetUserId, bool CanAutoApprove, short ReasonId, string Reason)? LastAutoApprove { get; private set; }
        public (int TargetUserId, string Reason)? LastReinstate { get; private set; }

        public Task ReinstateUserAsync(int targetUserId, string reason)
        {
            LastReinstate = (targetUserId, reason);
            return Task.CompletedTask;
        }

        public Task SetCanAutoApproveAsync(int targetUserId, bool canAutoApprove, short reasonId, string reason)
        {
            LastAutoApprove = (targetUserId, canAutoApprove, reasonId, reason);
            return Task.CompletedTask;
        }

        public Task<ReportQueueItemDto[]> GetReportQueueAsync(bool includeResolved = false) => throw new NotImplementedException();
        public Task<StorySubmissionQueueItemDto[]> GetPendingSubmissionsAsync() => throw new NotImplementedException();
        public Task<UserModerationHistoryDto?> GetUserModerationHistoryAsync(int userId) => throw new NotImplementedException();

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
