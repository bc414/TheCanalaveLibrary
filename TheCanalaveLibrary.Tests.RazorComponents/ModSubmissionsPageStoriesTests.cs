using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// <see cref="ModSubmissionsPage"/>'s Stories tab after WU-StoryLifecycle (owner rulings D1/D2):
/// the "submitted" date is the nullable <c>SubmittedDate</c>, and a guard refusal from approve or
/// reject (<see cref="ModerationValidationException"/> — e.g. another moderator or an author
/// withdraw got there first) shows its message AND reloads the queue so a handled row disappears —
/// and a reload that itself fails stays inside the handler (message kept, never stuck on
/// "Loading…"), because it runs from a catch block. The guards themselves are Integration-covered
/// (<c>ModerationServiceTests</c>).
/// Tier: RazorComponents (bUnit).
/// </summary>
public class ModSubmissionsPageStoriesTests : BunitContext
{
    private readonly ScriptedModerationWriteService _moderation = new();

    public ModSubmissionsPageStoriesTests()
    {
        Services.AddSingleton<IModerationWriteService>(_moderation);
        Services.AddSingleton<IExternalVerificationWriteService>(new EmptyExternalVerificationWriteService());
    }

    private static StorySubmissionQueueItemDto Row(int storyId, DateTime? submitted) =>
        new(storyId, $"Pending {storyId}", "SomeAuthor", Rating.E, submitted, StoryStatusEnum.InProgress, false);

    [Fact]
    public void SubmittedDate_RendersTheDate_OrADashWhenNull()
    {
        _moderation.Queue = [Row(1, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)), Row(2, null)];

        IRenderedComponent<ModSubmissionsPage> cut = Render<ModSubmissionsPage>();

        cut.Markup.Should().Contain("submitted 2026-09-01");
        cut.Markup.Should().Contain("submitted —");
    }

    [Fact]
    public async Task Approve_AlreadyHandled_ShowsMessage_AndReloadsTheQueue()
    {
        _moderation.Queue = [Row(1, DateTime.UtcNow)];
        _moderation.ApproveBehavior = () =>
        {
            _moderation.Queue = []; // another moderator approved it meanwhile
            throw new ModerationValidationException(["This submission was already handled."]);
        };

        IRenderedComponent<ModSubmissionsPage> cut = Render<ModSubmissionsPage>();
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Approve").ClickAsync(new());

        cut.Markup.Should().Contain("This submission was already handled.");
        cut.Markup.Should().Contain("No pending submissions.", "the queue reloads, so the handled row is gone");
    }

    [Fact]
    public async Task Approve_LiveAuthorRefusal_ShowsMessage_RowStays()
    {
        _moderation.Queue = [Row(1, DateTime.UtcNow)];
        _moderation.ApproveBehavior = () => throw new ModerationValidationException(
            ["This story's author is banned or suspended, so it can't be approved — reject it instead."]);

        IRenderedComponent<ModSubmissionsPage> cut = Render<ModSubmissionsPage>();
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Approve").ClickAsync(new());

        cut.Markup.Should().Contain("reject it instead");
        cut.Markup.Should().Contain("Pending 1", "the row is still pending — the moderator can reject it");
    }

    [Fact]
    public async Task Reject_AlreadyHandled_ShowsMessage_AndReloadsTheQueue()
    {
        _moderation.Queue = [Row(1, DateTime.UtcNow)];
        _moderation.RejectBehavior = () =>
        {
            _moderation.Queue = [];
            throw new ModerationValidationException(["This submission was already handled."]);
        };

        IRenderedComponent<ModSubmissionsPage> cut = Render<ModSubmissionsPage>();
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Reject").ClickAsync(new());
        cut.Find("textarea").Change("Spam.");
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Confirm reject").ClickAsync(new());

        cut.Markup.Should().Contain("This submission was already handled.");
        cut.Markup.Should().Contain("No pending submissions.");
    }

    [Fact]
    public async Task Approve_Refusal_ThenTheReloadFails_KeepsTheMessage_AndTheQueue()
    {
        _moderation.Queue = [Row(1, DateTime.UtcNow)];
        IRenderedComponent<ModSubmissionsPage> cut = Render<ModSubmissionsPage>();
        _moderation.ApproveBehavior = () => throw new ModerationValidationException(["This submission was already handled."]);
        _moderation.LoadBehavior = () => throw new HttpRequestException("Network down.");

        // Before the fix this reload ran unguarded inside the catch: the exception escaped the event
        // handler (this await would throw) and the queue was stranded on "Loading…".
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Approve").ClickAsync(new());

        cut.Markup.Should().Contain("This submission was already handled.");
        cut.Markup.Should().NotContain("Loading", "a failed reload must reset the loading state");
        cut.Markup.Should().Contain("Pending 1", "the stale queue stays on screen");
    }

    [Fact]
    public async Task Reject_Refusal_ThenTheReloadFails_KeepsTheMessage_AndTheQueue()
    {
        _moderation.Queue = [Row(1, DateTime.UtcNow)];
        IRenderedComponent<ModSubmissionsPage> cut = Render<ModSubmissionsPage>();
        _moderation.RejectBehavior = () => throw new ModerationValidationException(["This submission was already handled."]);
        _moderation.LoadBehavior = () => throw new HttpRequestException("Network down.");

        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Reject").ClickAsync(new());
        cut.Find("textarea").Change("Spam.");
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Confirm reject").ClickAsync(new());

        cut.Markup.Should().Contain("This submission was already handled.");
        cut.Markup.Should().NotContain("Loading");
        cut.Markup.Should().Contain("Pending 1");
    }

    // ── Fakes ─────────────────────────────────────────────────────────────────────

    private sealed class ScriptedModerationWriteService : IModerationWriteService
    {
        public StorySubmissionQueueItemDto[] Queue { get; set; } = [];
        public Action ApproveBehavior { get; set; } = () => { };
        public Action RejectBehavior { get; set; } = () => { };
        public Action LoadBehavior { get; set; } = () => { };

        public Task<StorySubmissionQueueItemDto[]> GetPendingSubmissionsAsync()
        {
            LoadBehavior();
            return Task.FromResult(Queue);
        }

        public Task ApproveStoryAsync(int storyId)
        {
            ApproveBehavior();
            return Task.CompletedTask;
        }

        public Task RejectStoryAsync(int storyId, string reason)
        {
            RejectBehavior();
            return Task.CompletedTask;
        }

        public Task<ReportReasonDto[]> GetReportReasonsAsync() => throw new NotImplementedException();
        public Task<ReportQueueItemDto[]> GetReportQueueAsync(bool includeResolved = false) => throw new NotImplementedException();
        public Task<UserModerationHistoryDto?> GetUserModerationHistoryAsync(int userId) => throw new NotImplementedException();
        public Task SubmitReportAsync(SubmitReportRequest request) => throw new NotImplementedException();
        public Task ClaimReportAsync(long reportId) => throw new NotImplementedException();
        public Task ResolveNoActionAsync(long reportId, string? actionNotes) => throw new NotImplementedException();
        public Task ResolveWithRemovalAsync(long reportId, string removalReason, bool hardDelete = false) => throw new NotImplementedException();
        public Task ApplyAccountActionAsync(long reportId, ModeratorActionType action, string reason, DateTime? suspendedUntilUtc = null) => throw new NotImplementedException();
        public Task ApplyAccountActionToUserAsync(int targetUserId, short reasonId, ModeratorActionType action, string reason, DateTime? suspendedUntilUtc = null) => throw new NotImplementedException();
        public Task SetCanAutoApproveAsync(int targetUserId, bool canAutoApprove, short reasonId, string reason) => throw new NotImplementedException();
    }

    private sealed class EmptyExternalVerificationWriteService : IExternalVerificationWriteService
    {
        public Task<IReadOnlyList<VerificationPlatformDto>> GetVerificationPlatformsAsync() => Task.FromResult<IReadOnlyList<VerificationPlatformDto>>([]);
        public Task<IReadOnlyList<ExternalAccountDto>> GetMyExternalAccountsAsync() => Task.FromResult<IReadOnlyList<ExternalAccountDto>>([]);
        public Task<IReadOnlyList<PendingAccountVerificationDto>> GetPendingAccountVerificationsAsync() => Task.FromResult<IReadOnlyList<PendingAccountVerificationDto>>([]);
        public Task<IReadOnlyList<PendingLinkVerificationDto>> GetPendingLinkVerificationsAsync() => Task.FromResult<IReadOnlyList<PendingLinkVerificationDto>>([]);
        public Task<string> EnsureMyVerificationCodeAsync() => throw new NotImplementedException();
        public Task SubmitAccountForVerificationAsync(AddExternalAccountRequest request) => throw new NotImplementedException();
        public Task RequestLinkVerificationAsync(int storyExternalLinkId) => throw new NotImplementedException();
        public Task ApproveAccountVerificationAsync(int userExternalIdentityId) => throw new NotImplementedException();
        public Task RejectAccountVerificationAsync(int userExternalIdentityId, string reason) => throw new NotImplementedException();
        public Task ApproveLinkVerificationAsync(int storyExternalLinkId) => throw new NotImplementedException();
        public Task RejectLinkVerificationAsync(int storyExternalLinkId, string reason) => throw new NotImplementedException();
    }
}
