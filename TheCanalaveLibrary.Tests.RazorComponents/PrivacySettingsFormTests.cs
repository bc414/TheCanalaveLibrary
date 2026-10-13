using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// <see cref="PrivacySettingsForm"/>'s two <see cref="SocialInteractionPermission"/> selects
/// (WU-AccessGateSweep2). Their "Off" options used to post the literal <c>2</c> —
/// <see cref="SocialInteractionPermission.Following"/>, not <see cref="SocialInteractionPermission.Nobody"/>
/// (3) — so choosing "Off" for messages silently meant "only people I follow", and choosing it for
/// the comment wall left the wall shown (<c>ProfilePage</c> hides it only for <c>Nobody</c>). The
/// values are now bound to the enum. Covers the rendered option values and the EventCallback
/// argument the form raises on Save (L3 <c>@code</c> logic — this tier per <c>testing.md</c>).
/// The form has no <c>@inject</c>, so no DI setup is needed.
/// Tier: RazorComponents (bUnit).
/// </summary>
public class PrivacySettingsFormTests : BunitContext
{
    private static readonly PrivacySettingsDto Defaults = new(
        ProfileVisibility.Public,
        ShowActivityStatus: true,
        AllowProfileComments: SocialInteractionPermission.Public,
        AllowPrivateMessages: SocialInteractionPermission.UsersOnly,
        ShowUserStats: true,
        ShowCurrentlyReading: true,
        ShowMatureContent: false,
        AllowDiscoveryFromHiddenFavorites: false);

    [Theory]
    [InlineData("#allow-comments")]
    [InlineData("#allow-pm")]
    public void OffOption_CarriesTheNobodyValue(string selectId)
    {
        IRenderedComponent<PrivacySettingsForm> cut = Render<PrivacySettingsForm>(p =>
            p.Add(f => f.Initial, Defaults));

        IElement off = cut.FindAll($"{selectId} option").Single(o => o.TextContent.StartsWith("Off"));

        off.GetAttribute("value").Should().Be(((int)SocialInteractionPermission.Nobody).ToString(),
            "\"Off\" must write Nobody — the literal 2 it used to post is the Following tier");
    }

    [Fact]
    public void ChoosingOff_ThenSave_RaisesNobodyForBothSettings()
    {
        PrivacySettingsDto? saved = null;
        IRenderedComponent<PrivacySettingsForm> cut = Render<PrivacySettingsForm>(p => p
            .Add(f => f.Initial, Defaults)
            .Add(f => f.OnSave, (PrivacySettingsDto dto) => saved = dto));

        string offValue = cut.FindAll("#allow-comments option")
            .Single(o => o.TextContent.StartsWith("Off")).GetAttribute("value")!;
        cut.Find("#allow-comments").Change(offValue);
        cut.Find("#allow-pm").Change(cut.FindAll("#allow-pm option")
            .Single(o => o.TextContent.StartsWith("Off")).GetAttribute("value")!);
        cut.Find("button[type=button]").Click();

        saved.Should().NotBeNull();
        saved!.AllowProfileComments.Should().Be(SocialInteractionPermission.Nobody);
        saved.AllowPrivateMessages.Should().Be(SocialInteractionPermission.Nobody);
    }

    [Theory]
    [InlineData("#allow-comments")]
    [InlineData("#allow-pm")]
    public void SavedNobody_DisplaysAsTheOffOption(string selectId)
    {
        IRenderedComponent<PrivacySettingsForm> cut = Render<PrivacySettingsForm>(p => p
            .Add(f => f.Initial, Defaults with
            {
                AllowProfileComments = SocialInteractionPermission.Nobody,
                AllowPrivateMessages = SocialInteractionPermission.Nobody,
            }));

        string selected = cut.Find(selectId).GetAttribute("value")!;
        string off = cut.FindAll($"{selectId} option")
            .Single(o => o.TextContent.StartsWith("Off")).GetAttribute("value")!;

        selected.Should().Be(off,
            "a saved Nobody must match the Off option — otherwise no option matches and the browser "
            + "silently displays the first one (Public)");
    }

    /// <summary>
    /// Owner ruling D18 puts the new-message email preference "beside <c>AllowPrivateMessages</c> —
    /// where a user looks for it", and its one toggle on <c>/notifications/settings</c>. The privacy
    /// form's "Private messages" control therefore carries a pointer to that page, announced as the
    /// select's description, and no second toggle (WU-PrivateMessageEmail review fixes, 2026-10-13).
    /// </summary>
    [Fact]
    public void ThePrivateMessagesControl_PointsToTheMessageEmailToggle_AndOffersNoSecondOne()
    {
        IRenderedComponent<PrivacySettingsForm> cut = Render<PrivacySettingsForm>(p => p.Add(f => f.Initial, Defaults));

        IElement hint = cut.Find("#allow-pm-email-hint");
        cut.Find("#allow-pm").GetAttribute("aria-describedby").Should().Be("allow-pm-email-hint",
            "a screen reader on the select hears where the email setting lives");
        hint.TextContent.Should().Contain("Emails about new messages");

        string href = hint.QuerySelector("a")!.GetAttribute("href")!;
        string notificationSettingsRoute = typeof(NotificationSettingsPage)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.Components.RouteAttribute>().Single().Template;
        href.Should().Be(notificationSettingsRoute, "the pointer must land on the page that holds the toggle");

        cut.FindAll("input[type=checkbox]").Select(c => c.ParentElement!.TextContent)
            .Should().NotContain(text => text.Contains("email", StringComparison.OrdinalIgnoreCase),
                "one value, one toggle: a second one here would save on a different gesture");
    }
}
