using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheCanalaveLibrary.Server;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// <see cref="UnsubscribeLandingPage"/>, the HTML shell every one-click unsubscribe page renders
/// through (notification and private-message routes alike). Its settings link pointed at
/// <c>/settings</c>, which holds no email settings, until WU-PrivateMessageEmail (2026-10-13) sent it
/// to <c>/notifications/settings</c> — D18's standing rider makes that page the one home of every
/// optional email setting. These pin the link to the page's real route, the title encoding (the only
/// value the shell encodes itself) and the caller's body. Tier: Unit (a static renderer, no host).
/// </summary>
public class UnsubscribeLandingPageTests
{
    private static string NotificationSettingsRoute =>
        typeof(NotificationSettingsPage).GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>().Single().Template;

    [Fact]
    public void TheSettingsLink_GoesToTheNotificationSettingsPage()
    {
        string html = UnsubscribeLandingPage.Render("Unsubscribed", "<p>Done.</p>");

        html.Should().Contain($"<a href=\"{NotificationSettingsRoute}\">Manage your email settings</a>",
            "the link must land on the page that holds every optional email setting (D18's standing rider)");
        html.Should().NotContain("href=\"/settings\"", "the profile settings page has no email settings");
    }

    [Fact]
    public void BothEmailBodies_LinkTheSamePage()
    {
        UnsubscribeLandingPage.EmailSettingsPath.Should().Be(NotificationSettingsRoute);
        NotificationEmailBodies.SettingsPath.Should().Be(NotificationSettingsRoute,
            "a notification email's footer link names the same page as its landing page");
    }

    [Fact]
    public void TheTitleIsEncoded_AndTheBodyIsKeptAsGiven()
    {
        string html = UnsubscribeLandingPage.Render("<script>alert(1)</script>", "<p>Body <b>kept</b></p>");

        html.Should().NotContain("<script>");
        html.Should().Contain("&lt;script&gt;alert(1)&lt;/script&gt;");
        html.Should().Contain("<p>Body <b>kept</b></p>", "the caller encodes what it interpolates; the shell inserts it as-is");
        html.Should().Contain("<meta name=\"robots\" content=\"noindex,nofollow\">");
    }
}
