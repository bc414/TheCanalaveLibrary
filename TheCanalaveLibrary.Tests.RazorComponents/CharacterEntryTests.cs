using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render and interaction tests for <see cref="CharacterEntry"/> (WU37 Phase 5, updated WU38).
/// CharacterEntry injects ISpriteReadService for sprite URL resolution.
/// Tier: RazorComponents (bUnit).
/// </summary>
public class CharacterEntryTests : BunitContext
{
    public CharacterEntryTests()
    {
        // CharacterEntry injects ISpriteReadService for sprite URL resolution.
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
    }

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
        IRenderedComponent<CharacterEntry> cut = Render<CharacterEntry>(p =>
        {
            p.Add(c => c.Chip, MakeChip());
            p.Add(c => c.Dto, MakeDto());
        });

        cut.Markup.Should().Contain("Pikachu");
    }

    [Fact]
    public void CharacterEntry_OcToggle_Shown_WhenAllowOcDetailsTrue()
    {
        IRenderedComponent<CharacterEntry> cut = Render<CharacterEntry>(p =>
        {
            p.Add(c => c.Chip, MakeChip(allowOc: true));
            p.Add(c => c.Dto, MakeDto());
        });

        cut.FindAll("input[type='checkbox']").Should().NotBeEmpty("OC checkbox must be shown when AllowOCDetails=true");
    }

    [Fact]
    public void CharacterEntry_OcFields_Shown_WhenIsOcTrue()
    {
        IRenderedComponent<CharacterEntry> cut = Render<CharacterEntry>(p =>
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
        IRenderedComponent<CharacterEntry> cut = Render<CharacterEntry>(p =>
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
        IRenderedComponent<CharacterEntry> cut = Render<CharacterEntry>(p =>
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
