using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="ChapterPropertiesForm"/> (WU6). ChapterPropertiesForm has no
/// @inject (presentational) — no DI setup needed except EditorView JS interop (Loose mode).
/// Tier: RazorComponents (bUnit).
/// </summary>
public class ChapterPropertiesFormTests : BunitContext
{
    public ChapterPropertiesFormTests()
    {
        // EditorView (Blazored.TextEditor / Quill.js) makes JS calls on render. Loose mode
        // accepts any JS invocation without throwing (no JS interpreter in bUnit).
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static ChapterEditorViewModel MakeValidViewModel() => new()
    {
        Title = "Chapter One",
        ChapterText = "<p>Once upon a time.</p>"
    };

    [Fact]
    public void Form_Renders_TitleInput()
    {
        IRenderedComponent<ChapterPropertiesForm> cut = Render<ChapterPropertiesForm>(p => p
            .Add(f => f.ViewModel, MakeValidViewModel())
            .Add(f => f.OnValidSubmit, () => { }));

        cut.Find("input[placeholder='Chapter title']").Should().NotBeNull();
    }

    // Every EditorView (Top/Bottom Author's Note, Chapter Text) renders its own Quill toolbar,
    // which includes a real <select class="ql-header"> — present regardless of IsPrimary. These
    // two tests care about the Rating picker specifically, so they exclude Quill's own selects.
    private static IEnumerable<AngleSharp.Dom.IElement> NonQuillSelects(IRenderedComponent<ChapterPropertiesForm> cut) =>
        cut.FindAll("select").Where(s => !(s.GetAttribute("class") ?? "").Contains("ql-header"));

    [Fact]
    public void Form_IsPrimary_ShowsInheritedRatingText_NoSelect()
    {
        IRenderedComponent<ChapterPropertiesForm> cut = Render<ChapterPropertiesForm>(p => p
            .Add(f => f.ViewModel, MakeValidViewModel())
            .Add(f => f.OnValidSubmit, () => { })
            .Add(f => f.IsPrimary, true));

        cut.Markup.Should().Contain("primary invariant");
        NonQuillSelects(cut).Should().BeEmpty("the primary version inherits the story's rating - no picker");
    }

    [Fact]
    public void Form_NotPrimary_ShowsRatingSelect()
    {
        IRenderedComponent<ChapterPropertiesForm> cut = Render<ChapterPropertiesForm>(p => p
            .Add(f => f.ViewModel, MakeValidViewModel())
            .Add(f => f.OnValidSubmit, () => { })
            .Add(f => f.IsPrimary, false));

        NonQuillSelects(cut).Should().NotBeEmpty();
    }

    [Fact]
    public void Form_VersionCountGreaterThanOne_ShowsVersionNameInput()
    {
        IRenderedComponent<ChapterPropertiesForm> cut = Render<ChapterPropertiesForm>(p => p
            .Add(f => f.ViewModel, MakeValidViewModel())
            .Add(f => f.OnValidSubmit, () => { })
            .Add(f => f.VersionCount, 2));

        cut.Markup.Should().Contain("Version Name");
    }

    [Fact]
    public void AllFields_HaveAccessibleNames()
    {
        // WU-A11y (Structure), 2026-07-31 — the second-densest orphan-label file in the pre-fix
        // survey (6 of 43), and the one that established the role="group"/aria-labelledby
        // composite pattern (Top/Bottom Author's Note, Chapter Text, Version Rating all wrap
        // EditorView or a conditionally-absent control). IsPrimary=false + VersionCount=2 render
        // every optional field. See AccessibleNameAssertions for what this catches beyond
        // check-a11y.ps1's static gates.
        IRenderedComponent<ChapterPropertiesForm> cut = Render<ChapterPropertiesForm>(p => p
            .Add(f => f.ViewModel, MakeValidViewModel())
            .Add(f => f.OnValidSubmit, () => { })
            .Add(f => f.IsPrimary, false)
            .Add(f => f.VersionCount, 2));

        AccessibleNameAssertions.AllFieldsHaveAccessibleNames(cut);
    }
}
