using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// <see cref="PrivateMessageEmailBodies"/> — the private-message email's subject and body (owner ruling
/// D18: Messaging owns its body; WU-PrivateMessageEmail 2026-10-13). Pins encoding, the deleted-sender
/// fallback, the three absolute links, and above all that no sender-authored text can reach a mail
/// (roadmap decision row 28 (b)): the API has no parameter for it, and the body's text is exactly the
/// fixed copy around the sender's name. Tier: Unit.
/// </summary>
public class PrivateMessageEmailBodiesTests
{
    private const string ConversationUrl = "https://library.test/messages/12";
    private const string UnsubscribeUrl = "https://library.test/unsubscribe/messages/TOKEN";
    private const string SettingsUrl = "https://library.test/notifications/settings";

    private static (string Subject, string Body) Compose(string? sender) =>
        PrivateMessageEmailBodies.Compose(sender, ConversationUrl, UnsubscribeUrl, SettingsUrl);

    [Fact]
    public void Subject_NamesTheSender()
    {
        Compose("Cynthia").Subject.Should().Be("New private message from Cynthia");
    }

    [Fact]
    public void AScriptInTheUsername_IsEncodedInTheBody()
    {
        (_, string body) = Compose("<script>alert(1)</script>");

        body.Should().NotContain("<script>");
        body.Should().Contain("&lt;script&gt;alert(1)&lt;/script&gt; sent you a private message");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ADeletedOrUnknownSender_ReadsAsAMember(string? sender)
    {
        (string subject, string body) = Compose(sender);

        subject.Should().Be($"New private message from {PrivateMessageEmailBodies.UnknownSender}");
        body.Should().Contain($"{PrivateMessageEmailBodies.UnknownSender} sent you a private message");
    }

    [Fact]
    public void TheBody_LinksTheConversation_TheUnsubscribe_AndTheSettingsPage()
    {
        (_, string body) = Compose("Cynthia");

        body.Should().Contain($"href=\"{ConversationUrl}\"");
        body.Should().Contain($"href=\"{UnsubscribeUrl}\"");
        body.Should().Contain($"href=\"{SettingsUrl}\"");
    }

    [Fact]
    public void Compose_HasNoParameterThatCouldCarryTheMessageOrTheSubject()
    {
        // The guarantee is structural: a caller cannot pass sender-authored text because there is
        // nowhere to put it. A new parameter must fail this test and be argued for (decision row 28 (b)).
        MethodInfo compose = typeof(PrivateMessageEmailBodies).GetMethod(nameof(PrivateMessageEmailBodies.Compose))!;

        compose.GetParameters().Select(p => p.Name).Should().Equal(
            "senderUserName", "absoluteConversationUrl", "absoluteUnsubscribeUrl", "absoluteSettingsUrl");
    }

    [Fact]
    public void TheBodysText_IsExactlyTheFixedCopy()
    {
        (_, string body) = Compose("Cynthia");

        string text = Regex.Replace(Regex.Replace(body, "<[^>]+>", " "), @"\s+", " ").Replace("&nbsp;", " ").Trim();

        Regex.Replace(text, @"\s+", " ").Should().Be(
            "The Canalave Library Cynthia sent you a private message on The Canalave Library. " +
            "Read it on the site You received this because new-message emails are on for your account. " +
            "You won't get another until you've read this conversation. " +
            "Unsubscribe from message emails · All email settings");
    }
}
