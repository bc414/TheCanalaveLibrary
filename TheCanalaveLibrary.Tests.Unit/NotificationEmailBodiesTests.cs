using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Rendered-shape coverage for <see cref="NotificationEmailBodies"/> (WU-NotifEmail) — the same
/// "pure composition, no SMTP" testing posture as <see cref="EmailBodiesTests"/>.
///
/// <para>The message text itself is <see cref="TheCanalaveLibrary.SharedUI.NotificationPresenter"/>'s
/// job and is covered by <c>NotificationPresenterTests</c>; these tests deliberately do not restate
/// its ~40 per-type strings. What they cover is what this file adds on top: the subject choice, the
/// conditional call-to-action, the unsubscribe footer, and — the one with real teeth —
/// HTML-encoding of user-supplied entity titles.</para>
/// </summary>
public class NotificationEmailBodiesTests
{
    private const string Unsubscribe = "https://example.test/unsubscribe/TOKEN";
    private const string Settings = "https://example.test/notifications/settings";

    private static NotificationDto Notification(
        NotificationTypeEnum type = NotificationTypeEnum.NewStoryComment,
        string? sourceUserName = "Alice",
        string? targetTitle = "A Quiet Harbour",
        string? targetUrl = "/story/42") =>
        new(
            NotificationId: 1,
            NotificationTypeId: type,
            CategoryId: NotificationCategoryEnum.YourStories,
            SourceUserId: 7,
            SourceUserName: sourceUserName,
            TargetTitle: targetTitle,
            TargetUrl: targetUrl,
            RelatedEntityId: 42,
            IsRead: false,
            DateCreated: new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc),
            Collapsed: false);

    [Fact]
    public void Subject_IsTheNotificationTypeDisplayName()
    {
        (string subject, _) = NotificationEmailBodies.Compose(
            Notification(), "New Story Comment", "https://example.test/story/42", Unsubscribe, Settings);

        // The seeded DisplayName is already a short human-readable label per type, which is exactly
        // what a subject line needs — no second string to keep in sync.
        subject.Should().Be("New Story Comment");
    }

    [Fact]
    public void Body_ContainsThePresenterText()
    {
        (_, string body) = NotificationEmailBodies.Compose(
            Notification(), "New Story Comment", "https://example.test/story/42", Unsubscribe, Settings);

        body.Should().Contain("Alice").And.Contain("A Quiet Harbour");
    }

    [Fact]
    public void Body_HtmlEncodesUserSuppliedEntityTitles()
    {
        // Story titles are user-supplied and reach this file RAW from NotificationEnricher —
        // unlike EmailBodies, whose callback links arrive pre-encoded from the Identity scaffold.
        // If this ever regresses, every notification email becomes an HTML-injection vector.
        NotificationDto dto = Notification(targetTitle: "<script>alert('x')</script>");

        (_, string body) = NotificationEmailBodies.Compose(
            dto, "New Story Comment", "https://example.test/story/42", Unsubscribe, Settings);

        body.Should().NotContain("<script>");
        body.Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public void Body_HtmlEncodesUserSuppliedActorNames()
    {
        NotificationDto dto = Notification(sourceUserName: "Bob<img src=x onerror=1>");

        (_, string body) = NotificationEmailBodies.Compose(
            dto, "New Story Comment", "https://example.test/story/42", Unsubscribe, Settings);

        body.Should().NotContain("<img src=x");
        body.Should().Contain("&lt;img");
    }

    [Fact]
    public void Body_RendersTheCallToActionWhenATargetUrlExists()
    {
        (_, string body) = NotificationEmailBodies.Compose(
            Notification(), "New Story Comment", "https://example.test/story/42", Unsubscribe, Settings);

        body.Should().Contain("https://example.test/story/42");
        body.Should().Contain("View it on the site");
    }

    [Fact]
    public void Body_OmitsTheCallToActionWhenThereIsNoTarget()
    {
        // Site announcements, account warnings and report outcomes carry no navigable entity.
        // A dead "View it" button is worse than no button.
        NotificationDto dto = Notification(
            type: NotificationTypeEnum.AccountWarning, targetTitle: null, targetUrl: null);

        (_, string body) = NotificationEmailBodies.Compose(
            dto, "Account Warning", absoluteTargetUrl: null, Unsubscribe, Settings);

        body.Should().NotContain("View it on the site");
    }

    [Fact]
    public void Body_AlwaysCarriesBothUnsubscribeAndSettingsLinks()
    {
        // Every notification email must be unsubscribable from the message itself — the visible
        // half of the RFC 8058 pair whose headers the flusher sets.
        (_, string body) = NotificationEmailBodies.Compose(
            Notification(), "New Story Comment", "https://example.test/story/42", Unsubscribe, Settings);

        body.Should().Contain(Unsubscribe);
        body.Should().Contain(Settings);
    }

    [Fact]
    public void Body_NamesTheTypeBeingUnsubscribedFrom()
    {
        // The reader must be able to tell what the unsubscribe link silences — "these emails" is
        // ambiguous when a user gets a dozen notification types.
        (_, string body) = NotificationEmailBodies.Compose(
            Notification(), "New Story Comment", "https://example.test/story/42", Unsubscribe, Settings);

        body.Should().Contain("New Story Comment");
    }

    [Fact]
    public void SettingsPath_MatchesTheRealNotificationSettingsRoute()
    {
        // Guards a silent break: this constant is the only link from email back to the settings
        // page, and nothing else would fail if the page's @page route changed underneath it.
        NotificationEmailBodies.SettingsPath.Should().Be("/notifications/settings");
    }
}
