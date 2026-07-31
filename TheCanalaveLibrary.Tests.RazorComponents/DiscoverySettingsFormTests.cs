using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="DiscoverySettingsForm"/> (WU-DiscoveryOverrideUI, closes tracker
/// item B7). Parameter-driven leaf — no @inject — so these test rendering and the OnToggle
/// contract in isolation, independent of SettingsPage or the real
/// <see cref="IDiscoveryFilterSettingsService"/>. What each row's fields mean (system default vs.
/// effective value, which modes/keys are included) is the service's job — covered by
/// DiscoveryFilterSettingsServiceTests (Integration); this component just renders whatever
/// <see cref="DiscoveryFilterModeDto"/> list it's handed.
///
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class DiscoverySettingsFormTests : BunitContext
{
    private static DiscoveryFilterRowDto Row(
        string key, UserStoryInteractionTypeEnum type, bool systemDefault, bool effective, bool overridden) =>
        new(key, type, systemDefault, effective, overridden);

    private static DiscoveryFilterModeDto MakeMode(string key, string displayName, params DiscoveryFilterRowDto[] rows) =>
        new(key, displayName, rows);

    [Fact]
    public void RendersOneSectionPerMode_WithModeDisplayName()
    {
        DiscoveryFilterModeDto[] modes =
        [
            MakeMode(SiteSearchModes.SearchPage, "Search Page",
                Row(UserStoryInteractionFilters.Ignored, UserStoryInteractionTypeEnum.Ignore, true, true, false)),
            MakeMode(SiteSearchModes.AlsoFavorited, "Also Favorited",
                Row(UserStoryInteractionFilters.Ignored, UserStoryInteractionTypeEnum.Ignore, true, true, false))
        ];

        IRenderedComponent<DiscoverySettingsForm> cut = Render<DiscoverySettingsForm>(p => p
            .Add(c => c.Modes, modes));

        cut.Markup.Should().Contain("Search Page").And.Contain("Also Favorited");
    }

    [Fact]
    public void RendersOneCheckboxPerRow_CheckedStateReflectsEffectiveValue()
    {
        DiscoveryFilterModeDto[] modes =
        [
            MakeMode(SiteSearchModes.SearchPage, "Search Page",
                Row(UserStoryInteractionFilters.Ignored, UserStoryInteractionTypeEnum.Ignore, true, true, false),
                Row(UserStoryInteractionFilters.Favorited, UserStoryInteractionTypeEnum.Favorite, false, false, false))
        ];

        IRenderedComponent<DiscoverySettingsForm> cut = Render<DiscoverySettingsForm>(p => p
            .Add(c => c.Modes, modes));

        var checkboxes = cut.FindAll("input[type='checkbox']");
        checkboxes.Count.Should().Be(2);
        checkboxes[0].HasAttribute("checked").Should().BeTrue("Ignored's EffectiveValue is true");
        checkboxes[1].HasAttribute("checked").Should().BeFalse("Favorited's EffectiveValue is false");
    }

    [Fact]
    public void ReusesUserStoryInteractionFilterLabelWording()
    {
        DiscoveryFilterModeDto[] modes =
        [
            MakeMode(SiteSearchModes.SearchPage, "Search Page",
                Row(UserStoryInteractionFilters.Favorited, UserStoryInteractionTypeEnum.Favorite, false, false, false))
        ];

        IRenderedComponent<DiscoverySettingsForm> cut = Render<DiscoverySettingsForm>(p => p
            .Add(c => c.Modes, modes));

        // Same wording UserStoryInteractionFilter.LabelFor uses for the live filter panel — the
        // point of promoting LabelFor to internal rather than duplicating the switch here.
        cut.Markup.Should().Contain("Hide stories I've favorited");
    }

    [Fact]
    public async Task Toggle_RaisesOnToggle_WithSearchModeKeyAndFilterKeyAndNewValue()
    {
        (string SearchModeKey, string FilterKey, bool IsEnabled)? captured = null;
        DiscoveryFilterModeDto[] modes =
        [
            MakeMode(SiteSearchModes.SearchPage, "Search Page",
                Row(UserStoryInteractionFilters.Ignored, UserStoryInteractionTypeEnum.Ignore, true, true, false))
        ];

        IRenderedComponent<DiscoverySettingsForm> cut = Render<DiscoverySettingsForm>(p => p
            .Add(c => c.Modes, modes)
            .Add(c => c.OnToggle,
                ((string SearchModeKey, string FilterKey, bool IsEnabled) args) => captured = args));

        IElement checkbox = cut.Find("input[type='checkbox']");
        await checkbox.TriggerEventAsync("onchange",
            new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = false });

        captured.Should().NotBeNull();
        captured!.Value.SearchModeKey.Should().Be(SiteSearchModes.SearchPage);
        captured.Value.FilterKey.Should().Be(UserStoryInteractionFilters.Ignored);
        captured.Value.IsEnabled.Should().BeFalse("the checkbox was unchecked");
    }

    [Fact]
    public void Busy_DisablesAllCheckboxes()
    {
        DiscoveryFilterModeDto[] modes =
        [
            MakeMode(SiteSearchModes.SearchPage, "Search Page",
                Row(UserStoryInteractionFilters.Ignored, UserStoryInteractionTypeEnum.Ignore, true, true, false))
        ];

        IRenderedComponent<DiscoverySettingsForm> cut = Render<DiscoverySettingsForm>(p => p
            .Add(c => c.Modes, modes)
            .Add(c => c.Busy, true));

        cut.Find("input[type='checkbox']").HasAttribute("disabled").Should().BeTrue();
    }
}
