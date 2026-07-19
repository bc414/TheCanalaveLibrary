using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render + interaction tests for <see cref="CanalaveTypeahead{TItem}"/> — the in-house typeahead
/// minted in the Global Flip wave to replace the archived Blazored.Typeahead (whose
/// programmatic-Value-clear bug crashed the WASM renderer; Blazored/Typeahead#221). Unlike its
/// predecessor, the component is 100% Blazor-managed DOM with no JS interop, so the full
/// search→select path IS bUnit-drivable — the coverage gap TagSelectorTests historically
/// documented ("adding a tag via the typeahead requires JavaScript simulation") closes here.
/// </summary>
public class CanalaveTypeaheadTests : BunitContext
{
    private static IRenderedComponent<CanalaveTypeahead<string>> RenderTypeahead(
        BunitContext ctx,
        Func<string, Task<IEnumerable<string>>> search,
        Action<string> onSelected) =>
        ctx.Render<CanalaveTypeahead<string>>(p => p
            .Add(c => c.SearchMethod, search)
            .Add(c => c.OnSelected, onSelected)
            .Add(c => c.DebounceMilliseconds, 0) // no debounce inside tests — see CanalaveTypeahead's
                                                  // HandleInputAsync: DebounceMilliseconds<=0 skips
                                                  // Task.Delay entirely, so SearchMethod's already-
                                                  // completed Task.FromResult(...) awaits synchronously,
                                                  // eliminating a real timer-hop that raced under heavy
                                                  // parallel-test-host load (was 1ms; a genuine, if
                                                  // tiny, async gap)
            .Add(c => c.MinimumLength, 2)
            .Add(c => c.ResultTemplate, item => b => b.AddContent(0, item)));

    [Fact]
    public void BelowMinimumLength_NoDropdown()
    {
        var cut = RenderTypeahead(this, _ => Task.FromResult<IEnumerable<string>>(["alpha"]), _ => { });

        cut.Find("input").Input("a"); // 1 char < MinimumLength 2

        cut.FindAll("button").Should().BeEmpty();
    }

    [Fact]
    public void Search_RendersResults()
    {
        var cut = RenderTypeahead(
            this, _ => Task.FromResult<IEnumerable<string>>(["alpha", "beta"]), _ => { });

        cut.Find("input").Input("al");

        cut.WaitForAssertion(() => cut.FindAll("button").Count.Should().Be(2));
        cut.Markup.Should().Contain("alpha").And.Contain("beta");
    }

    [Fact]
    public void Search_NoResults_ShowsNotFoundText()
    {
        var cut = RenderTypeahead(this, _ => Task.FromResult<IEnumerable<string>>([]), _ => { });

        cut.Find("input").Input("zz");

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("No results found"));
    }

    [Fact]
    public void MouseDownOnResult_FiresOnSelected_AndClearsInput()
    {
        string? selected = null;
        var cut = RenderTypeahead(
            this, _ => Task.FromResult<IEnumerable<string>>(["alpha", "beta"]), s => selected = s);

        cut.Find("input").Input("al");
        cut.WaitForAssertion(() => cut.FindAll("button").Count.Should().Be(2));

        cut.FindAll("button")[1].MouseDown();

        selected.Should().Be("beta");
        cut.Find("input").GetAttribute("value").Should().BeNullOrEmpty(); // pick clears the term
        cut.FindAll("button").Should().BeEmpty();                         // dropdown closed
    }

    [Fact]
    public void EnterKey_SelectsHighlighted_ArrowsMoveHighlight()
    {
        string? selected = null;
        var cut = RenderTypeahead(
            this, _ => Task.FromResult<IEnumerable<string>>(["alpha", "beta"]), s => selected = s);

        cut.Find("input").Input("al");
        cut.WaitForAssertion(() => cut.FindAll("button").Count.Should().Be(2));

        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        selected.Should().Be("beta"); // highlight moved from 0 to 1 before Enter
    }

    [Fact]
    public void Escape_ClosesDropdown_WithoutSelecting()
    {
        string? selected = null;
        var cut = RenderTypeahead(
            this, _ => Task.FromResult<IEnumerable<string>>(["alpha"]), s => selected = s);

        cut.Find("input").Input("al");
        cut.WaitForAssertion(() => cut.FindAll("button").Count.Should().Be(1));

        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        selected.Should().BeNull();
        cut.FindAll("button").Should().BeEmpty();
    }

    [Fact]
    public void FocusOut_ClosesDropdown()
    {
        var cut = RenderTypeahead(this, _ => Task.FromResult<IEnumerable<string>>(["alpha"]), _ => { });

        cut.Find("input").Input("al");
        cut.WaitForAssertion(() => cut.FindAll("button").Count.Should().Be(1));

        cut.Find("div.relative").FocusOut();

        cut.FindAll("button").Should().BeEmpty();
    }
}
