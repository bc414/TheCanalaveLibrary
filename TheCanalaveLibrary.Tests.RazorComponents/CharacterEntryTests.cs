using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render and interaction tests for <see cref="CharacterEntry"/> (WU37 Phase 5).
/// No @inject — no DI setup needed.
/// Tier: RazorComponents (bUnit).
/// </summary>
public class CharacterEntryTests : TestContext
{
    private static TagChipDto MakeChip(bool allowOc = false) => new()
    {
        TagId = 1,
        TagName = "Pikachu",
        TagTypeId = TagTypeEnum.Character,
        AllowOCDetails = allowOc
    };

    private static StoryCharacterDto MakeDto(bool isOc = false) => new()
    {
        CharacterTagId = 1,
        Priority = TagPriority.Primary,
        IsOc = isOc,
        OcName = isOc ? "Volt" : null,
        OcBio = isOc ? "Friendly Pikachu" : null
    };

    [Fact]
    public void CharacterEntry_Renders_ChipName()
    {
        IRenderedComponent<CharacterEntry> cut = RenderComponent<CharacterEntry>(p =>
        {
            p.Add(c => c.Chip, MakeChip());
            p.Add(c => c.Dto, MakeDto());
        });

        cut.Markup.Should().Contain("Pikachu");
    }

    [Fact]
    public void CharacterEntry_Renders_PrioritySelect()
    {
        IRenderedComponent<CharacterEntry> cut = RenderComponent<CharacterEntry>(p =>
        {
            p.Add(c => c.Chip, MakeChip());
            p.Add(c => c.Dto, MakeDto());
        });

        cut.FindAll("select").Should().NotBeEmpty("priority dropdown must be present");
    }

    [Fact]
    public void CharacterEntry_OcToggle_NotShown_WhenAllowOcDetailsFalse()
    {
        IRenderedComponent<CharacterEntry> cut = RenderComponent<CharacterEntry>(p =>
        {
            p.Add(c => c.Chip, MakeChip(allowOc: false));
            p.Add(c => c.Dto, MakeDto());
        });

        cut.FindAll("input[type='checkbox']").Should().BeEmpty("OC checkbox must be hidden when AllowOCDetails=false");
    }

    [Fact]
    public void CharacterEntry_OcToggle_Shown_WhenAllowOcDetailsTrue()
    {
        IRenderedComponent<CharacterEntry> cut = RenderComponent<CharacterEntry>(p =>
        {
            p.Add(c => c.Chip, MakeChip(allowOc: true));
            p.Add(c => c.Dto, MakeDto());
        });

        cut.FindAll("input[type='checkbox']").Should().NotBeEmpty("OC checkbox must be shown when AllowOCDetails=true");
    }

    [Fact]
    public void CharacterEntry_OcFields_NotShown_WhenIsOcFalse()
    {
        IRenderedComponent<CharacterEntry> cut = RenderComponent<CharacterEntry>(p =>
        {
            p.Add(c => c.Chip, MakeChip(allowOc: true));
            p.Add(c => c.Dto, MakeDto(isOc: false));
        });

        // OC name placeholder should not appear
        cut.Markup.Should().NotContain("OC name", "OC text inputs must be hidden when IsOc=false");
    }

    [Fact]
    public void CharacterEntry_OcFields_Shown_WhenIsOcTrue()
    {
        IRenderedComponent<CharacterEntry> cut = RenderComponent<CharacterEntry>(p =>
        {
            p.Add(c => c.Chip, MakeChip(allowOc: true));
            p.Add(c => c.Dto, MakeDto(isOc: true));
        });

        cut.Markup.Should().Contain("OC name", "OC name input must be shown when IsOc=true");
        cut.Markup.Should().Contain("Volt", "current OcName value must be rendered");
    }

    [Fact]
    public async Task CharacterEntry_RemoveButton_FiresOnRemoved()
    {
        bool fired = false;
        IRenderedComponent<CharacterEntry> cut = RenderComponent<CharacterEntry>(p =>
        {
            p.Add(c => c.Chip, MakeChip());
            p.Add(c => c.Dto, MakeDto());
            p.Add(c => c.OnRemoved, Microsoft.AspNetCore.Components.EventCallback.Factory.Create(this, () => fired = true));
        });

        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        fired.Should().BeTrue("clicking Remove must fire OnRemoved");
    }

    [Fact]
    public async Task CharacterEntry_PriorityChange_FiresOnChangedWithNewPriority()
    {
        StoryCharacterDto? received = null;
        IRenderedComponent<CharacterEntry> cut = RenderComponent<CharacterEntry>(p =>
        {
            p.Add(c => c.Chip, MakeChip());
            p.Add(c => c.Dto, MakeDto());
            p.Add(c => c.OnChanged, Microsoft.AspNetCore.Components.EventCallback.Factory.Create<StoryCharacterDto>(this, dto => received = dto));
        });

        await cut.Find("select").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "Supporting" });

        received.Should().NotBeNull();
        received!.Priority.Should().Be(TagPriority.Supporting);
    }
}
