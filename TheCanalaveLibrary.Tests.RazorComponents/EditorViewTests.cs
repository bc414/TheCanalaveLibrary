using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// <see cref="EditorView"/>'s content contract: <c>Html</c> is the INITIAL content only. Quill takes
/// over the rendered markup on the first render; if a later render changed it, Blazor would diff
/// nodes Quill already detached and the renderer would die (TypeError removeChild of null — the
/// WU-StoryLifecycle browser pass, 2026-09-30, hit it on the circuit after a refused story save). bUnit
/// can't run Quill, so this pins the half it can see: the rendered markup never changes after the
/// first render, whatever the consumer does with <c>Html</c>. Rule: <c>layer5-wasm.md</c>
/// §"WASM renderer vs third-party DOM", rule 3.
/// Tier: RazorComponents (bUnit, loose JS interop).
/// </summary>
public class EditorViewTests : BunitContext
{
    public EditorViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Html_RendersAsTheInitialContent()
    {
        IRenderedComponent<EditorView> cut = Render<EditorView>(p => p.Add(c => c.Html, "<p>first draft</p>"));

        cut.Markup.Should().Contain("<p>first draft</p>");
    }

    [Fact]
    public void Html_ChangedAfterTheFirstRender_DoesNotReachTheQuillOwnedMarkup()
    {
        IRenderedComponent<EditorView> cut = Render<EditorView>(p => p.Add(c => c.Html, "<p>first draft</p>"));

        cut.Render(p => p.Add(c => c.Html, "<p>pulled back from the editor</p>"));

        cut.Markup.Should().Contain("<p>first draft</p>");
        cut.Markup.Should().NotContain("pulled back from the editor",
            "re-rendering the content Quill owns is the removeChild crash; SetHtmlAsync is the way to change it");
    }
}
