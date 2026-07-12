using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="TreeSearchResultBadge"/> (WU44; PathHops added by WU40's privacy
/// correction, 2026-07-12). Covers degree-label wording, the ids-only raw-path fallback, and the
/// hydrated hop rendering: real usernames/titles as links on chain-of-trust paths, opaque #id
/// for hops the viewer cannot see. Tier: RazorComponents (bUnit).
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
    public void RawPathFallback_RendersStoryHopsOnly_ByIds()
    {
        // Ids-only fallback when no hydrated PathHops are supplied — (t,1) story, (f,99) user,
        // (t,2) story: with no labels available, the user hop stays unrendered (nothing useful
        // to show for it without hydration).
        IRenderedComponent<TreeSearchResultBadge> cut = Render<TreeSearchResultBadge>(p => p
            .Add(c => c.Degree, 2)
            .Add(c => c.Path, """{"(t,1)","(f,99)","(t,2)"}"""));

        cut.Markup.Should().Contain("#1").And.Contain("#2");
        cut.Markup.Should().NotContain("#99");
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

    // ── Hydrated PathHops (WU40 privacy correction, 2026-07-12) ─────────────────────────

    [Fact]
    public void PathHops_RenderUsernamesAndStoryTitles_AsLinks()
    {
        // Chain-of-trust paths carry no anonymized contributor — user hops render REAL
        // usernames (the WU44-era collapse-user-hops behavior was over-anonymization).
        IRenderedComponent<TreeSearchResultBadge> cut = Render<TreeSearchResultBadge>(p => p
            .Add(c => c.Degree, 2)
            .Add(c => c.PathHops, new List<TreeSearchPathHopDto>
            {
                new(true, 1, "Root Story"),
                new(false, 99, "GemmerUser"),
                new(true, 2, "The Gem"),
            }));

        cut.Markup.Should().Contain("GemmerUser", "user hops carry real identity on chain-of-trust paths");
        cut.Markup.Should().Contain("Root Story").And.Contain("The Gem");
        cut.FindAll("a[href='/user/99']").Should().ContainSingle("hops are clickable links");
        cut.FindAll("a[href='/story/2']").Should().ContainSingle();
    }

    [Fact]
    public void PathHops_UnlabeledHop_StaysOpaqueId()
    {
        // A rating-gated bridge story yields no label server-side — the silent-bridge rule
        // holds for labels: the hop renders as an unlinked #id.
        IRenderedComponent<TreeSearchResultBadge> cut = Render<TreeSearchResultBadge>(p => p
            .Add(c => c.Degree, 2)
            .Add(c => c.PathHops, new List<TreeSearchPathHopDto>
            {
                new(true, 1, "Root Story"),
                new(true, 7, null),
                new(true, 2, "The Gem"),
            }));

        cut.Markup.Should().Contain("#7");
        cut.FindAll("a[href='/story/7']").Should().BeEmpty("an invisible-to-viewer hop is never a link");
    }
}
