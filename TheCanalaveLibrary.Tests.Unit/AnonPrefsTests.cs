using FluentAssertions;
using Microsoft.AspNetCore.Http;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// <see cref="AnonPrefs"/>, the anonymous viewer's consent cookie (Feature 66). Found and fixed by
/// WU-ThrottleCoverage (2026-10-13) while it guarded the anonymous reveal branch: <c>Read</c> returned one
/// shared static instance for every request with no (or a malformed) cookie, and the consent endpoints
/// mutate what <c>Read</c> returns. So the first cookieless visitor to press "Always show mature content"
/// switched mature content on for every later cookieless visitor in the process — crawlers included —
/// and the same for "View this story". Also pinned: a cookie that nulls a list parses to defaults
/// (the class's "never throw on request input" contract). Tier: Unit (directly constructed, no host).
/// </summary>
public class AnonPrefsTests
{
    [Fact]
    public void Read_WithNoCookie_HandsEachRequestItsOwnInstance()
    {
        AnonPrefs first = AnonPrefs.Read(RequestWithCookie(null));
        first.Mature = true;                              // what POST /content-gate/mature does
        first.AddReveal(RevealedEntityType.Story, 5);     // what POST /content-gate/reveal does

        AnonPrefs second = AnonPrefs.Read(RequestWithCookie(null));

        second.Should().NotBeSameAs(first);
        second.Mature.Should().BeFalse("another visitor's consent is not this visitor's");
        second.HasRevealed(RevealedEntityType.Story, 5).Should().BeFalse();
    }

    [Fact]
    public void Read_WithAMalformedCookie_HandsEachRequestItsOwnInstance()
    {
        AnonPrefs first = AnonPrefs.Read(RequestWithCookie("{not json"));
        first.Mature = true;

        AnonPrefs.Read(RequestWithCookie("{not json")).Mature.Should().BeFalse();
    }

    [Fact]
    public void OneCookielessViewersConsent_NeverReachesAnotherCookielessViewersContext()
    {
        // The consumer-level statement of the same property: the active-user context of a later
        // cookieless request still sees mature content off.
        AnonPrefs.Read(RequestWithCookie(null)).Mature = true;

        DefaultHttpContext later = new();
        ServerActiveUserContext viewer = new(new HttpContextAccessor { HttpContext = later }, null!);

        viewer.IsAuthenticated.Should().BeFalse();
        viewer.ShowMatureContent.Should().BeFalse();
        viewer.MaxRating.Should().Be(Rating.T);
    }

    [Fact]
    public void Read_ACookieThatNullsItsLists_ParsesToEmptyLists_AndNeverThrows()
    {
        AnonPrefs prefs = AnonPrefs.Read(RequestWithCookie("""{"m":true,"rs":null,"rg":null,"rb":null}"""));

        prefs.Mature.Should().BeTrue();
        prefs.HasRevealed(RevealedEntityType.Story, 1).Should().BeFalse();
        prefs.Invoking(p => p.AddReveal(RevealedEntityType.Group, 3)).Should().NotThrow();
        prefs.HasRevealed(RevealedEntityType.Group, 3).Should().BeTrue();
    }

    [Fact]
    public void Read_AWellFormedCookie_KeepsItsReveals()
    {
        AnonPrefs prefs = AnonPrefs.Read(RequestWithCookie("""{"m":false,"rs":[4,9],"rg":[],"rb":[2]}"""));

        prefs.HasRevealed(RevealedEntityType.Story, 9).Should().BeTrue();
        prefs.HasRevealed(RevealedEntityType.BlogPost, 2).Should().BeTrue();
        prefs.HasRevealed(RevealedEntityType.Group, 4).Should().BeFalse();
    }

    private static HttpRequest RequestWithCookie(string? value)
    {
        DefaultHttpContext context = new();
        if (value is not null)
            context.Request.Headers.Cookie = $"{AnonPrefs.CookieName}={Uri.EscapeDataString(value)}";
        return context.Request;
    }
}
