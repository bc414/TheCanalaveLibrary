using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// <see cref="StoryEditorPage"/>'s lifecycle handler (WU-StoryLifecycle, owner ruling D1; added by the
/// review fixes, 2026-09-30). <see cref="StoryLifecyclePanelTests"/> proves the panel asks for the
/// right TARGET; this proves the page then applies the RESULTING status the server returns — a
/// trusted author's submit asks for <c>PendingApproval</c> and lands published — re-rendering the
/// panel from it, choosing the notice from it, and showing the "Status when published" select only
/// while the result is unpublished. Also: a refusal shows the server's message and keeps the status;
/// a 403 renders the forbidden state. The transition itself is Integration-covered
/// (<c>StoryLifecycleTests</c>). The 2026-09-30 browser pass added two page regressions: the rejection
/// reason reaches the panel (it rendered the field's name), and a refused save leaves the Quill-owned
/// editor markup alone (it killed the circuit).
/// <para>
/// Setup: the chapter-manager, arc-manager and chapter-import panels (each with services of its own)
/// are bUnit stubs. <see cref="StoryPropertiesForm"/> and <see cref="DraftAutosave"/> render for real —
/// the page captures both with <c>@ref</c>, which a stub instance can't satisfy — over loose JS interop
/// (Quill, draft-autosave.js).
/// </para>
/// Tier: RazorComponents (bUnit).
/// </summary>
public class StoryEditorPageTests : BunitContext
{
    private const int StoryId = 7;

    private readonly FakeStoryReadService _reads = new();
    private readonly ScriptedStoryWriteService _writes = new();

    public StoryEditorPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddSingleton<IStoryReadService>(_reads);
        Services.AddSingleton<IStoryWriteService>(_writes);
        EmptyExternalVerificationService verification = new();
        Services.AddSingleton<IExternalVerificationReadService>(verification);
        Services.AddSingleton<IExternalVerificationWriteService>(verification);
        Services.AddSingleton<ITagReadService>(new FakeTagReadService());
        Services.AddSingleton<IFanonReadService>(new UnusedFanonReadService());
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
        Services.AddScoped<DraftStore>();
        Services.AddSingleton<IToastService>(new ToastService());

        ComponentFactories.AddStub<ChapterManagerPanel>();
        ComponentFactories.AddStub<StoryArcManagerPanel>();
        ComponentFactories.AddStub<StoryChapterImport>();
    }

    private IRenderedComponent<StoryEditorPage> RenderEditor(
        StoryStatusEnum status, StoryStatusEnum postApproval = StoryStatusEnum.Completed,
        string? rejectionReason = null, string? longDescription = null)
    {
        _reads.EditDto = new StoryUpdateDTO
        {
            StoryId = StoryId,
            Title = "Lifecycle Story",
            ShortDescription = "A short description",
            Rating = Rating.E,
            StoryStatusId = status,
            PostApprovalStatus = postApproval,
            RejectionReason = rejectionReason,
            LongDescription = longDescription,
        };
        return Render<StoryEditorPage>(p => p.Add(c => c.StoryId, StoryId));
    }

    private static Task ClickAsync(IRenderedComponent<StoryEditorPage> cut, string label) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == label).ClickAsync(new());

    private static string ShownStatus(IRenderedComponent<StoryEditorPage> cut) =>
        cut.Find("[data-testid=lifecycle-status]").TextContent;

    [Fact]
    public async Task TrustedSubmit_AppliesTheResultingPublishedStatus_NotTheTarget()
    {
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(StoryStatusEnum.Draft);
        cut.FindAll("#story-post-approval-status").Should().NotBeEmpty("a draft chooses its status-when-published");
        _writes.Result = StoryStatusEnum.Completed; // the trust waiver routes submit to PostApprovalStatus

        await ClickAsync(cut, "Submit for publication");

        _writes.Requested.Should().Equal([StoryStatusEnum.PendingApproval], "the panel asks for a submit");
        ShownStatus(cut).Should().Be(StoryDisplayFormat.StatusLabel(StoryStatusEnum.Completed),
            "the page shows where the story LANDED — showing the target would read 'Pending Approval'");
        cut.Markup.Should().Contain("Published.");
        cut.Markup.Should().NotContain("Submitted for review.");
        cut.FindAll("#story-post-approval-status").Should().BeEmpty(
            "a published story changes status through the panel, so the form's select goes away");
        cut.FindAll("button").Should().Contain(b => b.TextContent.Trim() == "Unpublish",
            "the panel re-renders with the published-story actions");
    }

    [Fact]
    public async Task UntrustedSubmit_ShowsPending_AndSubmittedForReview()
    {
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(StoryStatusEnum.Draft);
        _writes.Result = StoryStatusEnum.PendingApproval;

        await ClickAsync(cut, "Submit for publication");

        ShownStatus(cut).Should().Be(StoryDisplayFormat.StatusLabel(StoryStatusEnum.PendingApproval));
        cut.Markup.Should().Contain("Submitted for review.");
        cut.FindAll("#story-post-approval-status").Should().NotBeEmpty("a queued story is still unpublished");
        cut.FindAll("button").Should().Contain(b => b.TextContent.Trim() == "Withdraw");
    }

    [Fact]
    public async Task Unpublish_AfterConfirming_ShowsDraft_AndBringsBackTheStatusWhenPublishedSelect()
    {
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(StoryStatusEnum.InProgress);
        cut.FindAll("#story-post-approval-status").Should().BeEmpty();
        _writes.Result = StoryStatusEnum.Draft;

        await ClickAsync(cut, "Unpublish");
        await cut.Find("[role=dialog]").QuerySelectorAll("button")
            .Single(b => b.TextContent.Trim() == "Unpublish").ClickAsync(new());

        _writes.Requested.Should().Equal([StoryStatusEnum.Draft]);
        ShownStatus(cut).Should().Be(StoryDisplayFormat.StatusLabel(StoryStatusEnum.Draft));
        cut.Markup.Should().Contain("Unpublished — the story is a draft again.");
        cut.FindAll("#story-post-approval-status").Should().NotBeEmpty();
    }

    [Fact]
    public async Task RefusedTransition_ShowsTheServerMessage_AndKeepsTheStatus()
    {
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(StoryStatusEnum.PendingApproval);
        _writes.Failure = new StoryValidationException(["This story's status just changed — reload the page and try again."]);

        await ClickAsync(cut, "Withdraw");

        cut.Markup.Should().Contain("This story's status just changed");
        ShownStatus(cut).Should().Be(StoryDisplayFormat.StatusLabel(StoryStatusEnum.PendingApproval));
        cut.Markup.Should().NotContain("Moved back to draft.");
    }

    [Fact]
    public async Task ForbiddenTransition_RendersTheForbiddenState()
    {
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(StoryStatusEnum.Draft);
        _writes.Failure = new UnauthorizedAccessException("You can only change the status of your own stories.");

        await ClickAsync(cut, "Submit for publication");

        cut.Markup.Should().Contain("You don't have permission to edit this story.");
    }

    // ── Browser-pass regressions (2026-09-30) ─────────────────────────────────────

    [Fact]
    public void RejectedStory_ShowsTheModeratorsReason_NotTheFieldName()
    {
        // The page passed RejectionReason="_rejectionReason" (no @): Razor reads an un-prefixed value
        // on a string parameter as a literal, so every rejected story showed
        // "Reason: _rejectionReason" on both render phases.
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(
            StoryStatusEnum.Rejected, rejectionReason: "Please add a content warning.");

        cut.Find("[data-testid=lifecycle-rejection-reason]").TextContent
            .Should().Contain("Please add a content warning.");
        cut.Markup.Should().NotContain("_rejectionReason");
    }

    [Fact]
    public async Task RefusedSave_ShowsTheServerMessage_AndLeavesTheQuillContentAlone()
    {
        // The browser pass's circuit crash: Save pulls the editor's HTML, writes it back into the view
        // model bound to EditorView.Html, and the server refuses. The re-render then changed the markup
        // Quill had already taken over — TypeError removeChild of null, circuit dead. EditorView now
        // freezes its rendered content at the first render, so the edited HTML must not reach it.
        JSInterop.Setup<string>("QuillFunctions.getQuillHTML", _ => true).SetResult("<p>edited summary</p>");
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(
            StoryStatusEnum.Draft, longDescription: "<p>original summary</p>");
        _writes.SaveFailure = new StoryValidationException(["Your story must have at least one Setting tag selected."]);

        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("Your story must have at least one Setting tag selected.");
        cut.Markup.Should().Contain("original summary");
        cut.Markup.Should().NotContain("edited summary",
            "a changed EditorContent is exactly the diff that crashes the renderer under real Quill");
    }

    // ── Fakes ─────────────────────────────────────────────────────────────────────

    /// <summary>Records each requested target and answers with <see cref="Result"/> (the status the
    /// server says the story landed on) or throws <see cref="Failure"/>.</summary>
    private sealed class ScriptedStoryWriteService : IStoryWriteService
    {
        public StoryStatusEnum Result { get; set; }
        public Exception? Failure { get; set; }
        public Exception? SaveFailure { get; set; }
        public List<StoryStatusEnum> Requested { get; } = [];

        public Task<StoryStatusEnum> TransitionStatusAsync(int storyId, StoryStatusEnum targetStatus)
        {
            Requested.Add(targetStatus);
            return Failure is null ? Task.FromResult(Result) : Task.FromException<StoryStatusEnum>(Failure);
        }

        public Task<int> CreateStoryAsync(CreateStoryDTO dto) => throw new NotImplementedException();
        public Task UpdateStoryAsync(StoryUpdateDTO dto) =>
            SaveFailure is null ? throw new NotImplementedException() : Task.FromException(SaveFailure);
        public Task<string> UploadCoverArtAsync(Stream content, string contentType, int storyId) => throw new NotImplementedException();
    }

    private sealed class UnusedFanonReadService : IFanonReadService
    {
        public Task<IReadOnlyList<FanonGroupDto>> GetGroupsAsync(TagTypeEnum axis, string? search, int page, int pageSize) => throw new NotImplementedException();
        public Task<int> GetGroupCountAsync(TagTypeEnum axis, string? search) => throw new NotImplementedException();
        public Task<FanonGroupStoriesDto> GetGroupStoriesAsync(TagTypeEnum axis, int baseTagId, string name) => throw new NotImplementedException();
        public Task<IReadOnlyList<FanonTagDto>> GetEstablishedFanonTagsAsync() => throw new NotImplementedException();
        public Task<TagAdoptionPageDto?> GetMyAdoptionPageAsync(int targetTagId) => throw new NotImplementedException();
        public Task<IReadOnlyList<MyTagAdoptionSummaryDto>> GetMyAdoptionIndexAsync() => throw new NotImplementedException();
        public Task<TagChipDto?> FindOfficialTagByNameAsync(TagTypeEnum axis, string name) => Task.FromResult<TagChipDto?>(null);
    }

    private sealed class EmptyExternalVerificationService : IExternalVerificationWriteService
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
