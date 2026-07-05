using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="UserStoryInteractionPanel"/> (WU16). Covers:
/// - Detail context renders all 6 buttons in the locked enum-declaration order
///   (Favorite → Private Favorite → Following → Completed → Read It Later → Ignored).
/// - Listing context renders Fav/PrivFav/Follow/Complete as read-only (leaf shows only when active).
/// - Listing context blank-slate: ReadLater + Ignore are shown and clickable.
/// - Listing context non-blank-slate: ReadLater + Ignore hidden unless already active.
/// - IsOwnStory renders an Edit link and no interaction buttons.
/// - Optimistic toggle: clicking a button flips IsActive in local state before debounce fires.
///
/// <b>Not tested here:</b> The 2-second debounce flush and service persistence (covered by
/// <c>UserStoryInteractionServiceTests</c> in the Integration tier). Tailwind visual rendering
/// requires human sign-off for Stage 6.
/// </summary>
public class UserStoryInteractionPanelTests : BunitContext
{
    private readonly FakeUserStoryInteractionWriteService _fakeService = new();

    public UserStoryInteractionPanelTests()
    {
        Services.AddScoped<IUserStoryInteractionWriteService>(_ => _fakeService);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ── Detail context — all 6 clickable buttons in locked order ────────────────

    [Fact]
    public void Detail_AllFalseState_RendersAllSixButtons()
    {
        IRenderedComponent<UserStoryInteractionPanel> cut = Render<UserStoryInteractionPanel>(p => p
            .Add(c => c.StoryId, 1)
            .Add(c => c.State, UserStoryInteractionStateDto.AllFalse(1))
            .Add(c => c.Context, UserStoryInteractionDisplayContext.Detail));

        cut.FindAll("button[aria-label]").Count.Should().Be(6,
            "all 6 interaction types are clickable in detail context");
    }

    [Fact]
    public void Detail_AllFalseState_ButtonsInLockedEnumDeclarationOrder()
    {
        IRenderedComponent<UserStoryInteractionPanel> cut = Render<UserStoryInteractionPanel>(p => p
            .Add(c => c.StoryId, 1)
            .Add(c => c.State, UserStoryInteractionStateDto.AllFalse(1))
            .Add(c => c.Context, UserStoryInteractionDisplayContext.Detail));

        // Enum declaration order = button order:
        // Favorite → PrivateFavorite → Follow → Complete → ReadLater → Ignore
        string[] expectedLabels = ["Favorite", "Private Favorite", "Following", "Completed", "Read It Later", "Ignored"];
        var buttons = cut.FindAll("button[aria-label]");
        buttons.Select(b => b.GetAttribute("aria-label")).Should().Equal(expectedLabels,
            "the panel must iterate Enum.GetValues<UserStoryInteractionTypeEnum>() which yields the locked declaration order");
    }

    [Fact]
    public void Detail_AllFalseState_NoButtonsHaveAriaPressed()
    {
        IRenderedComponent<UserStoryInteractionPanel> cut = Render<UserStoryInteractionPanel>(p => p
            .Add(c => c.StoryId, 1)
            .Add(c => c.State, UserStoryInteractionStateDto.AllFalse(1))
            .Add(c => c.Context, UserStoryInteractionDisplayContext.Detail));

        // Blazor renders aria-pressed="@bool" as a boolean HTML attribute:
        // absent when false, present when true. No button should have it when all bits are false.
        var buttons = cut.FindAll("button[aria-label]");
        buttons.All(b => !b.HasAttribute("aria-pressed")).Should().BeTrue(
            "all bits are false so no button should have aria-pressed (Blazor omits false bool attributes)");
    }

    [Fact]
    public void Detail_IsFavoriteTrue_FavoriteButtonHasAriaPressed()
    {
        var state = UserStoryInteractionStateDto.AllFalse(1) with { IsFavorite = true };
        IRenderedComponent<UserStoryInteractionPanel> cut = Render<UserStoryInteractionPanel>(p => p
            .Add(c => c.StoryId, 1)
            .Add(c => c.State, state)
            .Add(c => c.Context, UserStoryInteractionDisplayContext.Detail));

        // Blazor renders bool true as the attribute being present (empty string value).
        IElement btn = cut.Find("button[aria-label='Favorite']");
        btn.HasAttribute("aria-pressed").Should().BeTrue(
            "IsFavorite=true → Favorite button should have aria-pressed present");
    }

    // ── Listing context — read-only badges + conditional ReadLater/Ignore ────────

    [Fact]
    public void Listing_BlankSlate_RendersOnlyReadLaterAndIgnoreButtons()
    {
        IRenderedComponent<UserStoryInteractionPanel> cut = Render<UserStoryInteractionPanel>(p => p
            .Add(c => c.StoryId, 2)
            .Add(c => c.State, UserStoryInteractionStateDto.AllFalse(2))
            .Add(c => c.Context, UserStoryInteractionDisplayContext.Listing));

        // Fav/PrivFav/Follow/Complete are read-only and inactive → leaf renders nothing.
        // ReadLater + Ignore are clickable on blank-slate → rendered as <button>.
        var buttons = cut.FindAll("button[aria-label]");
        buttons.Count.Should().Be(2, "only ReadLater and Ignore are clickable on a blank-slate story");
        buttons.Select(b => b.GetAttribute("aria-label")).Should().Equal(["Read It Later", "Ignored"]);
    }

    [Fact]
    public void Listing_IsFavoriteTrue_NoReadLaterOrIgnoreButtons()
    {
        var state = UserStoryInteractionStateDto.AllFalse(2) with { IsFavorite = true };
        IRenderedComponent<UserStoryInteractionPanel> cut = Render<UserStoryInteractionPanel>(p => p
            .Add(c => c.StoryId, 2)
            .Add(c => c.State, state)
            .Add(c => c.Context, UserStoryInteractionDisplayContext.Listing));

        // Story is not blank-slate (IsFavorite=true); ReadLater and Ignore are hidden unless active.
        cut.FindAll("button[aria-label]").Should().BeEmpty(
            "ReadLater and Ignore must hide when story is not blank-slate and neither is active");
    }

    [Fact]
    public void Listing_IsFavoriteTrue_FavoriteShownAsSpan()
    {
        var state = UserStoryInteractionStateDto.AllFalse(2) with { IsFavorite = true };
        IRenderedComponent<UserStoryInteractionPanel> cut = Render<UserStoryInteractionPanel>(p => p
            .Add(c => c.StoryId, 2)
            .Add(c => c.State, state)
            .Add(c => c.Context, UserStoryInteractionDisplayContext.Listing));

        // Favorite is active and read-only in listing context → rendered as <span>.
        cut.Find("span[aria-label='Favorite']").Should().NotBeNull(
            "active read-only buttons render as <span> via the leaf rule");
    }

    [Fact]
    public void Listing_IsReadLaterTrue_ReadLaterActiveAndIgnoreStillShown()
    {
        var state = UserStoryInteractionStateDto.AllFalse(2) with { IsReadItLater = true };
        IRenderedComponent<UserStoryInteractionPanel> cut = Render<UserStoryInteractionPanel>(p => p
            .Add(c => c.StoryId, 2)
            .Add(c => c.State, state)
            .Add(c => c.Context, UserStoryInteractionDisplayContext.Listing));

        // ReadLater is active → aria-pressed present.
        IElement readLater = cut.Find("button[aria-label='Read It Later']");
        readLater.HasAttribute("aria-pressed").Should().BeTrue(
            "IsReadItLater=true → ReadLater button should have aria-pressed present");

        // IsReadItLater=true does NOT break blank-slate (blank-slate only checks
        // Fav/HiddenFav/Follow/Complete/ActivelyReading). Ignore is still shown (blank-slate=true).
        cut.FindAll("button[aria-label='Ignored']").Count.Should().Be(1,
            "Ignore is still shown because IsReadItLater alone does not break blank-slate");
    }

    [Fact]
    public void Listing_IsCompletedTrue_ReadLaterAndIgnoreHidden()
    {
        var state = UserStoryInteractionStateDto.AllFalse(2) with { IsCompleted = true };
        IRenderedComponent<UserStoryInteractionPanel> cut = Render<UserStoryInteractionPanel>(p => p
            .Add(c => c.StoryId, 2)
            .Add(c => c.State, state)
            .Add(c => c.Context, UserStoryInteractionDisplayContext.Listing));

        // IsCompleted=true → not blank-slate → ReadLater and Ignore hidden.
        cut.FindAll("button[aria-label]").Should().BeEmpty();
        // Complete is active → rendered as read-only <span>.
        cut.Find("span[aria-label='Completed']").Should().NotBeNull();
    }

    // ── IsOwnStory ───────────────────────────────────────────────────────────────

    [Fact]
    public void IsOwnStory_True_RendersEditLinkAndNoButtons()
    {
        IRenderedComponent<UserStoryInteractionPanel> cut = Render<UserStoryInteractionPanel>(p => p
            .Add(c => c.StoryId, 7)
            .Add(c => c.State, UserStoryInteractionStateDto.AllFalse(7))
            .Add(c => c.Context, UserStoryInteractionDisplayContext.Detail)
            .Add(c => c.IsOwnStory, true));

        cut.Find("a").GetAttribute("href").Should().Contain("/story/7/edit");
        cut.FindAll("button").Should().BeEmpty("own story shows the edit link, not interaction buttons");
        cut.FindAll("span[aria-label]").Should().BeEmpty();
    }

    // ── Optimistic toggle ────────────────────────────────────────────────────────

    [Fact]
    public async Task Detail_ClickFavorite_ImmediatelyAddsAriaPressed_BeforeDebounce()
    {
        IRenderedComponent<UserStoryInteractionPanel> cut = Render<UserStoryInteractionPanel>(p => p
            .Add(c => c.StoryId, 3)
            .Add(c => c.State, UserStoryInteractionStateDto.AllFalse(3))
            .Add(c => c.Context, UserStoryInteractionDisplayContext.Detail));

        // Blazor omits aria-pressed when false, so initially the attribute is absent.
        cut.Find("button[aria-label='Favorite']").HasAttribute("aria-pressed").Should().BeFalse();

        await cut.Find("button[aria-label='Favorite']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Optimistic update fires before the 2-second debounce; re-render reflects the toggle.
        cut.Find("button[aria-label='Favorite']").HasAttribute("aria-pressed").Should().BeTrue(
            "the panel applies the toggle optimistically via StateHasChanged() before debounce fires");
    }

    [Fact]
    public async Task Detail_ClickFavoriteOff_RemovesAriaPressed()
    {
        var state = UserStoryInteractionStateDto.AllFalse(3) with { IsFavorite = true };
        IRenderedComponent<UserStoryInteractionPanel> cut = Render<UserStoryInteractionPanel>(p => p
            .Add(c => c.StoryId, 3)
            .Add(c => c.State, state)
            .Add(c => c.Context, UserStoryInteractionDisplayContext.Detail));

        // Initially active (aria-pressed present).
        cut.Find("button[aria-label='Favorite']").HasAttribute("aria-pressed").Should().BeTrue();

        await cut.Find("button[aria-label='Favorite']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // After toggle off, attribute is removed.
        cut.Find("button[aria-label='Favorite']").HasAttribute("aria-pressed").Should().BeFalse();
    }

    [Fact]
    public async Task Listing_ClickReadLater_FlipsAndBecomesActive()
    {
        IRenderedComponent<UserStoryInteractionPanel> cut = Render<UserStoryInteractionPanel>(p => p
            .Add(c => c.StoryId, 4)
            .Add(c => c.State, UserStoryInteractionStateDto.AllFalse(4))
            .Add(c => c.Context, UserStoryInteractionDisplayContext.Listing));

        cut.Find("button[aria-label='Read It Later']").HasAttribute("aria-pressed").Should().BeFalse();

        await cut.Find("button[aria-label='Read It Later']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.Find("button[aria-label='Read It Later']").HasAttribute("aria-pressed").Should().BeTrue(
            "clicking ReadLater optimistically toggles it active before debounce fires");
    }
}
