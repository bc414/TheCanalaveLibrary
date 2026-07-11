using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render/behavior tests for <see cref="ImportReviewPanel"/> (Feature 63, WU38d) — the
/// interactive review step of bulk chapter import: rename, merge-into-previous, remove,
/// reorder, warnings display, and the commit callback carrying the final drafts in display
/// order. Tier: RazorComponents (bUnit; the panel is injection-free by design).
/// </summary>
public class ImportReviewPanelTests : BunitContext
{
    private static ImportedChapterDraft Draft(string? title, string html = "<p>body</p>",
        int words = 10, params ImportWarning[] warnings) =>
        new(title, html, words, warnings);

    private static IReadOnlyList<ImportedChapterDraft> ThreeDrafts() =>
    [
        Draft("Front Matter", "<p>front</p>", 3),
        Draft("Chapter 1", "<p>one</p>", 100),
        Draft("Chapter 2", "<p>two</p>", 200)
    ];

    [Fact]
    public void RendersOneRowPerDraft_WithTitleAndWordCount()
    {
        IRenderedComponent<ImportReviewPanel> cut = Render<ImportReviewPanel>(p => p
            .Add(c => c.Drafts, ThreeDrafts()));

        cut.FindAll("li").Should().HaveCount(3);
        cut.Markup.Should().Contain("100 words").And.Contain("200 words");
        cut.FindAll("input[aria-label='Chapter title']")[1].GetAttribute("value").Should().Be("Chapter 1");
        cut.Markup.Should().Contain("Import 3 chapters as drafts");
    }

    [Fact]
    public void Warnings_AreShownOnTheirRow()
    {
        IRenderedComponent<ImportReviewPanel> cut = Render<ImportReviewPanel>(p => p
            .Add(c => c.Drafts, (IReadOnlyList<ImportedChapterDraft>)
            [
                Draft("Ch", warnings: new ImportWarning(ImportWarningKind.ImagesDropped, "2 images dropped"))
            ]));

        cut.Markup.Should().Contain("2 images dropped");
    }

    [Fact]
    public void Remove_DropsTheRow_AndCommitCountUpdates()
    {
        IRenderedComponent<ImportReviewPanel> cut = Render<ImportReviewPanel>(p => p
            .Add(c => c.Drafts, ThreeDrafts()));

        cut.FindAll("button").First(b => b.TextContent == "Remove").Click(); // removes "Front Matter"

        cut.FindAll("li").Should().HaveCount(2);
        cut.Markup.Should().NotContain("Front Matter");
        cut.Markup.Should().Contain("Import 2 chapters as drafts");
    }

    [Fact]
    public void MergeIntoPrevious_ConcatenatesContentAndWordCounts()
    {
        IRenderedComponent<ImportReviewPanel> cut = Render<ImportReviewPanel>(p => p
            .Add(c => c.Drafts, ThreeDrafts()));

        // Merge "Chapter 2" (row 3) into "Chapter 1" (row 2).
        cut.FindAll("button").Last(b => b.TextContent == "Merge into previous").Click();

        cut.FindAll("li").Should().HaveCount(2);
        cut.Markup.Should().Contain("300 words", "100 + 200 merged");
    }

    [Fact]
    public void MoveUp_ReordersRows()
    {
        IRenderedComponent<ImportReviewPanel> cut = Render<ImportReviewPanel>(p => p
            .Add(c => c.Drafts, ThreeDrafts()));

        // Move "Chapter 2" (last row) up one.
        cut.FindAll("button[aria-label='Move up']").Last().Click();

        var titles = cut.FindAll("input[aria-label='Chapter title']")
            .Select(i => i.GetAttribute("value")).ToList();
        titles.Should().Equal("Front Matter", "Chapter 2", "Chapter 1");
    }

    [Fact]
    public void Commit_ReturnsFinalDrafts_InDisplayOrder_WithEditedTitles()
    {
        IReadOnlyList<ImportedChapterDraft>? committed = null;
        IRenderedComponent<ImportReviewPanel> cut = Render<ImportReviewPanel>(p => p
            .Add(c => c.Drafts, ThreeDrafts())
            .Add(c => c.OnCommit, drafts => committed = drafts));

        // Drop front matter, rename Chapter 1, commit.
        cut.FindAll("button").First(b => b.TextContent == "Remove").Click();
        cut.FindAll("input[aria-label='Chapter title']")[0].Change("The Harbor, Revised");
        cut.FindAll("button").First(b => b.TextContent.StartsWith("Import")).Click();

        committed.Should().NotBeNull();
        committed!.Should().HaveCount(2);
        committed[0].Title.Should().Be("The Harbor, Revised");
        committed[1].Title.Should().Be("Chapter 2");
    }

    [Fact]
    public void Preview_TogglesRichTextRendering()
    {
        IRenderedComponent<ImportReviewPanel> cut = Render<ImportReviewPanel>(p => p
            .Add(c => c.Drafts, (IReadOnlyList<ImportedChapterDraft>)
            [
                Draft("Ch", "<p>unique-preview-text</p>")
            ]));

        cut.Markup.Should().NotContain("unique-preview-text", "preview starts collapsed");
        cut.FindAll("button").First(b => b.TextContent == "Preview").Click();
        cut.Markup.Should().Contain("unique-preview-text");
    }

    [Fact]
    public void EmptyDrafts_ShowsNothingToImport()
    {
        IRenderedComponent<ImportReviewPanel> cut = Render<ImportReviewPanel>(p => p
            .Add(c => c.Drafts, (IReadOnlyList<ImportedChapterDraft>)[]));

        cut.Markup.Should().Contain("Nothing to import");
    }
}
