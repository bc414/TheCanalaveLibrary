using AngleSharp.Html.Parser;
using FluentAssertions;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ServerHtmlSanitizationService"/> — the XSS allow-list that mirrors
/// <c>EditorView</c>'s toolbar (WU5/WU6). Verified WU6 by only a throwaway dev-diagnostics endpoint
/// that was removed; these tests are the standing regression net for the allow-list (see
/// <c>canalave-conventions/testing.md</c> §"Three test tiers").
///
/// The service has no dependencies (constructed directly — no host, no DB) and is registered
/// Singleton in production, so <c>new ServerHtmlSanitizationService()</c> is the correct test setup.
/// </summary>
public class HtmlSanitizationServiceTests
{
    private readonly ServerHtmlSanitizationService _sut = new();

    // ── null / empty guard ──────────────────────────────────────────────────────

    [Fact]
    public void Sanitize_OfNull_ReturnsEmptyString()
    {
        _sut.Sanitize(null).Should().Be(string.Empty);
    }

    [Fact]
    public void Sanitize_OfEmptyString_ReturnsEmptyString()
    {
        _sut.Sanitize(string.Empty).Should().Be(string.Empty);
    }

    [Fact]
    public void Sanitize_OfWhitespaceOnly_ReturnsEmptyString()
    {
        _sut.Sanitize("   ").Should().Be(string.Empty);
    }

    // ── XSS / dangerous tags stripped ──────────────────────────────────────────

    [Fact]
    public void Sanitize_ScriptTag_IsStrippedCompletely()
    {
        // This is the WU6 behavior that was verified by a removed throwaway endpoint.
        var input = "<p>Hello</p><script>alert('xss')</script>";
        var result = _sut.Sanitize(input);
        result.Should().NotContain("<script");
        result.Should().NotContain("alert(");
        result.Should().Contain("<p>Hello</p>");
    }

    [Theory]
    [InlineData("<iframe src='evil'></iframe>")]
    [InlineData("<object data='evil'></object>")]
    [InlineData("<form action='evil'></form>")]
    [InlineData("<input type='hidden' value='evil'>")]
    [InlineData("<style>body{display:none}</style>")]
    public void Sanitize_DisallowedTag_IsStripped(string disallowedHtml)
    {
        var result = _sut.Sanitize(disallowedHtml);
        // The element itself should be gone; inner text content may or may not survive
        // depending on AngleSharp's fragment handling, but the tag is definitely gone.
        result.Should().NotMatchRegex(@"<iframe|<object|<form|<input|<style", "disallowed tags must not survive sanitization");
    }

    // ── allowed tags survive ────────────────────────────────────────────────────

    [Theory]
    [InlineData("<p>paragraph</p>")]
    [InlineData("<strong>bold</strong>")]
    [InlineData("<em>italic</em>")]
    [InlineData("<u>underline</u>")]
    [InlineData("<s>strikethrough</s>")]
    [InlineData("<h2>Heading two</h2>")]
    [InlineData("<h3>Heading three</h3>")]
    [InlineData("<blockquote>quote</blockquote>")]
    [InlineData("<ul><li>item</li></ul>")]
    [InlineData("<ol><li>item</li></ol>")]
    [InlineData("<br>")]
    public void Sanitize_AllowedTag_Survives(string allowedHtml)
    {
        // Extract the tag name for the assertion
        var tagMatch = System.Text.RegularExpressions.Regex.Match(allowedHtml, @"<(\w+)");
        tagMatch.Success.Should().BeTrue();
        var tagName = tagMatch.Groups[1].Value;

        var result = _sut.Sanitize(allowedHtml);
        result.Should().Contain($"<{tagName}", $"<{tagName}> is in the EditorView allow-list and must survive sanitization");
    }

    // ── anchor handling: href survives, other attrs/schemes are stripped ────────

    [Fact]
    public void Sanitize_AnchorWithHref_SurvivesWithHrefOnly()
    {
        var input = "<a href=\"https://example.com\" onclick=\"evil()\" style=\"color:red\">link</a>";
        var result = _sut.Sanitize(input);
        result.Should().Contain("href=", "href is the one allowed anchor attribute");
        result.Should().NotContain("onclick", "onclick is not an allowed attribute");
        result.Should().NotContain("style=", "style is not an allowed attribute on anchors");
    }

    [Fact]
    public void Sanitize_AnchorWithHttpsHref_GetsTargetBlankAndNoopenerNoreferrer()
    {
        // PostProcessNode hook normalizes all anchors for safe external link behavior.
        var input = "<a href=\"https://example.com\">link</a>";
        var result = _sut.Sanitize(input);
        result.Should().Contain("target=\"_blank\"", "all anchors must get target=_blank");
        result.Should().Contain("rel=\"noopener noreferrer\"", "all anchors must get rel=noopener noreferrer");
    }

    [Theory]
    [InlineData("javascript:alert(1)", "javascript: scheme must be rejected")]
    [InlineData("vbscript:msgbox(1)", "vbscript: scheme must be rejected")]
    [InlineData("data:text/html,<h1>x</h1>", "data: scheme must be rejected")]
    public void Sanitize_AnchorWithDisallowedScheme_HrefIsRemoved(string dangerousHref, string reason)
    {
        var input = $"<a href=\"{dangerousHref}\">link</a>";
        var result = _sut.Sanitize(input);
        result.Should().NotContain(dangerousHref, reason);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com")]
    [InlineData("mailto:user@example.com")]
    public void Sanitize_AnchorWithAllowedScheme_HrefSurvives(string allowedHref)
    {
        var input = $"<a href=\"{allowedHref}\">link</a>";
        var result = _sut.Sanitize(input);
        result.Should().Contain(allowedHref, $"{allowedHref.Split(':')[0]}: is an allowed scheme");
    }

    // ── CSS / class attributes are stripped ────────────────────────────────────

    [Fact]
    public void Sanitize_StyleAttribute_IsStripped()
    {
        var input = "<p style=\"color:red;font-size:9999px\">text</p>";
        var result = _sut.Sanitize(input);
        result.Should().NotContain("style=");
    }

    [Fact]
    public void Sanitize_ClassAttribute_IsStripped()
    {
        var input = "<p class=\"ql-align-center\">text</p>";
        var result = _sut.Sanitize(input);
        result.Should().NotContain("class=");
    }

    // ── plain text content is preserved ────────────────────────────────────────

    [Fact]
    public void Sanitize_PlainTextInAllowedElement_TextContentSurvives()
    {
        var input = "<p>The Canalave Library</p>";
        var result = _sut.Sanitize(input);
        result.Should().Contain("The Canalave Library");
    }

    // ── parser mXSS: the AngleSharp advisories (tracker E2) ──────────────────────
    //
    // Both are parse/serialize disagreements: the sanitizer walks AngleSharp's DOM and approves
    // it, but the serialized output re-parses (in a browser) into a different tree with a live
    // <img onerror>.
    // - CVE-2026-54570 / GHSA-pgww-w46g-26qg (< 1.5.0): content inside
    //   <annotation-xml encoding="text/html"> is HTML (an integration point), not MathML.
    // - GHSA-cgp3-27rh-pcp2 (1.3.1 – 1.8.1): an SVG <style>'s text was serialized raw, so an
    //   escaped "&lt;/style>" came back out as a real end tag.
    // The allow-list strips both vectors whatever the parser does; the two canaries pin the parser
    // itself past both fixes (the first fails on 0.17.1, the second on 1.7.2).

    private const string AnnotationXmlPayload =
        "<math><annotation-xml encoding=\"text/html\">" +
        "<title><a encoding=\"</title><img src=x onerror=alert(1)>\">" +
        "</annotation-xml></math>";

    private const string SvgStylePayload =
        "<svg><style>&lt;/style>&lt;img src=x onerror=alert(1)></style></svg>";

    [Theory]
    [InlineData(AnnotationXmlPayload)]
    [InlineData("<math><annotation-xml encoding=\"text/html\">" +
                "<style><a encoding=\"</style><img src=x onerror=alert(1)>\">" +
                "</annotation-xml></math>")]
    [InlineData("<math><annotation-xml encoding=\"text/html\">" +
                "<style><img src=x onerror=alert(1)></style></annotation-xml></math>")]
    [InlineData(SvgStylePayload)]
    public void Sanitize_ParserMxssPayload_LeavesNoLiveMarkup(string payload)
    {
        var result = _sut.Sanitize($"<p>before</p>{payload}<p>after</p>");

        result.Should().NotContain("<img").And.NotContain("onerror").And.NotContain("annotation-xml")
            .And.NotContain("<math").And.NotContain("<svg").And.NotContain("<style")
            .And.NotContain("<title");
        result.Should().Contain("<p>before</p>").And.Contain("<p>after</p>",
            "allowed siblings of the stripped vector survive untouched");
    }

    [Fact]
    public void SanitizerParser_TreatsAnnotationXmlTextHtmlAsAnHtmlIntegrationPoint()
    {
        // GHSA-pgww's own PoC check. A browser's parse of this payload has a live <img>; the
        // parser the sanitizer walks must see it too, or the sanitizer is judging a different tree.
        var document = new HtmlParser().ParseDocument(AnnotationXmlPayload);

        document.QuerySelectorAll("img").Should().ContainSingle(
            "AngleSharp >= 1.5.0 parses annotation-xml[encoding=text/html] content as HTML");
    }

    [Fact]
    public void SanitizerParser_RoundTripsSvgStyleTextWithoutMintingElements()
    {
        // GHSA-cgp3's own PoC check: the first parse holds only inert text, so serializing and
        // re-parsing must not produce an element.
        var parser = new HtmlParser();
        var first = parser.ParseDocument(SvgStylePayload);
        first.QuerySelectorAll("img").Should().BeEmpty();

        var reparsed = parser.ParseDocument(first.Body!.InnerHtml);

        reparsed.QuerySelectorAll("img").Should().BeEmpty(
            "AngleSharp >= 1.8.2 escapes an SVG <style>'s text when it serializes it");
    }
}
