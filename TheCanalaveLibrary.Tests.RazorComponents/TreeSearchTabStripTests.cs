using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="TreeSearchTabStrip"/> (WU44; three tabs since WU40).
/// Tier: RazorComponents (bUnit).
/// </summary>
public class TreeSearchTabStripTests : BunitContext
{
    /// <summary>Exactly the active tab is selected (merged from three per-tab tests, H7).</summary>
    [Theory]
    [InlineData(TreeSearchTab.Automatic, 0)]
    [InlineData(TreeSearchTab.Explore, 1)]
    [InlineData(TreeSearchTab.DeepDive, 2)]
    public void ActiveTab_IsTheOnlyTabMarkedSelected(TreeSearchTab active, int selectedIndex)
    {
        IRenderedComponent<TreeSearchTabStrip> cut = Render<TreeSearchTabStrip>(p => p
            .Add(c => c.ActiveTab, active));

        var tabs = cut.FindAll("button[role=tab]");
        tabs.Count.Should().Be(3);
        for (int i = 0; i < tabs.Count; i++)
            tabs[i].GetAttribute("aria-selected").Should().Be(i == selectedIndex ? "true" : "false");
    }

    /// <summary>Clicking a tab emits that tab (merged from two per-tab tests, H7).</summary>
    [Theory]
    [InlineData(1, TreeSearchTab.Explore)]
    [InlineData(2, TreeSearchTab.DeepDive)]
    public void ClickingATab_EmitsThatTab(int tabIndex, TreeSearchTab expected)
    {
        TreeSearchTab? emitted = null;
        IRenderedComponent<TreeSearchTabStrip> cut = Render<TreeSearchTabStrip>(p => p
            .Add(c => c.ActiveTab, TreeSearchTab.Automatic)
            .Add(c => c.OnTabChanged, EventCallback.Factory.Create<TreeSearchTab>(this, t => emitted = t)));

        cut.FindAll("button[role=tab]")[tabIndex].Click();

        emitted.Should().Be(expected);
    }
}
