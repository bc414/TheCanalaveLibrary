using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="UserStoryInteractionFilter"/> (WU23). Covers:
/// - Default render: one checkbox per DefaultKinds entry.
/// - Toggling a checkbox raises OnChanged with the updated excluded set.
/// - ExcludedKinds seed: pre-checked checkboxes match the seed.
/// - AvailableKinds param restricts which checkboxes render.
///
/// No service injection needed (component is injection-free).
/// </summary>
public class UserStoryInteractionFilterTests : TestContext
{
    // ── Default render ───────────────────────────────────────────────────────────────

    [Fact]
    public void DefaultRender_RendersOneCheckboxPerDefaultKind()
    {
        IRenderedComponent<UserStoryInteractionFilter> cut =
            RenderComponent<UserStoryInteractionFilter>();

        int expectedCount = UserStoryInteractionFilter.DefaultKinds.Count;
        cut.FindAll("input[type='checkbox']").Count.Should().Be(expectedCount,
            "one checkbox per DefaultKinds entry");
    }

    [Fact]
    public void DefaultRender_NoCheckboxesAreChecked()
    {
        IRenderedComponent<UserStoryInteractionFilter> cut =
            RenderComponent<UserStoryInteractionFilter>();

        cut.FindAll("input[type='checkbox']")
            .All(cb => !cb.HasAttribute("checked"))
            .Should().BeTrue("no kinds are excluded by default");
    }

    // ── Toggle emits updated excluded list ──────────────────────────────────────────

    [Fact]
    public async Task ToggleIgnore_EmitsIgnoreInExcludedList()
    {
        IReadOnlyList<UserStoryInteractionTypeEnum>? emitted = null;
        IRenderedComponent<UserStoryInteractionFilter> cut = RenderComponent<UserStoryInteractionFilter>(p => p
            .Add(c => c.OnChanged,
                (IReadOnlyList<UserStoryInteractionTypeEnum> list) => emitted = list));

        // Click the Ignore checkbox (label text "Hide stories I've ignored").
        IElement ignoreLabel = cut.FindAll("label")
            .First(l => l.TextContent.Contains("ignored", StringComparison.OrdinalIgnoreCase));
        IElement ignoreCheckbox = ignoreLabel.QuerySelector("input[type='checkbox']")!;

        await ignoreCheckbox.TriggerEventAsync("onchange",
            new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true });

        emitted.Should().NotBeNull();
        emitted!.Should().Contain(UserStoryInteractionTypeEnum.Ignore,
            "checking the Ignore checkbox must add Ignore to the excluded list");
    }

    [Fact]
    public async Task ToggleTwoKinds_EmitsBothInExcludedList()
    {
        IReadOnlyList<UserStoryInteractionTypeEnum>? emitted = null;
        IRenderedComponent<UserStoryInteractionFilter> cut = RenderComponent<UserStoryInteractionFilter>(p => p
            .Add(c => c.OnChanged,
                (IReadOnlyList<UserStoryInteractionTypeEnum> list) => emitted = list));

        // Re-find each element immediately before triggering — avoids stale event handler IDs after re-render.
        await cut.FindAll("label")
            .First(l => l.TextContent.Contains("favorited", StringComparison.OrdinalIgnoreCase)
                        && !l.TextContent.Contains("privately", StringComparison.OrdinalIgnoreCase))
            .QuerySelector("input[type='checkbox']")!
            .TriggerEventAsync("onchange",
                new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true });

        await cut.FindAll("label")
            .First(l => l.TextContent.Contains("ignored", StringComparison.OrdinalIgnoreCase))
            .QuerySelector("input[type='checkbox']")!
            .TriggerEventAsync("onchange",
                new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true });

        emitted.Should().Contain(UserStoryInteractionTypeEnum.Favorite);
        emitted.Should().Contain(UserStoryInteractionTypeEnum.Ignore);
        emitted!.Count.Should().Be(2);
    }

    [Fact]
    public async Task UncheckKind_RemovesItFromExcludedList()
    {
        IReadOnlyList<UserStoryInteractionTypeEnum>? emitted = null;
        IRenderedComponent<UserStoryInteractionFilter> cut = RenderComponent<UserStoryInteractionFilter>(p => p
            .Add(c => c.ExcludedKinds, [UserStoryInteractionTypeEnum.Ignore])
            .Add(c => c.OnChanged,
                (IReadOnlyList<UserStoryInteractionTypeEnum> list) => emitted = list));

        IElement ignoreCheckbox = cut.FindAll("label")
            .First(l => l.TextContent.Contains("ignored", StringComparison.OrdinalIgnoreCase))
            .QuerySelector("input[type='checkbox']")!;

        // Uncheck (value = false).
        await ignoreCheckbox.TriggerEventAsync("onchange",
            new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = false });

        emitted.Should().NotBeNull();
        emitted!.Should().NotContain(UserStoryInteractionTypeEnum.Ignore,
            "unchecking Ignore must remove it from the excluded list");
    }

    // ── ExcludedKinds seed ───────────────────────────────────────────────────────────

    [Fact]
    public void ExcludedKindsSeed_PresetsCheckboxCheckedState()
    {
        IRenderedComponent<UserStoryInteractionFilter> cut = RenderComponent<UserStoryInteractionFilter>(p => p
            .Add(c => c.ExcludedKinds, [UserStoryInteractionTypeEnum.Ignore]));

        // The ignored checkbox should be checked, others should not.
        IElement ignoreCheckbox = cut.FindAll("label")
            .First(l => l.TextContent.Contains("ignored", StringComparison.OrdinalIgnoreCase))
            .QuerySelector("input[type='checkbox']")!;

        ignoreCheckbox.HasAttribute("checked").Should().BeTrue(
            "checkbox seeded by ExcludedKinds must be pre-checked");
    }

    // ── AvailableKinds restricts rendered checkboxes ─────────────────────────────────

    [Fact]
    public void AvailableKinds_RestrictedList_OnlyRendersSpecifiedKinds()
    {
        IReadOnlyList<UserStoryInteractionTypeEnum> subset =
        [
            UserStoryInteractionTypeEnum.Ignore,
            UserStoryInteractionTypeEnum.Complete
        ];

        IRenderedComponent<UserStoryInteractionFilter> cut = RenderComponent<UserStoryInteractionFilter>(p => p
            .Add(c => c.AvailableKinds, subset));

        cut.FindAll("input[type='checkbox']").Count.Should().Be(2,
            "AvailableKinds=[Ignore, Complete] must render exactly 2 checkboxes");
    }
}
