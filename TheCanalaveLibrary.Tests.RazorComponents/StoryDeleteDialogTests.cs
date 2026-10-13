using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// <see cref="StoryDeleteDialog"/> — the author story delete's confirm (owner ruling D15,
/// WU-AuthorStoryDelete 2026-10-13). D15's RazorComponents test: the destructive action stays disabled
/// until the typed title matches, and the dialog offers its two outs (Unpublish instead, a download)
/// first. Also: the match is exact (case-sensitive; whitespace runs collapsed, since the label shows the
/// title collapsed — review fixes, 2026-10-13), a busy dialog refuses every action, the typed text clears
/// on reopen, a backdrop click is a Cancel, the page's error list shows inside the dialog, and a taken-down
/// story offers neither out (review fixes). Buttons are found by <c>aria-label</c> (testing.md). The page's handlers are covered by
/// <see cref="StoryEditorPageTests"/>; the delete itself by Integration <c>StoryDeletionTests</c>.
/// Tier: RazorComponents (bUnit).
/// </summary>
public class StoryDeleteDialogTests : BunitContext
{
    private const int StoryId = 7;
    private const string Title = "The Lighthouse Keeper";

    private int _confirms;
    private int _unpublishes;
    private int _cancels;
    private readonly List<bool> _openChanges = [];

    private IRenderedComponent<StoryDeleteDialog> RenderDialog(
        bool canUnpublish = true, bool isBusy = false, IReadOnlyList<string>? errors = null,
        string title = Title, bool isTakenDown = false) =>
        Render<StoryDeleteDialog>(p =>
        {
            p.Add(c => c.IsOpen, true);
            p.Add(c => c.IsOpenChanged, EventCallback.Factory.Create<bool>(this, open => _openChanges.Add(open)));
            p.Add(c => c.StoryId, StoryId);
            p.Add(c => c.StoryTitle, title);
            p.Add(c => c.CanUnpublish, canUnpublish);
            p.Add(c => c.IsTakenDown, isTakenDown);
            p.Add(c => c.IsBusy, isBusy);
            p.Add(c => c.ErrorMessages, errors);
            p.Add(c => c.OnConfirmDelete, EventCallback.Factory.Create(this, () => _confirms++));
            p.Add(c => c.OnUnpublish, EventCallback.Factory.Create(this, () => _unpublishes++));
            p.Add(c => c.OnCancel, EventCallback.Factory.Create(this, () => _cancels++));
        });

    private static IElement ByLabel(IRenderedComponent<StoryDeleteDialog> cut, string ariaLabel) =>
        cut.Find($"button[aria-label='{ariaLabel}']");

    private static IElement Confirm(IRenderedComponent<StoryDeleteDialog> cut) =>
        ByLabel(cut, "Delete story permanently");

    private static Task TypeAsync(IRenderedComponent<StoryDeleteDialog> cut, string text) =>
        cut.Find("#story-delete-confirm").InputAsync(new ChangeEventArgs { Value = text });

    [Theory]
    [InlineData("")]
    [InlineData("The Lighthouse")]                 // a prefix
    [InlineData("the lighthouse keeper")]          // case differs: the match is ordinal
    [InlineData("The Lighthouse Keeper!")]
    [InlineData("TheLighthouse Keeper")]           // whitespace is collapsed, never removed
    public async Task Confirm_IsDisabled_UntilTheTypedTitleMatches(string typed)
    {
        IRenderedComponent<StoryDeleteDialog> cut = RenderDialog();
        Confirm(cut).HasAttribute("disabled").Should().BeTrue("nothing is typed yet");

        await TypeAsync(cut, typed);

        Confirm(cut).HasAttribute("disabled").Should().BeTrue($"\"{typed}\" is not the title");
        await Confirm(cut).ClickAsync(new());
        _confirms.Should().Be(0);
    }

    [Theory]
    [InlineData(Title)]
    [InlineData("  The Lighthouse Keeper  ")] // surrounding whitespace is trimmed
    public async Task Confirm_EnablesOnAnExactMatch_AndRaisesOnConfirmDelete(string typed)
    {
        IRenderedComponent<StoryDeleteDialog> cut = RenderDialog();

        await TypeAsync(cut, typed);

        Confirm(cut).HasAttribute("disabled").Should().BeFalse();
        await Confirm(cut).ClickAsync(new());
        _confirms.Should().Be(1);
        _cancels.Should().Be(0);
    }

    [Theory]
    [InlineData("The  Lighthouse Keeper", "The Lighthouse Keeper")]       // a saved double space
    [InlineData("The\tLighthouse Keeper", "The Lighthouse Keeper")]       // a saved tab
    [InlineData("The\u00A0Lighthouse Keeper", "The Lighthouse Keeper")]   // a saved non-breaking space
    [InlineData("The Lighthouse Keeper", "The   Lighthouse  Keeper")]     // extra spaces typed
    public async Task Confirm_MatchesWhatTheLabelShows_WhitespaceRunsCollapsed(string savedTitle, string typed)
    {
        // Review fixes (2026-10-13): titles are stored as typed, and the label renders the saved title in
        // normal flow, which shows any whitespace run as one space. An author who types what they see must
        // not be locked out of the delete.
        IRenderedComponent<StoryDeleteDialog> cut = RenderDialog(title: savedTitle);

        await TypeAsync(cut, typed);

        Confirm(cut).HasAttribute("disabled").Should().BeFalse($"\"{typed}\" is what the label shows for \"{savedTitle}\"");
        await Confirm(cut).ClickAsync(new());
        _confirms.Should().Be(1);
    }

    [Fact]
    public async Task ATakenDownStory_OffersNeitherOut_ButStillDeletes()
    {
        // Review fixes (2026-10-13): a taken-down story's status is frozen (no unpublish) and its export
        // 404s (no download), so one line says so in their place; the delete itself is unchanged (D15).
        IRenderedComponent<StoryDeleteDialog> cut = RenderDialog(canUnpublish: true, isTakenDown: true);

        cut.FindAll("button[aria-label='Unpublish this story instead of deleting it']").Should().BeEmpty();
        cut.FindAll("a[download]").Should().BeEmpty("the export 404s for a taken-down story");
        cut.Find("[data-testid=story-delete-taken-down]").TextContent
            .Should().Contain("taken this story down").And.Contain("can't be unpublished or downloaded");

        await TypeAsync(cut, Title);
        await Confirm(cut).ClickAsync(new());
        _confirms.Should().Be(1);
    }

    [Fact]
    public void Copy_StatesIrreversibility_AndNamesWhatGoesWithTheStory()
    {
        IRenderedComponent<StoryDeleteDialog> cut = RenderDialog();

        cut.Find("[role=dialog]").GetAttribute("aria-label").Should().Be("Delete this story permanently?");
        cut.Find("[data-testid=story-delete-warning]").TextContent.Should().Contain(Title).And.Contain("can't be undone");
        // D15: comments, recommendations and readers' progress, named in the copy.
        cut.Markup.Should().Contain("every comment and reply").And.Contain("recommendations of it")
            .And.Contain("reading progress");
        cut.Find("label[for=story-delete-confirm]").TextContent.Should().Contain(Title,
            "the title to type is shown beside the input");
    }

    [Fact]
    public async Task APublishedStory_OffersUnpublishInstead_WhichRaisesOnUnpublish()
    {
        IRenderedComponent<StoryDeleteDialog> cut = RenderDialog(canUnpublish: true);

        await ByLabel(cut, "Unpublish this story instead of deleting it").ClickAsync(new());

        _unpublishes.Should().Be(1);
        _confirms.Should().Be(0);
    }

    [Fact]
    public void AnUnpublishedStory_HasNoUnpublishOut()
    {
        IRenderedComponent<StoryDeleteDialog> cut = RenderDialog(canUnpublish: false);

        cut.FindAll("button[aria-label='Unpublish this story instead of deleting it']").Should().BeEmpty();
    }

    [Fact]
    public void TheDownloadOut_LinksEveryExportFormat_ForThisStory_AndSaysPublishedChaptersOnly()
    {
        IRenderedComponent<StoryDeleteDialog> cut = RenderDialog(canUnpublish: false);

        List<string?> hrefs = cut.FindAll("a[download]").Select(a => a.GetAttribute("href")).ToList();
        hrefs.Should().NotBeEmpty().And.OnlyContain(h => h != null && h.StartsWith($"/api/stories/{StoryId}/export/"));
        hrefs.Should().Contain($"/api/stories/{StoryId}/export/epub");
        cut.Markup.Should().Contain("only the chapters that are published",
            "the export takes published chapters only (decision row 27 (d)); the copy must not promise more");
    }

    [Fact]
    public async Task WhileBusy_EveryActionIsDisabled_AndNoneRaises()
    {
        IRenderedComponent<StoryDeleteDialog> cut = RenderDialog(isBusy: true);
        await TypeAsync(cut, Title);

        Confirm(cut).HasAttribute("disabled").Should().BeTrue("a delete is already in flight");
        ByLabel(cut, "Cancel and keep the story").HasAttribute("disabled").Should().BeTrue();
        ByLabel(cut, "Unpublish this story instead of deleting it").HasAttribute("disabled").Should().BeTrue();
        cut.Find("#story-delete-confirm").HasAttribute("disabled").Should().BeTrue();

        // Events that beat the re-render.
        await Confirm(cut).ClickAsync(new());
        await ByLabel(cut, "Cancel and keep the story").ClickAsync(new());
        await ByLabel(cut, "Unpublish this story instead of deleting it").ClickAsync(new());
        await cut.Find("[role=dialog]").ParentElement!.ClickAsync(new()); // the backdrop
        (_confirms, _cancels, _unpublishes).Should().Be((0, 0, 0));
        _openChanges.Should().BeEmpty("a busy dialog never closes itself");
    }

    [Fact]
    public async Task Cancel_RaisesOnCancel_AndAsksToClose()
    {
        IRenderedComponent<StoryDeleteDialog> cut = RenderDialog();

        await ByLabel(cut, "Cancel and keep the story").ClickAsync(new());

        _cancels.Should().Be(1);
        _openChanges.Should().Equal(false);
    }

    [Fact]
    public async Task ABackdropClick_IsACancel()
    {
        IRenderedComponent<StoryDeleteDialog> cut = RenderDialog();

        await cut.Find("[role=dialog]").ParentElement!.ClickAsync(new());

        _cancels.Should().Be(1);
        _openChanges.Should().Equal(false);
        _confirms.Should().Be(0);
    }

    [Fact]
    public async Task Reopening_ClearsTheTypedTitle()
    {
        IRenderedComponent<StoryDeleteDialog> cut = RenderDialog();
        await TypeAsync(cut, Title);
        Confirm(cut).HasAttribute("disabled").Should().BeFalse();

        cut.Render(p => p.Add(c => c.IsOpen, false));
        cut.Render(p => p.Add(c => c.IsOpen, true));

        cut.Find("#story-delete-confirm").GetAttribute("value").Should().BeNullOrEmpty();
        Confirm(cut).HasAttribute("disabled").Should().BeTrue("each opening asks for the title again");
    }

    [Fact]
    public void ThePagesErrors_ShowInsideTheDialog()
    {
        IRenderedComponent<StoryDeleteDialog> cut = RenderDialog(errors: ["You don't have permission to do that."]);

        cut.Find("[role=dialog]").TextContent.Should().Contain("You don't have permission to do that.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")] // whitespace only: empty once collapsed
    public async Task AnEmptySavedTitle_NeverEnablesTheDelete(string savedTitle)
    {
        IRenderedComponent<StoryDeleteDialog> cut = RenderDialog(title: savedTitle);

        await TypeAsync(cut, savedTitle);

        Confirm(cut).HasAttribute("disabled").Should().BeTrue("an empty input must not match an empty title");
    }
}
