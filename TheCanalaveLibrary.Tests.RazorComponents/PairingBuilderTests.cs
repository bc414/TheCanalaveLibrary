using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render and interaction tests for <see cref="PairingBuilder"/> (WU37 Phase 5).
/// No @inject — no DI setup needed.
/// Tier: RazorComponents (bUnit).
/// </summary>
public class PairingBuilderTests : TestContext
{
    private static IReadOnlyList<TagChipDto> TwoChips() =>
    [
        new TagChipDto { TagId = 1, TagName = "Pikachu", TagTypeId = TagTypeEnum.Character },
        new TagChipDto { TagId = 2, TagName = "Eevee",   TagTypeId = TagTypeEnum.Character }
    ];

    [Fact]
    public void PairingBuilder_WithZeroCharacters_ShowsNoAddUI()
    {
        IRenderedComponent<PairingBuilder> cut = RenderComponent<PairingBuilder>(p =>
        {
            p.Add(c => c.CharacterChips, []);
            p.Add(c => c.Pairings, []);
        });

        cut.Markup.Should().NotContain("Add Pairing", "add UI must be hidden when fewer than 2 characters");
    }

    [Fact]
    public void PairingBuilder_WithOneCharacter_ShowsHintNotAddUI()
    {
        IRenderedComponent<PairingBuilder> cut = RenderComponent<PairingBuilder>(p =>
        {
            p.Add(c => c.CharacterChips, [new TagChipDto { TagId = 1, TagName = "Pikachu", TagTypeId = TagTypeEnum.Character }]);
            p.Add(c => c.Pairings, []);
        });

        cut.Markup.Should().Contain("at least 2 characters", "hint text must appear with only 1 character");
        cut.Markup.Should().NotContain("Add Pairing");
    }

    [Fact]
    public void PairingBuilder_WithTwoCharacters_ShowsAddUI()
    {
        IRenderedComponent<PairingBuilder> cut = RenderComponent<PairingBuilder>(p =>
        {
            p.Add(c => c.CharacterChips, TwoChips());
            p.Add(c => c.Pairings, []);
        });

        cut.Markup.Should().Contain("Add Pairing", "add UI must appear when 2+ characters present");
    }

    [Fact]
    public void PairingBuilder_WithExistingPairing_RendersMemberNames()
    {
        IReadOnlyList<StoryCharacterPairingDto> pairings =
        [
            new StoryCharacterPairingDto
            {
                PairingType = CharacterPairingType.Romantic,
                Priority = TagPriority.Primary,
                MemberCharacterTagIds = [1, 2]
            }
        ];

        IRenderedComponent<PairingBuilder> cut = RenderComponent<PairingBuilder>(p =>
        {
            p.Add(c => c.CharacterChips, TwoChips());
            p.Add(c => c.Pairings, pairings);
        });

        cut.Markup.Should().Contain("Pikachu", "first member name must be shown");
        cut.Markup.Should().Contain("Eevee", "second member name must be shown");
    }

    [Fact]
    public async Task PairingBuilder_RemovePairing_FiresOnPairingsChangedWithFewerPairings()
    {
        List<StoryCharacterPairingDto>? received = null;
        IReadOnlyList<StoryCharacterPairingDto> pairings =
        [
            new StoryCharacterPairingDto
            {
                PairingType = CharacterPairingType.Romantic,
                MemberCharacterTagIds = [1, 2]
            }
        ];

        IRenderedComponent<PairingBuilder> cut = RenderComponent<PairingBuilder>(p =>
        {
            p.Add(c => c.CharacterChips, TwoChips());
            p.Add(c => c.Pairings, pairings);
            p.Add(c => c.OnPairingsChanged,
                Microsoft.AspNetCore.Components.EventCallback.Factory.Create<List<StoryCharacterPairingDto>>(
                    this, list => received = list));
        });

        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        received.Should().NotBeNull();
        received!.Should().BeEmpty("removing the only pairing must result in an empty list");
    }

    [Fact]
    public void PairingBuilder_AddButton_DisabledWhenNoMembersSelected()
    {
        IRenderedComponent<PairingBuilder> cut = RenderComponent<PairingBuilder>(p =>
        {
            p.Add(c => c.CharacterChips, TwoChips());
            p.Add(c => c.Pairings, []);
        });

        // The "Add Pairing" submit button (last button) should be disabled initially
        var buttons = cut.FindAll("button");
        var addButton = buttons.Last(b => b.TextContent.Contains("Add Pairing"));
        addButton.HasAttribute("disabled").Should().BeTrue("Add Pairing must be disabled until ≥2 members selected");
    }
}
