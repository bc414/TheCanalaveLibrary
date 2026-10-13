using System.Reflection;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// <see cref="StoryChapterImport"/>'s delimiter picker (mode 4, the re-split). Found by the
/// WU-ParityAndRemaining browser pass (2026-10-13): the re-split was a synchronous service call, which
/// on WASM blocked on an HTTP request and crashed the story editor ("Cannot wait on monitors on this
/// runtime"), and nothing caught a refused re-split on either phase. The handler now awaits
/// <see cref="IContentImportService.ResplitAsync"/>, disables the picker until the answer lands, and
/// shows a refusal while keeping the drafts on screen.
/// <para>The WASM runtime's refusal itself is invisible to every tier (they run on the multi-threaded
/// runtime, where a blocking wait succeeds); the async interface is the guard, and these tests pin the
/// component's side of it. Tier: RazorComponents (bUnit).</para>
/// </summary>
public class StoryChapterImportTests : BunitContext
{
    private readonly FakeImportService _import = new();

    public StoryChapterImportTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddSingleton<IContentImportService>(_import);
        Services.AddSingleton(DispatchProxy.Create<IChapterWriteService, ChapterEditorPageTakedownTests.UnexpectedCall>());
    }

    [Fact]
    public void ChangingTheDelimiter_AwaitsTheResplit_AndShowsItsDrafts()
    {
        TaskCompletionSource<IReadOnlyList<ImportedChapterDraft>> pending = new();
        _import.Resplit = (_, _) => pending.Task;
        IRenderedComponent<StoryChapterImport> cut = RenderWithParsedDocument();

        cut.Find("#chapter-import-split-strategy").Change(nameof(SplitStrategy.ChapterTextPattern));

        // In flight: the picker is disabled and the suggested split is still on screen.
        cut.Find("#chapter-import-split-strategy").HasAttribute("disabled").Should().BeTrue();
        _import.LastStrategy.Should().Be(SplitStrategy.ChapterTextPattern);
        TitlesOnScreen(cut).Should().Equal("Part One", "Part Two");

        pending.SetResult([Draft("Chapter 1"), Draft("Chapter 2"), Draft("Chapter 3")]);

        cut.WaitForAssertion(() =>
            TitlesOnScreen(cut).Should().Equal("Chapter 1", "Chapter 2", "Chapter 3"));
        cut.Find("#chapter-import-split-strategy").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void ARefusedResplit_ShowsItsMessage_AndKeepsTheDraftsOnScreen()
    {
        _import.Resplit = (_, _) => throw new ImportException("That split would make more than 500 chapters.");
        IRenderedComponent<StoryChapterImport> cut = RenderWithParsedDocument();

        cut.Find("#chapter-import-split-strategy").Change(nameof(SplitStrategy.ChapterTextPattern));

        cut.Find("[role=alert]").TextContent.Should().Contain("That split would make more than 500 chapters.");
        TitlesOnScreen(cut).Should().Equal("Part One", "Part Two");
        cut.Find("#chapter-import-split-strategy").HasAttribute("disabled").Should().BeFalse();
    }

    private IRenderedComponent<StoryChapterImport> RenderWithParsedDocument()
    {
        IRenderedComponent<StoryChapterImport> cut = Render<StoryChapterImport>(p => p.Add(c => c.StoryId, 13));
        cut.FindAll("button").First(b => b.TextContent.Contains("Import chapters")).Click();
        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("# Part One\n\nChapter 1\n\nText.\n", "story.md"));
        cut.WaitForAssertion(() => TitlesOnScreen(cut).Should().Equal("Part One", "Part Two"));
        return cut;
    }

    private static IEnumerable<string?> TitlesOnScreen(IRenderedComponent<StoryChapterImport> cut) =>
        cut.FindAll("input[aria-label='Chapter title']").Select(i => i.GetAttribute("value"));

    private static ImportedChapterDraft Draft(string title) => new(title, "<p>Text.</p>", 1, []);

    private sealed class FakeImportService : IContentImportService
    {
        public Func<ImportParseResult, SplitStrategy, Task<IReadOnlyList<ImportedChapterDraft>>> Resplit { get; set; }
            = (_, _) => Task.FromResult<IReadOnlyList<ImportedChapterDraft>>([]);

        public SplitStrategy? LastStrategy { get; private set; }

        public Task<ImportParseResult> ParseDocumentAsync(Stream file, string fileName, ImportFormat format) =>
            Task.FromResult(new ImportParseResult(
                NormalizedHtml: "<h2>Part One</h2><p>Chapter 1</p><p>Text.</p>",
                SuggestedStrategy: SplitStrategy.TopHeading,
                AvailableStrategies: [SplitStrategy.None, SplitStrategy.TopHeading, SplitStrategy.ChapterTextPattern],
                Drafts: [Draft("Part One"), Draft("Part Two")],
                Warnings: []));

        public Task<IReadOnlyList<ImportedChapterDraft>> ResplitAsync(ImportParseResult parsed, SplitStrategy strategy)
        {
            LastStrategy = strategy;
            return Resplit(parsed, strategy);
        }

        public Task<ImportedChapterDraft> ParseSingleAsync(Stream file, string fileName, ImportFormat format) =>
            throw new NotSupportedException();

        public Task<ImportParseResult> ParseEpubAsync(Stream file) => throw new NotSupportedException();
    }
}
