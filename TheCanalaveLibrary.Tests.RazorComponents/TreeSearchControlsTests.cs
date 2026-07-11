using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="TreeSearchControls"/> (WU44). Covers: default edge preselection,
/// toggling edges, IncludePaths auto-derivation (never a raw checkbox — see the component doc),
/// Apply disabled with zero edges selected, and the emitted <see cref="TreeSearchControlsSelection"/>.
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class TreeSearchControlsTests : BunitContext
{
    [Fact]
    public void Default_PreselectsDefaultEdgeTypes()
    {
        IRenderedComponent<TreeSearchControls> cut = Render<TreeSearchControls>();

        var checkedLabels = cut.FindAll("input[type=checkbox]:checked");
        checkedLabels.Should().HaveCount(TreeSearchControls.DefaultEdgeTypes.Count,
            "AuthoredBy + Favorite are preselected by default");
    }

    [Fact]
    public void Apply_Disabled_WhenNoEdgesSelected()
    {
        IRenderedComponent<TreeSearchControls> cut = Render<TreeSearchControls>(p => p
            .Add(c => c.InitialEdgeTypes, Array.Empty<TreeSearchEdgeType>()));

        cut.Find("button").HasAttribute("disabled").Should().BeTrue(
            "Apply must be disabled when zero connection types are selected");
    }

    [Fact]
    public void Apply_EmitsSelection_WithChosenDegreesAndSort()
    {
        TreeSearchControlsSelection? emitted = null;
        IRenderedComponent<TreeSearchControls> cut = Render<TreeSearchControls>(p => p
            .Add(c => c.InitialMaxDegrees, 3)
            .Add(c => c.InitialSort, TreeSearchSortOrder.ByDegree)
            .Add(c => c.OnApply, EventCallback.Factory.Create<TreeSearchControlsSelection>(this, s => emitted = s)));

        cut.Find("button").Click();

        emitted.Should().NotBeNull();
        emitted!.MaxDegrees.Should().Be(3);
        emitted.Sort.Should().Be(TreeSearchSortOrder.ByDegree);
        emitted.EdgeTypes.Should().BeEquivalentTo(TreeSearchControls.DefaultEdgeTypes);
    }

    [Fact]
    public void Apply_IncludePaths_TrueOnlyWhenEveryEdgeIsChainOfTrust()
    {
        TreeSearchControlsSelection? emitted = null;
        IRenderedComponent<TreeSearchControls> cut = Render<TreeSearchControls>(p => p
            .Add(c => c.InitialEdgeTypes, new[] { TreeSearchEdgeType.HiddenGem, TreeSearchEdgeType.AuthorSpotlight })
            .Add(c => c.OnApply, EventCallback.Factory.Create<TreeSearchControlsSelection>(this, s => emitted = s)));

        cut.Find("button").Click();

        emitted!.IncludePaths.Should().BeTrue("both selected edges are chain-of-trust");
    }

    [Fact]
    public void Apply_IncludePaths_FalseWhenAnyNonChainOfTrustEdgeSelected()
    {
        TreeSearchControlsSelection? emitted = null;
        IRenderedComponent<TreeSearchControls> cut = Render<TreeSearchControls>(p => p
            .Add(c => c.InitialEdgeTypes, new[] { TreeSearchEdgeType.HiddenGem, TreeSearchEdgeType.Favorite })
            .Add(c => c.OnApply, EventCallback.Factory.Create<TreeSearchControlsSelection>(this, s => emitted = s)));

        cut.Find("button").Click();

        emitted!.IncludePaths.Should().BeFalse(
            "mixing in a non-chain-of-trust edge (Favorite) must never send an invalid IncludePaths=true " +
            "(the service would throw ArgumentException)");
    }

    // Mutation sanity: unchecking the only selected edge disables Apply.
    [Fact]
    public void MutationSanity_UncheckingLastEdge_DisablesApply()
    {
        IRenderedComponent<TreeSearchControls> cut = Render<TreeSearchControls>(p => p
            .Add(c => c.InitialEdgeTypes, new[] { TreeSearchEdgeType.AuthoredBy }));

        cut.Find("button").HasAttribute("disabled").Should().BeFalse();

        cut.Find("input[type=checkbox]").Change(false);

        cut.Find("button").HasAttribute("disabled").Should().BeTrue(
            "unchecking the last selected edge must disable Apply");
    }

    // ── Async-parent-resolution race (found via browser verification, WU44) ──────────────────

    [Fact]
    public void ReSyncsFromInitialParams_BeforeUserInteracts()
    {
        // Regression test: the dispatcher (TreeSearchPage) resolves query-string-seeded
        // degrees/edges/sort inside an async OnInitializedAsync, so Blazor's synchronous first
        // render passes this component the not-yet-resolved field defaults, then a second render
        // arrives moments later with the real values. A plain OnInitialized() one-time snapshot
        // would freeze on the first (wrong) render forever — this must keep re-syncing until the
        // user actually touches a control.
        IRenderedComponent<TreeSearchControls> cut = Render<TreeSearchControls>(p => p
            .Add(c => c.InitialMaxDegrees, 2)
            .Add(c => c.InitialEdgeTypes, new[] { TreeSearchEdgeType.AuthoredBy, TreeSearchEdgeType.Favorite })
            .Add(c => c.InitialSort, TreeSearchSortOrder.Random));

        cut.Find("input[type=range]").GetAttribute("value").Should().Be("2");

        // Simulate the parent's second render (query-string resolution completing) — no user
        // interaction has happened yet, so the component must adopt the corrected values.
        cut.Render(p => p
            .Add(c => c.InitialMaxDegrees, 4)
            .Add(c => c.InitialEdgeTypes, new[] { TreeSearchEdgeType.HiddenGem })
            .Add(c => c.InitialSort, TreeSearchSortOrder.ByDegree));

        cut.Find("input[type=range]").GetAttribute("value").Should().Be("4",
            "must re-sync from Initial* on every parameter set until the user's first interaction");
        cut.FindAll("input[type=checkbox]:checked").Should().HaveCount(1);
    }

    [Fact]
    public void StopsResyncing_AfterUserInteracts_PreservingTheirEdit()
    {
        TreeSearchControlsSelection? emitted = null;
        IRenderedComponent<TreeSearchControls> cut = Render<TreeSearchControls>(p => p
            .Add(c => c.InitialMaxDegrees, 2)
            .Add(c => c.OnApply, EventCallback.Factory.Create<TreeSearchControlsSelection>(this, s => emitted = s)));

        // User interacts (unrelated checkbox toggle is enough to set the interaction flag).
        cut.Find("input[type=checkbox]").Change(false);
        cut.Find("input[type=checkbox]").Change(true);

        // A later parent re-render with a different InitialMaxDegrees (e.g. an unrelated Apply
        // round-trip) must NOT clobber what the user already set.
        cut.Render(p => p.Add(c => c.InitialMaxDegrees, 7));

        cut.Find("input[type=range]").GetAttribute("value").Should().Be("2",
            "once the user has interacted, further Initial* changes must not overwrite their edits");
    }
}
