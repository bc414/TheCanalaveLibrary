using AngleSharp.Dom;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
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
/// editor markup alone (it killed the circuit). Since WU-BlobCleanup (owner ruling D14): the cover is an
/// immediate commit outside the save, a 403 on it renders the forbidden state, and (its review fixes) a
/// cover call never overlaps a save or a status change, in either order. Since WU-AuthorStoryDelete (owner
/// ruling D15): the delete confirms against the SAVED title, clears the draft backup and leaves for the
/// author's Authored tab with a full document load; a refusal stays in the dialog; Unpublish instead runs in
/// place; the delete is a fourth write that never overlaps the others (the dialog itself:
/// <see cref="StoryDeleteDialogTests"/>); and (its review fixes) a taken-down story opens delete-only.
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
        string? rejectionReason = null, string? longDescription = null, string? cover = null,
        bool isTakenDown = false)
    {
        _reads.EditDto = new StoryEditDTO
        {
            StoryId = StoryId,
            Title = "Lifecycle Story",
            ShortDescription = "A short description",
            Rating = Rating.E,
            StoryStatusId = status,
            PostApprovalStatus = postApproval,
            RejectionReason = rejectionReason,
            LongDescription = longDescription,
            CoverArtRelativeUrl = cover,
            IsTakenDown = isTakenDown,
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

    // ── Cover: an immediate commit, never part of the save (owner ruling D14, WU-BlobCleanup) ──────

    private static readonly byte[] CoverBytes = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4];
    private const string OldCover = "/uploads/stories/7/cover-11111111-1111-1111-1111-111111111111.png";

    private static void PickCover(IRenderedComponent<StoryEditorPage> cut) =>
        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromBinary(CoverBytes, "cover.png", contentType: "image/png"));

    [Fact]
    public void EditMode_PickingACover_UploadsAtOnce_AndShowsIt_WithNoSaveAndNoUpdate()
    {
        // D14's RazorComponents test: the editor renders the newly uploaded cover without a save.
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(StoryStatusEnum.Draft);
        cut.FindAll("img[alt='Current cover']").Should().BeEmpty();

        PickCover(cut);

        cut.WaitForAssertion(() => cut.Find("img[alt='Current cover']").GetAttribute("src")
            .Should().Be(_writes.UploadedPath));
        _writes.Calls.Should().Equal([$"Upload:{StoryId}:image/png:{CoverBytes.Length}"],
            "the upload persists the cover itself; the page must not follow it with UpdateStoryAsync");
        cut.FindAll("button").Should().Contain(b => b.TextContent.Trim() == "Remove cover");
    }

    [Fact]
    public async Task EditMode_RemoveCover_CallsRemove_AndTheCoverDisappears()
    {
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(StoryStatusEnum.Draft, cover: OldCover);
        cut.Find("img[alt='Current cover']").GetAttribute("src").Should().Be(OldCover);

        await ClickAsync(cut, "Remove cover");

        _writes.Calls.Should().Equal([$"Remove:{StoryId}"]);
        cut.FindAll("img[alt='Current cover']").Should().BeEmpty();
        cut.FindAll("button").Should().NotContain(b => b.TextContent.Trim() == "Remove cover");
    }

    [Fact]
    public void EditMode_ARefusedUpload_ShowsTheMessage_AndKeepsTheOldCover()
    {
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(StoryStatusEnum.Draft, cover: OldCover);
        _writes.CoverFailure = new ImageUploadValidationException("Images must be JPEG, PNG or WebP files.");

        PickCover(cut);

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Images must be JPEG, PNG or WebP files."));
        cut.Find("img[alt='Current cover']").GetAttribute("src").Should().Be(OldCover);
        cut.Find("#story-cover-art").HasAttribute("disabled").Should().BeFalse(
            "the picker is usable again once the upload settled");
    }

    [Fact]
    public void EditMode_AForbiddenCoverUpload_RendersTheForbiddenState()
    {
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(StoryStatusEnum.Draft, cover: OldCover);
        _writes.CoverFailure = new UnauthorizedAccessException("You can only change the cover of your own stories.");

        PickCover(cut);

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("You don't have permission to edit this story."));
    }

    // ── A cover call never overlaps a save or a status change (review fixes, 2026-10-13) ───────────

    private static IElement SubmitButton(IRenderedComponent<StoryEditorPage> cut) =>
        cut.Find("button[type=submit]");

    [Fact]
    public async Task EditMode_WhileACoverUploads_SaveAndStatusChanges_AreDisabled_AndASaveDoesNotStart()
    {
        // The pick-then-Save habit of the old flow: Save's forceLoad reload would abort the upload still
        // streaming, and on a circuit both calls would share one scoped write context.
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(StoryStatusEnum.Draft);
        _writes.CoverGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // bUnit's UploadFiles blocks until the handler finishes, and the handler waits on the gate.
        Task pick = Task.Run(() => PickCover(cut));
        try
        {
            cut.WaitForAssertion(() => SubmitButton(cut).HasAttribute("disabled").Should().BeTrue());
            cut.FindAll("button").Single(b => b.TextContent.Trim() == "Submit for publication")
                .HasAttribute("disabled").Should().BeTrue("a status change is a second write too");

            await cut.Find("form").SubmitAsync(); // a click that beat the re-render
            await cut.InvokeAsync(() => cut.FindComponent<StoryLifecyclePanel>().Instance
                .OnTransition.InvokeAsync(StoryStatusEnum.PendingApproval));
            _writes.Calls.Should().Equal([$"Upload:{StoryId}:image/png:{CoverBytes.Length}"],
                "no save starts while the cover call runs");
            _writes.Requested.Should().BeEmpty("no status change starts while the cover call runs");
        }
        finally
        {
            _writes.CoverGate.TrySetResult();
        }
        await pick;

        cut.WaitForAssertion(() => cut.Find("img[alt='Current cover']").GetAttribute("src")
            .Should().Be(_writes.UploadedPath));
        SubmitButton(cut).HasAttribute("disabled").Should().BeFalse("the save is usable again once the cover settled");
    }

    [Fact]
    public async Task EditMode_WhileASaveRuns_CoverAndStatusControlsAreDisabled_AndNeitherWriteStarts()
    {
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(StoryStatusEnum.Draft, cover: OldCover);
        _writes.SaveGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task save = cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() => cut.Find("#story-cover-art").HasAttribute("disabled").Should().BeTrue());
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Remove cover")
            .HasAttribute("disabled").Should().BeTrue();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Submit for publication")
            .HasAttribute("disabled").Should().BeTrue();

        PickCover(cut); // events that beat the re-render
        await cut.InvokeAsync(() => cut.FindComponent<StoryPropertiesForm>().Instance.OnRemoveCover.InvokeAsync());
        await cut.InvokeAsync(() => cut.FindComponent<StoryLifecyclePanel>().Instance
            .OnTransition.InvokeAsync(StoryStatusEnum.PendingApproval));
        _writes.Calls.Should().Equal(["Update"], "no cover call starts under a save");
        _writes.Requested.Should().BeEmpty("no status change starts under a save");

        _writes.SaveGate.SetResult();
        await save;
    }

    [Fact]
    public async Task EditMode_WhileAStatusChangeRuns_CoverControlsAndSaveAreDisabled_AndNeitherWriteStarts()
    {
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(StoryStatusEnum.Draft, cover: OldCover);
        _writes.Result = StoryStatusEnum.PendingApproval;
        _writes.TransitionGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task transition = ClickAsync(cut, "Submit for publication");

        cut.WaitForAssertion(() => cut.Find("#story-cover-art").HasAttribute("disabled").Should().BeTrue());
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Remove cover")
            .HasAttribute("disabled").Should().BeTrue();
        SubmitButton(cut).HasAttribute("disabled").Should().BeTrue();

        PickCover(cut);
        await cut.InvokeAsync(() => cut.FindComponent<StoryPropertiesForm>().Instance.OnRemoveCover.InvokeAsync());
        await cut.Find("form").SubmitAsync();
        _writes.Calls.Should().BeEmpty("no cover call and no save starts under a status change");

        _writes.TransitionGate.SetResult();
        await transition;
        cut.WaitForAssertion(() => cut.Find("#story-cover-art").HasAttribute("disabled").Should().BeFalse());
        SubmitButton(cut).HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task CreateMode_APickedCover_WaitsForTheCreate_ThenUploadsToTheNewStory_WithNoUpdate()
    {
        IRenderedComponent<StoryEditorPage> cut = Render<StoryEditorPage>();

        PickCover(cut);
        _writes.Calls.Should().BeEmpty("a new story has no id to attach a cover to yet");

        await cut.Find("#story-title").ChangeAsync(new() { Value = "A brand new story" });
        await cut.Find("#story-short-description").ChangeAsync(new() { Value = "A short description" });
        await cut.Find("form").SubmitAsync();

        _writes.Calls.Should().Equal(
            ["Create", $"Upload:{_writes.CreatedStoryId}:image/png:{CoverBytes.Length}"],
            "UploadCoverArtAsync persists the cover, so the old second UpdateStoryAsync is gone (owner ruling D14)");
    }

    [Fact]
    public async Task CreateMode_WithNoCoverPicked_CreatesOnly()
    {
        IRenderedComponent<StoryEditorPage> cut = Render<StoryEditorPage>();

        await cut.Find("#story-title").ChangeAsync(new() { Value = "A brand new story" });
        await cut.Find("#story-short-description").ChangeAsync(new() { Value = "A short description" });
        await cut.Find("form").SubmitAsync();

        _writes.Calls.Should().Equal(["Create"]);
    }

    // ── Author story delete (owner ruling D15, WU-AuthorStoryDelete 2026-10-13) ─────────────────────

    private const int AuthorUserId = 5;
    private const string SavedTitle = "Lifecycle Story"; // RenderEditor's StoryEditDTO.Title

    private static Task OpenDeleteAsync(IRenderedComponent<StoryEditorPage> cut) =>
        cut.Find("button[aria-label='Delete this story']").ClickAsync(new());

    private static Task TypeConfirmationAsync(IRenderedComponent<StoryEditorPage> cut, string text) =>
        cut.Find("#story-delete-confirm").InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = text });

    private static IElement ConfirmDelete(IRenderedComponent<StoryEditorPage> cut) =>
        cut.Find("button[aria-label='Delete story permanently']");

    private void SignInAsTheAuthor() =>
        this.AddAuthorization().SetAuthorized("Author").SetClaims(
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, AuthorUserId.ToString()));

    [Fact]
    public async Task Delete_ConfirmsAgainstTheSavedTitle_NotAnUnsavedEdit_ThenDeletes_ClearsTheDraft_AndLeavesForTheAuthorsStories()
    {
        SignInAsTheAuthor();
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(StoryStatusEnum.Draft);
        await cut.Find("#story-title").ChangeAsync(new() { Value = "An unsaved new title" });

        await OpenDeleteAsync(cut);
        await TypeConfirmationAsync(cut, "An unsaved new title");
        ConfirmDelete(cut).HasAttribute("disabled").Should().BeTrue(
            "the confirmation string is the saved title; an unsaved edit must not become it");

        await TypeConfirmationAsync(cut, SavedTitle);
        await ConfirmDelete(cut).ClickAsync(new());

        _writes.Calls.Should().Equal([$"Delete:{StoryId}"]);
        JSInterop.Invocations.Should().Contain(i => i.Identifier == "canalaveDraft.clear"
            && (string?)i.Arguments[0] == $"draft:story:{StoryId}",
            "the device-local backup must not outlive the story");
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri
            .Should().EndWith($"/user/{AuthorUserId}/authored");
        Services.GetRequiredService<BunitNavigationManager>().History.First().Options.ForceLoad.Should().BeTrue(
            "a full document load: this page hosts Quill, and an in-place re-render crashes the WASM renderer");
    }

    [Fact]
    public async Task Delete_WithNoIdentityCascade_LeavesForTheHomePage()
    {
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(StoryStatusEnum.Draft);

        await OpenDeleteAsync(cut);
        await TypeConfirmationAsync(cut, SavedTitle);
        await ConfirmDelete(cut).ClickAsync(new());

        _writes.Calls.Should().Equal([$"Delete:{StoryId}"]);
        new Uri(Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri)
            .AbsolutePath.Should().Be("/");
    }

    [Fact]
    public async Task ARefusedDelete_ShowsTheMessageInTheDialog_AndStaysOnThePage()
    {
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(StoryStatusEnum.InProgress);
        string before = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri;
        _writes.DeleteFailure = new UnauthorizedAccessException("You can only delete your own stories.");

        await OpenDeleteAsync(cut);
        await TypeConfirmationAsync(cut, SavedTitle);
        await ConfirmDelete(cut).ClickAsync(new());

        cut.Find("[role=dialog]").TextContent.Should().Contain(ExceptionPresenter.PermissionMessage);
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri.Should().Be(before);
        JSInterop.Invocations.Should().NotContain(i => i.Identifier == "canalaveDraft.clear",
            "the story still exists, so its backup stays");
        ConfirmDelete(cut).HasAttribute("disabled").Should().BeFalse("the author may try again");
    }

    [Fact]
    public async Task UnpublishInstead_ClosesTheDialog_AndRunsTheUnpublishInPlace_WithNoDelete()
    {
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(StoryStatusEnum.InProgress);
        _writes.Result = StoryStatusEnum.Draft;

        await OpenDeleteAsync(cut);
        await cut.Find("button[aria-label='Unpublish this story instead of deleting it']").ClickAsync(new());

        _writes.Requested.Should().Equal([StoryStatusEnum.Draft], "D1's published → Draft, the reversible out");
        _writes.Calls.Should().BeEmpty("no delete and no reload-triggering save");
        cut.FindAll("[role=dialog]").Should().BeEmpty();
        ShownStatus(cut).Should().Be(StoryDisplayFormat.StatusLabel(StoryStatusEnum.Draft));
        cut.Markup.Should().Contain("Unpublished — the story is a draft again.");
    }

    [Theory]
    [InlineData(StoryStatusEnum.Draft)]
    [InlineData(StoryStatusEnum.PendingApproval)]
    public async Task AnUnpublishedStory_IsOfferedNoUnpublishOut(StoryStatusEnum status)
    {
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(status);

        await OpenDeleteAsync(cut);

        cut.FindAll("button[aria-label='Unpublish this story instead of deleting it']").Should().BeEmpty();
    }

    [Fact]
    public async Task WhileADeleteRuns_TheOtherWritesAreDisabled_AndNoneStarts()
    {
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(StoryStatusEnum.Draft, cover: OldCover);
        _writes.DeleteGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await OpenDeleteAsync(cut);
        await TypeConfirmationAsync(cut, SavedTitle);

        Task delete = ConfirmDelete(cut).ClickAsync(new());
        try
        {
            cut.WaitForAssertion(() => SubmitButton(cut).HasAttribute("disabled").Should().BeTrue());
            cut.Find("#story-cover-art").HasAttribute("disabled").Should().BeTrue();
            cut.FindAll("button").Single(b => b.TextContent.Trim() == "Submit for publication")
                .HasAttribute("disabled").Should().BeTrue();

            // Events that beat the re-render. The second delete is not awaited here: if it started, it would
            // wait on the same gate, so the assertion below must see it before the gate opens.
            await cut.Find("form").SubmitAsync();
            await cut.InvokeAsync(() => cut.FindComponent<StoryPropertiesForm>().Instance.OnRemoveCover.InvokeAsync());
            await cut.InvokeAsync(() => cut.FindComponent<StoryLifecyclePanel>().Instance
                .OnTransition.InvokeAsync(StoryStatusEnum.PendingApproval));
            Task secondDelete = cut.InvokeAsync(() => cut.FindComponent<StoryDeleteDialog>().Instance.OnConfirmDelete.InvokeAsync());
            _writes.Calls.Should().Equal([$"Delete:{StoryId}"], "no save, cover call or second delete under a delete");
            _writes.Requested.Should().BeEmpty();
            _writes.DeleteGate.TrySetResult();
            await secondDelete;
        }
        finally
        {
            _writes.DeleteGate.TrySetResult();
        }
        await delete;
    }

    [Fact]
    public async Task WhileASaveRuns_TheDeleteOpenerIsDisabled_AndADeleteDoesNotStart()
    {
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(StoryStatusEnum.Draft);
        _writes.SaveGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task save = cut.Find("form").SubmitAsync();
        try
        {
            cut.WaitForAssertion(() => cut.Find("button[aria-label='Delete this story']")
                .HasAttribute("disabled").Should().BeTrue());
            await cut.InvokeAsync(() => cut.FindComponent<StoryDeleteDialog>().Instance.OnConfirmDelete.InvokeAsync());
            _writes.Calls.Should().Equal(["Update"], "no delete starts under a save");
        }
        finally
        {
            _writes.SaveGate.TrySetResult();
        }
        await save;
    }

    [Fact]
    public async Task ATakenDownStory_OpensDeleteOnly_AndItsDeleteLeavesForTheAuthorsStories()
    {
        // Review fixes (2026-10-13), owner ruling D15: a takedown never blocks the author's delete, and the
        // delete lives on this page. The edit read reaches the author with IsTakenDown set, and the page
        // shows a notice and the Delete story section — no form, no lifecycle panel (the status is frozen),
        // no chapter panels — and the dialog offers neither out.
        SignInAsTheAuthor();
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(StoryStatusEnum.InProgress, isTakenDown: true);

        cut.Markup.Should().Contain("A moderator has taken this story down");
        cut.FindAll("form").Should().BeEmpty("a taken-down story's properties are not edited here");
        cut.FindAll("[data-testid=lifecycle-status]").Should().BeEmpty();
        cut.FindComponents<Stub<ChapterManagerPanel>>().Should().BeEmpty();
        cut.FindComponents<DraftAutosave>().Should().BeEmpty();

        await OpenDeleteAsync(cut);
        cut.FindAll("button[aria-label='Unpublish this story instead of deleting it']").Should().BeEmpty(
            "a published but taken-down story's status is frozen");
        cut.FindAll("a[download]").Should().BeEmpty("its export 404s");
        await TypeConfirmationAsync(cut, SavedTitle);
        await ConfirmDelete(cut).ClickAsync(new());

        _writes.Calls.Should().Equal([$"Delete:{StoryId}"]);
        BunitNavigationManager nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.Uri.Should().EndWith($"/user/{AuthorUserId}/authored");
        nav.History.First().Options.ForceLoad.Should().BeTrue();
    }

    [Fact]
    public void TheCreateRoute_OnAReusedInstance_NeverShowsTheTakedownState()
    {
        // The router reuses this instance between /story/{id}/edit and /story/new without re-running
        // OnInitializedAsync (tracker D18), so the taken-down flag survives the navigation; the create
        // route must still show the form, never the takedown notice.
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(StoryStatusEnum.InProgress, isTakenDown: true);

        cut.Render(p => p.Add(c => c.StoryId, (int?)null));

        cut.Markup.Should().NotContain("A moderator has taken this story down");
        cut.FindAll("form").Should().NotBeEmpty();
        cut.FindAll("button[aria-label='Delete this story']").Should().BeEmpty();
    }

    [Fact]
    public void ALiveStory_ShowsNoTakedownNotice()
    {
        IRenderedComponent<StoryEditorPage> cut = RenderEditor(StoryStatusEnum.InProgress);

        cut.Markup.Should().NotContain("A moderator has taken this story down");
        cut.FindAll("form").Should().NotBeEmpty();
    }

    [Fact]
    public void CreateMode_HasNoDeleteSection()
    {
        IRenderedComponent<StoryEditorPage> cut = Render<StoryEditorPage>();

        cut.FindAll("button[aria-label='Delete this story']").Should().BeEmpty();
        cut.FindComponents<StoryDeleteDialog>().Should().BeEmpty();
    }

    // ── Fakes ─────────────────────────────────────────────────────────────────────

    /// <summary>Records each requested target and answers with <see cref="Result"/> (the status the
    /// server says the story landed on) or throws <see cref="Failure"/>. Since WU-BlobCleanup it also
    /// records the create/update/cover calls in order (<see cref="Calls"/>), so a test can prove which
    /// call the page made and which it did not.</summary>
    private sealed class ScriptedStoryWriteService : IStoryWriteService
    {
        public StoryStatusEnum Result { get; set; }
        public Exception? Failure { get; set; }
        public Exception? SaveFailure { get; set; }
        public Exception? CoverFailure { get; set; }

        /// <summary>When set, the matching call waits on it, so a test can act while that write is in
        /// flight (the overlap tests).</summary>
        public TaskCompletionSource? CoverGate { get; set; }
        public TaskCompletionSource? SaveGate { get; set; }
        public TaskCompletionSource? TransitionGate { get; set; }

        public List<StoryStatusEnum> Requested { get; } = [];
        public int CreatedStoryId { get; set; } = 42;
        public string UploadedPath { get; set; } = "/uploads/stories/7/cover-22222222-2222-2222-2222-222222222222.png";

        /// <summary>"Create", "Update", "Upload:{storyId}:{contentType}:{bytes}" and "Remove:{storyId}",
        /// in call order.</summary>
        public List<string> Calls { get; } = [];

        public async Task<StoryStatusEnum> TransitionStatusAsync(int storyId, StoryStatusEnum targetStatus)
        {
            Requested.Add(targetStatus);
            if (TransitionGate is not null) await TransitionGate.Task;
            if (Failure is not null) throw Failure;
            return Result;
        }

        public Task<int> CreateStoryAsync(CreateStoryDTO dto)
        {
            Calls.Add("Create");
            return Task.FromResult(CreatedStoryId);
        }

        public async Task UpdateStoryAsync(StoryUpdateDTO dto)
        {
            Calls.Add("Update");
            if (SaveGate is not null) await SaveGate.Task;
            if (SaveFailure is not null) throw SaveFailure;
        }

        public async Task<string> UploadCoverArtAsync(Stream content, string contentType, int storyId)
        {
            using MemoryStream copy = new();
            await content.CopyToAsync(copy);
            Calls.Add($"Upload:{storyId}:{contentType}:{copy.Length}");
            if (CoverGate is not null) await CoverGate.Task;
            if (CoverFailure is not null) throw CoverFailure;
            return UploadedPath;
        }

        public Task RemoveCoverArtAsync(int storyId)
        {
            Calls.Add($"Remove:{storyId}");
            return CoverFailure is null ? Task.CompletedTask : Task.FromException(CoverFailure);
        }

        public Exception? DeleteFailure { get; set; }
        public TaskCompletionSource? DeleteGate { get; set; }

        /// <summary>Records "Delete:{storyId}".</summary>
        public async Task DeleteStoryAsync(int storyId)
        {
            Calls.Add($"Delete:{storyId}");
            if (DeleteGate is not null) await DeleteGate.Task;
            if (DeleteFailure is not null) throw DeleteFailure;
        }
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
