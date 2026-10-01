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
}
