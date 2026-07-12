using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="StoryLineageBox"/> (Feature 10, WU42) — the public "story lineage"
/// display box on the story page. No @inject — data arrives fully computed via
/// <see cref="StoryLineageDto"/> (the component trusts the read service's Approved-only,
/// content-rating-filtered result; it does no filtering of its own).
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class StoryLineageBoxTests : BunitContext
{
    [Fact]
    public void StoryLineageBox_WithLinks_RendersEachLink()
    {
        IReadOnlyList<StoryLineageDto> links =
        [
            new(3, "Sequel", 42, "Ashes of Kanto"),
            new(1, "Inspired By", 7, "Dawn of Sinnoh")
        ];

        IRenderedComponent<StoryLineageBox> cut = Render<StoryLineageBox>(p => p
            .Add(c => c.Links, links));

        cut.Markup.Should().Contain("Sequel");
        cut.Markup.Should().Contain("Ashes of Kanto");
        cut.Markup.Should().Contain("Inspired By");
        cut.Markup.Should().Contain("Dawn of Sinnoh");
    }

    [Fact]
    public void StoryLineageBox_WithLinks_RendersTargetHrefs()
    {
        IReadOnlyList<StoryLineageDto> links = [new(3, "Sequel", 42, "Ashes of Kanto")];

        IRenderedComponent<StoryLineageBox> cut = Render<StoryLineageBox>(p => p
            .Add(c => c.Links, links));

        cut.FindAll("a").Should().Contain(a => a.GetAttribute("href") == "/story/42");
    }

    [Fact]
    public void StoryLineageBox_Empty_RendersNothing()
    {
        IRenderedComponent<StoryLineageBox> cut = Render<StoryLineageBox>(p => p
            .Add(c => c.Links, Array.Empty<StoryLineageDto>()));

        cut.Markup.Should().BeNullOrWhiteSpace();
    }

    [Fact]
    public void StoryLineageBox_MultipleLinks_RendersOneListItemEach()
    {
        IReadOnlyList<StoryLineageDto> links =
        [
            new(3, "Sequel", 42, "Ashes of Kanto"),
            new(2, "Prequel", 7, "Dawn of Sinnoh"),
            new(4, "Companion Piece", 9, "Twilight of Hoenn")
        ];

        IRenderedComponent<StoryLineageBox> cut = Render<StoryLineageBox>(p => p
            .Add(c => c.Links, links));

        cut.FindAll("li").Should().HaveCount(3);
    }
}
