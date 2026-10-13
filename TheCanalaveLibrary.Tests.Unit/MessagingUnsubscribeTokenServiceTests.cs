using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Round-trip and rejection coverage for <see cref="MessagingUnsubscribeTokenService"/> (owner ruling
/// D18, WU-PrivateMessageEmail 2026-10-13), mirroring <see cref="UnsubscribeTokenServiceTests"/>. Uses
/// <see cref="EphemeralDataProtectionProvider"/> (an in-memory key ring). The cross-purpose cases are
/// the point of D18's "one purpose per owning feature": over one shared key ring, a notification link
/// must never read as a message link, and the reverse. Tier: Unit.
/// </summary>
public class MessagingUnsubscribeTokenServiceTests
{
    [Fact]
    public void CreateToken_RoundTripsTheUserId()
    {
        var service = new MessagingUnsubscribeTokenService(new EphemeralDataProtectionProvider());

        service.TryRead(service.CreateToken(4242)).Should().Be(4242);
    }

    [Fact]
    public void CreateToken_ProducesUrlSafeTokens()
    {
        string token = new MessagingUnsubscribeTokenService(new EphemeralDataProtectionProvider()).CreateToken(1);

        token.Should().NotContain("+").And.NotContain("/").And.NotContain("=");
    }

    [Fact]
    public void TryRead_RejectsATamperedToken()
    {
        var service = new MessagingUnsubscribeTokenService(new EphemeralDataProtectionProvider());
        char[] chars = service.CreateToken(4242).ToCharArray();
        chars[chars.Length / 2] = chars[chars.Length / 2] == 'A' ? 'B' : 'A';

        service.TryRead(new string(chars)).Should().BeNull();
    }

    [Theory]
    [InlineData("not-a-token")]
    [InlineData("")]
    public void TryRead_RejectsGarbage(string token)
    {
        new MessagingUnsubscribeTokenService(new EphemeralDataProtectionProvider()).TryRead(token).Should().BeNull();
    }

    [Fact]
    public void TryRead_RejectsATokenMintedByADifferentKeyRing()
    {
        string foreign = new MessagingUnsubscribeTokenService(new EphemeralDataProtectionProvider()).CreateToken(4242);

        new MessagingUnsubscribeTokenService(new EphemeralDataProtectionProvider()).TryRead(foreign).Should().BeNull();
    }

    [Fact]
    public void ANotificationToken_DoesNotReadAsAMessagingToken()
    {
        var ring = new EphemeralDataProtectionProvider();
        string notificationToken = new UnsubscribeTokenService(ring).CreateToken(4242, NotificationTypeEnum.NewFollowerOnYou);

        new MessagingUnsubscribeTokenService(ring).TryRead(notificationToken).Should().BeNull(
            "one key ring, two purposes: a notification link must never turn message email off");
    }

    [Fact]
    public void AMessagingToken_DoesNotReadAsANotificationToken()
    {
        var ring = new EphemeralDataProtectionProvider();
        string messagingToken = new MessagingUnsubscribeTokenService(ring).CreateToken(4242);

        new UnsubscribeTokenService(ring).TryRead(messagingToken).Should().BeNull(
            "a message link must never silence a notification type");
    }

    [Fact]
    public void PathFor_BuildsTheTwoSegmentRoute()
    {
        MessagingUnsubscribeTokenService.PathFor("ABC").Should().Be("/unsubscribe/messages/ABC");
    }

    [Fact]
    public void TokenLifetime_IsLongEnoughForMailThatSitsInAnInbox()
    {
        MessagingUnsubscribeTokenService.TokenLifetime.Should().BeGreaterThanOrEqualTo(TimeSpan.FromDays(180));
    }
}
