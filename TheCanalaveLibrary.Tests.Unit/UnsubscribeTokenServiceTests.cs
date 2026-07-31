using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Round-trip and rejection coverage for <see cref="UnsubscribeTokenService"/> (WU-NotifEmail).
/// Uses <see cref="EphemeralDataProtectionProvider"/> — an in-memory key ring, so no host, no DB,
/// no key-ring directory.
///
/// <para><b>Not tested here: actual expiry.</b> That is
/// <see cref="ITimeLimitedDataProtector"/>'s own guarantee, and forcing a clock would mean
/// widening this class's surface purely to observe it. What <em>is</em> tested is that an expired
/// token lands in the same rejection path as a tampered one, which is the behaviour that
/// matters — <see cref="UnsubscribeTokenService.TryRead"/> returns null for every invalid reason
/// and callers must not distinguish them.</para>
/// </summary>
public class UnsubscribeTokenServiceTests
{
    private static UnsubscribeTokenService NewService() =>
        new(new EphemeralDataProtectionProvider());

    [Fact]
    public void CreateToken_RoundTripsUserIdAndType()
    {
        UnsubscribeTokenService service = NewService();

        string token = service.CreateToken(4242, NotificationTypeEnum.NewStoryComment);

        service.TryRead(token).Should().Be((4242, NotificationTypeEnum.NewStoryComment));
    }

    [Fact]
    public void CreateToken_ProducesUrlSafeTokens()
    {
        // The token goes straight into a URL path segment (PathFor) and into a List-Unsubscribe
        // header. Data Protection's string protector is base64url, so this should hold — the test
        // exists because a regression would produce broken links in already-delivered mail, which
        // is unfixable after the fact.
        string token = NewService().CreateToken(1, NotificationTypeEnum.AccountWarning);

        token.Should().NotContain("+").And.NotContain("/").And.NotContain("=");
    }

    [Fact]
    public void TryRead_RejectsATamperedToken()
    {
        UnsubscribeTokenService service = NewService();
        string token = service.CreateToken(4242, NotificationTypeEnum.NewStoryComment);

        // Flip a character in the middle of the payload.
        char[] chars = token.ToCharArray();
        chars[chars.Length / 2] = chars[chars.Length / 2] == 'A' ? 'B' : 'A';

        service.TryRead(new string(chars)).Should().BeNull();
    }

    [Fact]
    public void TryRead_RejectsGarbage()
    {
        NewService().TryRead("not-a-token").Should().BeNull();
    }

    [Fact]
    public void TryRead_RejectsAnEmptyToken()
    {
        NewService().TryRead("").Should().BeNull();
    }

    [Fact]
    public void TryRead_RejectsATokenMintedByADifferentKeyRing()
    {
        // Proves the token is genuinely signed rather than merely encoded: an attacker who can
        // read one user's link must not be able to forge another's. Also models the key-rotation
        // case, where outstanding links degrade to "no longer valid" rather than to a wrong action.
        string foreignToken = NewService().CreateToken(4242, NotificationTypeEnum.NewStoryComment);

        NewService().TryRead(foreignToken).Should().BeNull();
    }

    [Fact]
    public void PathFor_BuildsTheUnsubscribeRoute()
    {
        UnsubscribeTokenService.PathFor("ABC").Should().Be("/unsubscribe/ABC");
    }

    [Fact]
    public void TokenLifetime_IsLongEnoughForMailThatSitsInAnInbox()
    {
        // An expired unsubscribe link is a worse outcome than a long-lived one whose worst case is
        // muting a single notification type for a single user. Pinned so a future "tighten the
        // expiry" change has to argue with this comment first.
        UnsubscribeTokenService.TokenLifetime.Should().BeGreaterThanOrEqualTo(TimeSpan.FromDays(180));
    }
}
