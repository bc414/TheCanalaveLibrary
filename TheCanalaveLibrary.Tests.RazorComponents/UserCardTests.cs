using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="UserCard"/> (WU10). The card is a pure leaf — it injects no service,
/// takes a <see cref="UserCardDto"/> and four optional EventCallback parameters
/// (OnDiscoverFromUser, OnCopyLink, OnReport, OnSendMessage), and renders a user-summary widget
/// with a profile link, optional tagline/badges, and a toggleable caret menu. All behaviours are
/// exercisable without a host or DB.
/// </summary>
public class UserCardTests : BunitContext
{
    // ── username and profile link ────────────────────────────────────────────────

    [Fact]
    public void UserCard_RendersUsername()
    {
        IRenderedComponent<UserCard> cut = Render<UserCard>(p =>
            p.Add(c => c.User, MakeUser(userId: 42, username: "Ash Ketchum")));

        cut.Markup.Should().Contain("Ash Ketchum");
    }

    [Fact]
    public void UserCard_ProfileLink_PointsToCorrectHref()
    {
        IRenderedComponent<UserCard> cut = Render<UserCard>(p =>
            p.Add(c => c.User, MakeUser(userId: 99, username: "Misty")));

        IElement profileLink = cut.FindAll("a[href]")
            .First(a => a.TextContent.Trim() == "Misty");

        profileLink.GetAttribute("href").Should().Be("/user/99");
    }

    // ── tagline ──────────────────────────────────────────────────────────────────

    [Fact]
    public void UserCard_WhenTaglineIsPresent_RendersTagline()
    {
        IRenderedComponent<UserCard> cut = Render<UserCard>(p =>
            p.Add(c => c.User, MakeUser(username: "Brock", tagline: "Pokémon Breeder")));

        cut.Markup.Should().Contain("Pokémon Breeder");
    }

    // ── avatar ────────────────────────────────────────────────────────────────────

    [Fact]
    public void UserCard_WhenAvatarUrlIsProvided_UsesProvidedUrl()
    {
        IRenderedComponent<UserCard> cut = Render<UserCard>(p =>
            p.Add(c => c.User, MakeUser(username: "Trainer", avatarUrl: "/avatars/trainer.png")));

        cut.Find("img").GetAttribute("src").Should().Be("/avatars/trainer.png");
    }

    [Fact]
    public void UserCard_WhenAvatarUrlIsNull_FallsBackToDefaultAvatar()
    {
        IRenderedComponent<UserCard> cut = Render<UserCard>(p =>
            p.Add(c => c.User, MakeUser(username: "NoAvatar", avatarUrl: null)));

        cut.Find("img").GetAttribute("src").Should().Be("/img/default-avatar.svg",
            "null AvatarUrl → the default avatar constant must be used");
    }

    // ── caret menu visibility ─────────────────────────────────────────────────────

    [Fact]
    public void UserCard_MenuIsClosedByDefault()
    {
        IRenderedComponent<UserCard> cut = Render<UserCard>(p =>
            p.Add(c => c.User, MakeUser(username: "Mewtwo")));

        // The menu div is inside an @if (_menuOpen) block — absent when closed.
        cut.FindAll("div.absolute").Should().BeEmpty("the dropdown menu must be closed by default");
    }

    [Fact]
    public async Task UserCard_ClickingCaretButton_OpensMenu()
    {
        IRenderedComponent<UserCard> cut = Render<UserCard>(p =>
            p.Add(c => c.User, MakeUser(username: "Ditto")));

        IElement caretButton = cut.Find("button[aria-label='More options']");
        await caretButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.FindAll("div.absolute").Should().HaveCount(1, "clicking the caret toggles the menu open");
    }

    // ── optional callback buttons in the menu ────────────────────────────────────

    [Fact]
    public async Task UserCard_WithNoOptionalCallbacks_MenuShowsOnlyViewProfile()
    {
        IRenderedComponent<UserCard> cut = Render<UserCard>(p =>
            p.Add(c => c.User, MakeUser(username: "Snorlax")));

        // Open the menu.
        await cut.Find("button[aria-label='More options']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Only "View Profile" link — no optional buttons (Report, Send PM, etc.).
        cut.FindAll("div.absolute button").Should().BeEmpty(
            "with no optional EventCallbacks, only the 'View Profile' link appears in the menu (no buttons)");
    }

    [Fact]
    public async Task UserCard_WhenOnReportHasDelegate_ShowsReportButtonInMenu()
    {
        bool reportInvoked = false;

        IRenderedComponent<UserCard> cut = Render<UserCard>(p => p
            .Add(c => c.User, MakeUser(username: "Villain"))
            .Add(c => c.OnReport, () => { reportInvoked = true; }));

        // Open menu.
        await cut.Find("button[aria-label='More options']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        IElement reportButton = cut.FindAll("div.absolute button")
            .First(b => b.TextContent.Trim() == "Report");

        reportButton.Should().NotBeNull("Report button must appear when OnReport has a delegate");

        await reportButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        reportInvoked.Should().BeTrue("clicking Report must invoke the OnReport callback");
    }

    // ── badge counts (WU-CounterSymmetry: a badge is a number; a manual grant's 0 is not shown) ──

    [Fact]
    public void UserCard_BadgeWithACount_RendersTheCountAndTheTooltipSuffix()
    {
        IRenderedComponent<UserCard> cut = Render<UserCard>(p => p.Add(c => c.User,
            MakeUser(username: "Gary") with { Badges = [new UserCardBadgeDto("/icons/rec.png", "Recommender", 3)] }));

        IElement badge = cut.Find("span[title]");
        badge.GetAttribute("title").Should().Be("Recommender (3)");
        badge.TextContent.Trim().Should().Be("3");
    }

    [Fact]
    public void UserCard_ZeroCountBadge_RendersNoCountText_AndAPlainTooltip()
    {
        // A manual grant (Patron/Architect/Artist) carries EarnedCount 0 by design; it used to render
        // a literal "0" and "Patron (0)". Same > 0 guard as BadgeSettingsForm.
        IRenderedComponent<UserCard> cut = Render<UserCard>(p => p.Add(c => c.User,
            MakeUser(username: "Oak") with { Badges = [new UserCardBadgeDto("/icons/patron.png", "Patron", 0)] }));

        IElement badge = cut.Find("span[title]");
        badge.GetAttribute("title").Should().Be("Patron");
        badge.TextContent.Trim().Should().BeEmpty("no count text for a zero count");
        badge.QuerySelector("img").Should().NotBeNull("the badge icon itself still renders");
    }

    // ── helper ───────────────────────────────────────────────────────────────────

    private static UserCardDto MakeUser(
        int userId = 1,
        string username = "TestUser",
        string? tagline = null,
        string? avatarUrl = null) =>
        new(userId, username, tagline, avatarUrl, Badges: []);
}
