using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="GroupCard"/> (WU32).
/// Covers: group name renders; link targets /group/{id}/{slug}; audience badge label; member count;
/// description renders when present; description absent when null.
/// No @inject in GroupCard — no services to register.
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class GroupCardTests : BunitContext
{
    // ── Factory ──────────────────────────────────────────────────────────────────

    private static GroupCardDto MakeGroup(
        int groupId = 1,
        string groupName = "Test Group",
        string? description = null,
        GroupAudienceType audienceType = GroupAudienceType.Standard,
        int memberCount = 5,
        DateTime? dateCreated = null) =>
        new(groupId, groupName, description, audienceType, memberCount,
            dateCreated ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    // ── Renders ──────────────────────────────────────────────────────────────────

    [Fact]
    public void GroupCard_RendersGroupName()
    {
        IRenderedComponent<GroupCard> cut = Render<GroupCard>(p => p
            .Add(c => c.Group, MakeGroup(groupName: "Pokémon Writers")));

        cut.Markup.Should().Contain("Pokémon Writers");
    }

    [Fact]
    public void GroupCard_LinksToGroupDetailPage()
    {
        IRenderedComponent<GroupCard> cut = Render<GroupCard>(p => p
            .Add(c => c.Group, MakeGroup(groupId: 42, groupName: "My Group")));

        // Href should start with /group/42/ (slug is cosmetic, appended by Slugify).
        cut.Find("a").GetAttribute("href").Should().StartWith("/group/42/");
    }

    [Fact]
    public void GroupCard_Standard_ShowsStandardBadge()
    {
        IRenderedComponent<GroupCard> cut = Render<GroupCard>(p => p
            .Add(c => c.Group, MakeGroup(audienceType: GroupAudienceType.Standard)));

        cut.Markup.Should().Contain("Standard", "Standard badge should be visible");
    }

    [Fact]
    public void GroupCard_SfwOnly_ShowsSfwOnlyBadge()
    {
        IRenderedComponent<GroupCard> cut = Render<GroupCard>(p => p
            .Add(c => c.Group, MakeGroup(audienceType: GroupAudienceType.SfwOnly)));

        cut.Markup.Should().Contain("SFW Only");
    }

    [Fact]
    public void GroupCard_Mature_ShowsMatureBadge()
    {
        IRenderedComponent<GroupCard> cut = Render<GroupCard>(p => p
            .Add(c => c.Group, MakeGroup(audienceType: GroupAudienceType.Mature)));

        cut.Markup.Should().Contain("Mature");
    }

    [Fact]
    public void GroupCard_ShowsMemberCount()
    {
        IRenderedComponent<GroupCard> cut = Render<GroupCard>(p => p
            .Add(c => c.Group, MakeGroup(memberCount: 12)));

        cut.Markup.Should().Contain("12 members");
    }

    [Fact]
    public void GroupCard_OneMember_ShowsSingular()
    {
        IRenderedComponent<GroupCard> cut = Render<GroupCard>(p => p
            .Add(c => c.Group, MakeGroup(memberCount: 1)));

        cut.Markup.Should().Contain("1 member", "singular form for exactly one member");
        cut.Markup.Should().NotContain("1 members");
    }

    [Fact]
    public void GroupCard_WithDescription_RendersDescription()
    {
        IRenderedComponent<GroupCard> cut = Render<GroupCard>(p => p
            .Add(c => c.Group, MakeGroup(description: "A great group for fans.")));

        cut.Markup.Should().Contain("A great group for fans.");
    }

    [Fact]
    public void GroupCard_NullDescription_DoesNotRenderDescriptionElement()
    {
        IRenderedComponent<GroupCard> cut = Render<GroupCard>(p => p
            .Add(c => c.Group, MakeGroup(description: null)));

        // No <p> element with empty or whitespace content for description.
        cut.FindAll("p").Should().BeEmpty("no description paragraph when Description is null");
    }
}
