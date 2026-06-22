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
public class UserCardTests : TestContext
{
    // ── username and profile link ────────────────────────────────────────────────

    [Fact]
    public void UserCard_RendersUsername()
    {
        IRenderedComponent<UserCard> cut = RenderComponent<UserCard>(p =>
            p.Add(c => c.User, MakeUser(userId: 42, username: "Ash Ketchum")));

        cut.Markup.Should().Contain("Ash Ketchum");
    }

    [Fact]
    public void UserCard_ProfileLink_PointsToCorrectHref()
    {
        IRenderedComponent<UserCard> cut = RenderComponent<UserCard>(p =>
            p.Add(c => c.User, MakeUser(userId: 99, username: "Misty")));

        IElement profileLink = cut.FindAll("a[href]")
            .First(a => a.TextContent.Trim() == "Misty");

        profileLink.GetAttribute("href").Should().Be("/user/99");
    }

    // ── tagline ──────────────────────────────────────────────────────────────────

    [Fact]
    public void UserCard_WhenTaglineIsPresent_RendersTagline()
    {
        IRenderedComponent<UserCard> cut = RenderComponent<UserCard>(p =>
            p.Add(c => c.User, MakeUser(username: "Brock", tagline: "Pokémon Breeder")));

        cut.Markup.Should().Contain("Pokémon Breeder");
    }

    [Fact]
    public void UserCard_WhenTaglineIsNull_DoesNotRenderTaglineSpan()
    {
        IRenderedComponent<UserCard> cut = RenderComponent<UserCard>(p =>
            p.Add(c => c.User, MakeUser(username: "Gary", tagline: null)));

        // The tagline span has class "text-muted" — should not appear when tagline is null.
        cut.FindAll("span.text-muted").Should().BeEmpty("tagline is null → the tagline span must be suppressed");
    }

    // ── avatar ────────────────────────────────────────────────────────────────────

    [Fact]
    public void UserCard_WhenAvatarUrlIsProvided_UsesProvidedUrl()
    {
        IRenderedComponent<UserCard> cut = RenderComponent<UserCard>(p =>
            p.Add(c => c.User, MakeUser(username: "Trainer", avatarUrl: "/avatars/trainer.png")));

        cut.Find("img").GetAttribute("src").Should().Be("/avatars/trainer.png");
    }

    [Fact]
    public void UserCard_WhenAvatarUrlIsNull_FallsBackToDefaultAvatar()
    {
        IRenderedComponent<UserCard> cut = RenderComponent<UserCard>(p =>
            p.Add(c => c.User, MakeUser(username: "NoAvatar", avatarUrl: null)));

        cut.Find("img").GetAttribute("src").Should().Be("/img/default-avatar.svg",
            "null AvatarUrl → the default avatar constant must be used");
    }

    // ── caret menu visibility ─────────────────────────────────────────────────────

    [Fact]
    public void UserCard_MenuIsClosedByDefault()
    {
        IRenderedComponent<UserCard> cut = RenderComponent<UserCard>(p =>
            p.Add(c => c.User, MakeUser(username: "Mewtwo")));

        // The menu div is inside an @if (_menuOpen) block — absent when closed.
        cut.FindAll("div.absolute").Should().BeEmpty("the dropdown menu must be closed by default");
    }

    [Fact]
    public async Task UserCard_ClickingCaretButton_OpensMenu()
    {
        IRenderedComponent<UserCard> cut = RenderComponent<UserCard>(p =>
            p.Add(c => c.User, MakeUser(username: "Ditto")));

        IElement caretButton = cut.Find("button[aria-label='More options']");
        await caretButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.FindAll("div.absolute").Should().HaveCount(1, "clicking the caret toggles the menu open");
    }

    // ── optional callback buttons in the menu ────────────────────────────────────

    [Fact]
    public async Task UserCard_WithNoOptionalCallbacks_MenuShowsOnlyViewProfile()
    {
        IRenderedComponent<UserCard> cut = RenderComponent<UserCard>(p =>
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

        IRenderedComponent<UserCard> cut = RenderComponent<UserCard>(p => p
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

    // ── helper ───────────────────────────────────────────────────────────────────

    private static UserCardDto MakeUser(
        int userId = 1,
        string username = "TestUser",
        string? tagline = null,
        string? avatarUrl = null) =>
        new(userId, username, tagline, avatarUrl, Badges: []);
}
