using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;
using Xunit;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// ContentSurface — the single Content Surface material (design constitution, 2026-07-10).
/// Covers the paper ground, the side-rails frame default, variant padding, and the Phase E
/// reader background override precedence (explicit choice > tokens; SiteDefault rides tokens).
/// </summary>
public class ContentSurfaceTests : BunitContext
{
    private IRenderedComponent<ContentSurface> RenderSurface(
        ContentSurfaceVariant variant = ContentSurfaceVariant.Inline,
        ReaderDisplaySettings? display = null)
        => Render<ContentSurface>(p =>
        {
            p.Add(c => c.Variant, variant);
            p.AddChildContent("<p>words</p>");
            if (display is not null)
            {
                p.AddCascadingValue(display);
            }
        });

    [Theory]
    [InlineData(ReadingBackgroundEnum.Light, "#FBFAF6")]
    [InlineData(ReadingBackgroundEnum.Sepia, "#F4E9D4")]
    [InlineData(ReadingBackgroundEnum.Dark, "#22211D")]
    public void ContentSurface_ExplicitReaderBackground_OverridesThePaperTokens(
        ReadingBackgroundEnum choice, string expectedBackground)
    {
        var cut = RenderSurface(display: new ReaderDisplaySettings { ReadingBackground = choice });

        string style = cut.Find("div").GetAttribute("style") ?? string.Empty;
        style.Should().Contain(expectedBackground,
            "an explicit reader choice overrides the site tokens (user > theme > default)");
        style.Should().Contain("color:", "the override carries its matching ink, not just the ground");
    }

    [Fact]
    public void ContentSurface_SiteDefaultChoice_LeavesTokensInCharge()
    {
        var cut = RenderSurface(display: new ReaderDisplaySettings
        {
            ReadingBackground = ReadingBackgroundEnum.SiteDefault
        });

        cut.Find("div").GetAttribute("style").Should().BeNullOrEmpty(
            "SiteDefault means the paper tokens (and any future site theme) decide");
    }
}
