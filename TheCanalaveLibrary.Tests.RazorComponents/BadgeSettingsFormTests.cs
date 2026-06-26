using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render and interaction tests for <see cref="BadgeSettingsForm"/> (Feature 50, WU36).
/// Covers: empty-state message; visible/hidden badge sections; Hide/Show toggle; move-up
/// reordering; Save callback emits ordered visible keys; Busy disables save button.
/// BadgeSettingsForm has no @inject (parameter-driven leaf) — no DI setup needed.
/// Tier: RazorComponents (bUnit).
/// </summary>
public class BadgeSettingsFormTests : TestContext
{
    // ── Empty state ───────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyBadges_ShowsNoBadgesMessage()
    {
        IRenderedComponent<BadgeSettingsForm> cut = RenderComponent<BadgeSettingsForm>(p =>
            p.Add(f => f.EarnedBadges, []));

        cut.Markup.Should().Contain("You haven't earned any badges yet.");
    }

    [Fact]
    public void EmptyBadges_DoesNotRenderSaveButton()
    {
        IRenderedComponent<BadgeSettingsForm> cut = RenderComponent<BadgeSettingsForm>(p =>
            p.Add(f => f.EarnedBadges, []));

        cut.FindAll("button[type='button']")
            .Select(b => b.TextContent.Trim())
            .Should().NotContain("Save Badge Display",
                "save button only appears when there are earned badges");
    }

    // ── Visible / hidden sections ─────────────────────────────────────────────────

    [Fact]
    public void VisibleBadge_DisplayNameAppearsInMarkup()
    {
        IRenderedComponent<BadgeSettingsForm> cut = RenderComponent<BadgeSettingsForm>(p =>
            p.Add(f => f.EarnedBadges, [MakeBadge("Recommender", displayOrder: 1)]));

        cut.Markup.Should().Contain("Recommender Name",
            "the badge's DisplayName must be rendered in the visible section");
    }

    [Fact]
    public void VisibleBadge_ShowsHideButton()
    {
        IRenderedComponent<BadgeSettingsForm> cut = RenderComponent<BadgeSettingsForm>(p =>
            p.Add(f => f.EarnedBadges, [MakeBadge("Recommender", displayOrder: 1)]));

        cut.FindAll("button[title='Hide this badge']")
            .Should().HaveCount(1, "a visible badge must have a Hide button");
    }

    [Fact]
    public void HiddenBadge_ShowsShowButton()
    {
        IRenderedComponent<BadgeSettingsForm> cut = RenderComponent<BadgeSettingsForm>(p =>
            p.Add(f => f.EarnedBadges, [MakeBadge("Recommender", displayOrder: 0)]));

        cut.FindAll("button[title='Make visible']")
            .Should().HaveCount(1, "a hidden badge (DisplayOrder = 0) must have a Show button");
    }

    [Fact]
    public void HiddenBadge_DoesNotShowHideButton()
    {
        IRenderedComponent<BadgeSettingsForm> cut = RenderComponent<BadgeSettingsForm>(p =>
            p.Add(f => f.EarnedBadges, [MakeBadge("Recommender", displayOrder: 0)]));

        cut.FindAll("button[title='Hide this badge']")
            .Should().BeEmpty("a hidden badge must not have a Hide button");
    }

    // ── Toggle interactions ───────────────────────────────────────────────────────

    [Fact]
    public async Task ClickHide_MovesVisibleBadgeToHiddenSection()
    {
        IRenderedComponent<BadgeSettingsForm> cut = RenderComponent<BadgeSettingsForm>(p =>
            p.Add(f => f.EarnedBadges, [MakeBadge("Recommender", displayOrder: 1)]));

        IElement hideButton = cut.Find("button[title='Hide this badge']");
        await hideButton.ClickAsync(new MouseEventArgs());

        // After clicking Hide, the badge must now be in the hidden section (Show button visible).
        cut.FindAll("button[title='Make visible']")
            .Should().HaveCount(1, "clicking Hide must move the badge to the hidden section");
        cut.FindAll("button[title='Hide this badge']")
            .Should().BeEmpty("the badge must no longer appear in the visible section");
    }

    [Fact]
    public async Task ClickShow_MovesHiddenBadgeToVisibleSection()
    {
        IRenderedComponent<BadgeSettingsForm> cut = RenderComponent<BadgeSettingsForm>(p =>
            p.Add(f => f.EarnedBadges, [MakeBadge("Recommender", displayOrder: 0)]));

        IElement showButton = cut.Find("button[title='Make visible']");
        await showButton.ClickAsync(new MouseEventArgs());

        // After clicking Show, the badge must now be in the visible section.
        cut.FindAll("button[title='Hide this badge']")
            .Should().HaveCount(1, "clicking Show must move the badge to the visible section");
        cut.FindAll("button[title='Make visible']")
            .Should().BeEmpty("the badge must no longer appear in the hidden section");
    }

    // ── Save callback ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveButton_EmitsOrderedVisibleKeys()
    {
        // One visible badge — the emitted list must contain exactly its key.
        IReadOnlyList<string>? emittedKeys = null;
        IRenderedComponent<BadgeSettingsForm> cut = RenderComponent<BadgeSettingsForm>(p => p
            .Add(f => f.EarnedBadges, [MakeBadge("Recommender", displayOrder: 1)])
            .Add(f => f.OnSave, EventCallback.Factory.Create<IReadOnlyList<string>>(
                this, keys => { emittedKeys = keys; })));

        await cut.Find("button[type='button']").ClickAsync(new MouseEventArgs());
        // The save button is the last button in the card; find it explicitly by content.
        IElement saveBtn = cut.FindAll("button[type='button']")
            .First(b => b.TextContent.Trim() == "Save Badge Display");
        await saveBtn.ClickAsync(new MouseEventArgs());

        emittedKeys.Should().NotBeNull("OnSave must be invoked when Save is clicked");
        emittedKeys!.Should().ContainSingle().Which.Should().Be("Recommender",
            "the emitted list must contain the visible badge key");
    }

    [Fact]
    public async Task SaveButton_HiddenBadges_NotInEmittedKeys()
    {
        // Hidden badge — Save must emit an empty visible list.
        IReadOnlyList<string>? emittedKeys = null;
        IRenderedComponent<BadgeSettingsForm> cut = RenderComponent<BadgeSettingsForm>(p => p
            .Add(f => f.EarnedBadges, [MakeBadge("Recommender", displayOrder: 0)])
            .Add(f => f.OnSave, EventCallback.Factory.Create<IReadOnlyList<string>>(
                this, keys => { emittedKeys = keys; })));

        IElement saveBtn = cut.FindAll("button[type='button']")
            .First(b => b.TextContent.Trim() == "Save Badge Display");
        await saveBtn.ClickAsync(new MouseEventArgs());

        emittedKeys.Should().NotBeNull();
        emittedKeys!.Should().BeEmpty("hidden badges must not appear in the emitted visible-keys list");
    }

    // ── Reorder ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MoveUp_SecondBadge_EmitsReversedOrder()
    {
        // Two visible badges: A at DisplayOrder 1, B at DisplayOrder 2.
        // Click ↑ on B → emitted order must be [B, A].
        IReadOnlyList<string>? emittedKeys = null;
        IRenderedComponent<BadgeSettingsForm> cut = RenderComponent<BadgeSettingsForm>(p => p
            .Add(f => f.EarnedBadges,
            [
                MakeBadge("BadgeA", displayOrder: 1, sortOrder: 1),
                MakeBadge("BadgeB", displayOrder: 2, sortOrder: 2)
            ])
            .Add(f => f.OnSave, EventCallback.Factory.Create<IReadOnlyList<string>>(
                this, keys => { emittedKeys = keys; })));

        // The ↑ buttons: first badge's ↑ is disabled; second badge's ↑ is enabled.
        IElement[] upButtons = [.. cut.FindAll("button[title='Move up']")];
        await upButtons[1].ClickAsync(new MouseEventArgs()); // B moves to position 1

        IElement saveBtn = cut.FindAll("button[type='button']")
            .First(b => b.TextContent.Trim() == "Save Badge Display");
        await saveBtn.ClickAsync(new MouseEventArgs());

        emittedKeys.Should().BeEquivalentTo(["BadgeB", "BadgeA"], options => options.WithStrictOrdering(),
            "clicking ↑ on the second badge must swap it with the first");
    }

    [Fact]
    public async Task MoveDown_FirstBadge_EmitsReversedOrder()
    {
        IReadOnlyList<string>? emittedKeys = null;
        IRenderedComponent<BadgeSettingsForm> cut = RenderComponent<BadgeSettingsForm>(p => p
            .Add(f => f.EarnedBadges,
            [
                MakeBadge("BadgeA", displayOrder: 1, sortOrder: 1),
                MakeBadge("BadgeB", displayOrder: 2, sortOrder: 2)
            ])
            .Add(f => f.OnSave, EventCallback.Factory.Create<IReadOnlyList<string>>(
                this, keys => { emittedKeys = keys; })));

        // The ↓ buttons: first badge's ↓ is enabled; second badge's ↓ is disabled.
        IElement[] downButtons = [.. cut.FindAll("button[title='Move down']")];
        await downButtons[0].ClickAsync(new MouseEventArgs()); // A moves to position 2

        IElement saveBtn = cut.FindAll("button[type='button']")
            .First(b => b.TextContent.Trim() == "Save Badge Display");
        await saveBtn.ClickAsync(new MouseEventArgs());

        emittedKeys.Should().BeEquivalentTo(["BadgeB", "BadgeA"], options => options.WithStrictOrdering(),
            "clicking ↓ on the first badge must swap it with the second");
    }

    // ── Busy state ────────────────────────────────────────────────────────────────

    [Fact]
    public void BusyTrue_SaveButtonIsDisabled()
    {
        IRenderedComponent<BadgeSettingsForm> cut = RenderComponent<BadgeSettingsForm>(p => p
            .Add(f => f.EarnedBadges, [MakeBadge("Recommender", displayOrder: 1)])
            .Add(f => f.Busy, true));

        IElement saveBtn = cut.FindAll("button[type='button']")
            .First(b => b.TextContent.Trim() == "Saving…");
        saveBtn.HasAttribute("disabled").Should().BeTrue("Busy=true must disable the save button");
    }

    [Fact]
    public void BusyTrue_SaveButtonShowsSavingText()
    {
        IRenderedComponent<BadgeSettingsForm> cut = RenderComponent<BadgeSettingsForm>(p => p
            .Add(f => f.EarnedBadges, [MakeBadge("Recommender", displayOrder: 1)])
            .Add(f => f.Busy, true));

        cut.Markup.Should().Contain("Saving…", "Busy=true must change the save button label to 'Saving…'");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static EarnedBadgeDto MakeBadge(
        string key,
        int displayOrder = 1,
        int sortOrder = 10) =>
        new(
            BadgeKey:    key,
            DisplayName: $"{key} Name",
            Description: $"{key} description",
            IconUrl:     $"/img/badges/{key}.png",
            SortOrder:   sortOrder,
            DisplayOrder: displayOrder);
}
