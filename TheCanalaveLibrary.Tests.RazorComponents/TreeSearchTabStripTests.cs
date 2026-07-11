using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="TreeSearchTabStrip"/> (WU44). Tier: RazorComponents (bUnit).
/// </summary>
public class TreeSearchTabStripTests : BunitContext
{
    [Fact]
    public void AutomaticActive_MarksAutomaticTabSelected()
    {
        IRenderedComponent<TreeSearchTabStrip> cut = Render<TreeSearchTabStrip>(p => p
            .Add(c => c.ActiveTab, TreeSearchTab.Automatic));

        var tabs = cut.FindAll("button[role=tab]");
        tabs[0].GetAttribute("aria-selected").Should().Be("true");
        tabs[1].GetAttribute("aria-selected").Should().Be("false");
    }

    [Fact]
    public void ManualActive_MarksManualTabSelected()
    {
        IRenderedComponent<TreeSearchTabStrip> cut = Render<TreeSearchTabStrip>(p => p
            .Add(c => c.ActiveTab, TreeSearchTab.Manual));

        var tabs = cut.FindAll("button[role=tab]");
        tabs[0].GetAttribute("aria-selected").Should().Be("false");
        tabs[1].GetAttribute("aria-selected").Should().Be("true");
    }

    [Fact]
    public void ClickingManualTab_EmitsManual()
    {
        TreeSearchTab? emitted = null;
        IRenderedComponent<TreeSearchTabStrip> cut = Render<TreeSearchTabStrip>(p => p
            .Add(c => c.ActiveTab, TreeSearchTab.Automatic)
            .Add(c => c.OnTabChanged, EventCallback.Factory.Create<TreeSearchTab>(this, t => emitted = t)));

        cut.FindAll("button[role=tab]")[1].Click();

        emitted.Should().Be(TreeSearchTab.Manual);
    }
}
