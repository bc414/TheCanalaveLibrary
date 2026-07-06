using FluentAssertions;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Pure body-composition coverage for <see cref="EmailBodies"/> — no send, no SMTP, no host;
/// exercises exactly the subject/body shape <see cref="SmtpEmailSender"/> hands to MailKit.
/// </summary>
public class EmailBodiesTests
{
    [Fact]
    public void ConfirmationBody_EmbedsTheLinkVerbatimAsHref()
    {
        // The link arrives pre-encoded by the caller (Register.razor et al. wrap it in
        // HtmlEncoder.Default.Encode) — this fixture mirrors that shape (&amp; already escaped).
        string body = EmailBodies.ConfirmationBody("https://example.com/confirm?userId=1&amp;code=abc");

        body.Should().Contain("href='https://example.com/confirm?userId=1&amp;code=abc'");
        body.Should().Contain("clicking here");
    }

    [Fact]
    public void PasswordResetLinkBody_EmbedsTheLinkVerbatimAsHref()
    {
        string body = EmailBodies.PasswordResetLinkBody("https://example.com/reset?code=xyz&amp;returnUrl=%2F");

        body.Should().Contain("href='https://example.com/reset?code=xyz&amp;returnUrl=%2F'");
    }

    [Fact]
    public void ConfirmationBody_DoesNotReEncodeAnAlreadyEncodedLink()
    {
        // Regression for a real bug found via live Mailpit verification (2026-07-06): Register.razor
        // passes an already-HtmlEncoder-escaped link (its query "&" is already "&amp;"). Re-encoding
        // that here turns "&amp;" into "&amp;amp;", which survives one round of browser HTML-decoding
        // as the literal text "&amp;" instead of a real "&" — the confirmation link's second query
        // parameter ("code") then fails to separate from "userId" and never binds on ConfirmEmail.
        const string preEncodedLink = "https://example.com/confirm?userId=8&amp;code=abc123";

        string body = EmailBodies.ConfirmationBody(preEncodedLink);

        body.Should().NotContain("&amp;amp;", "re-encoding an already-encoded '&' breaks the query separator");
        body.Should().Contain(preEncodedLink);
    }

    [Fact]
    public void PasswordResetCodeBody_HtmlEncodesTheRawCode()
    {
        // Unlike the link methods, no caller in this codebase pre-encodes resetCode (only the
        // Identity API endpoints' JSON clients call this method) — so EmailBodies is the one place
        // that must encode it.
        string body = EmailBodies.PasswordResetCodeBody("<code>&123");

        body.Should().Contain("&lt;code&gt;&amp;123");
        body.Should().NotContain("<code>&123");
    }

    [Fact]
    public void SubjectConstants_AreNonEmpty()
    {
        EmailBodies.ConfirmationSubject.Should().NotBeNullOrWhiteSpace();
        EmailBodies.PasswordResetLinkSubject.Should().NotBeNullOrWhiteSpace();
        EmailBodies.PasswordResetCodeSubject.Should().NotBeNullOrWhiteSpace();
    }
}
