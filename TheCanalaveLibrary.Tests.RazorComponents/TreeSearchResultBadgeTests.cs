using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="TreeSearchResultBadge"/> (WU44). Covers degree-label wording and
/// the path chip's presence/absence, and that it never surfaces a user-typed node (privacy model,
/// spec §5.4) — only story hops appear. Tier: RazorComponents (bUnit).
/// </summary>
public class TreeSearchResultBadgeTests : BunitContext
{
    [Theory]
    [InlineData(1, "1st-degree connection")]
    [InlineData(2, "2nd-degree connection")]
    [InlineData(3, "3rd-degree connection")]
    [InlineData(4, "4th-degree connection")]
    [InlineData(8, "8th-degree connection")]
    public void RendersCorrectDegreeLabel(int degree, string expected)
    {
        IRenderedComponent<TreeSearchResultBadge> cut = Render<TreeSearchResultBadge>(p => p
            .Add(c => c.Degree, degree));

        cut.Markup.Should().Contain(expected);
    }

    [Fact]
    public void NullPath_RendersNoPathChip()
    {
        IRenderedComponent<TreeSearchResultBadge> cut = Render<TreeSearchResultBadge>(p => p
            .Add(c => c.Degree, 2));

        cut.FindAll("span").Should().HaveCount(1, "only the degree badge renders when Path is null");
    }

    [Fact]
    public void ChainOfTrustPath_RendersStoryHopsOnly_NeverUserIds()
    {
        // (t,1) story, (f,99) user, (t,2) story — the user hop (99) must never appear in the markup.
        IRenderedComponent<TreeSearchResultBadge> cut = Render<TreeSearchResultBadge>(p => p
            .Add(c => c.Degree, 2)
            .Add(c => c.Path, """{"(t,1)","(f,99)","(t,2)"}"""));

        cut.Markup.Should().Contain("#1").And.Contain("#2");
        cut.Markup.Should().NotContain("#99", "user-typed hops must never surface an id — privacy model §5.4");
    }

    [Fact]
    public void PathWithOnlyOneStoryHop_RendersNoPathChip()
    {
        // A single-story path (root itself, no intermediate) has nothing meaningful to show.
        IRenderedComponent<TreeSearchResultBadge> cut = Render<TreeSearchResultBadge>(p => p
            .Add(c => c.Degree, 1)
            .Add(c => c.Path, """{"(t,5)"}"""));

        cut.FindAll("span").Should().HaveCount(1);
    }
}
