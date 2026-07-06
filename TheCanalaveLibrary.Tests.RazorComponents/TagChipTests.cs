using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="TagChip"/> (WU4, updated WU38). The chip injects
/// <see cref="ISpriteReadService"/> and reads a <see cref="ThemeContext"/> cascading value.
/// When <see cref="TagChipDto.SpriteIdentifier"/> is non-null and a <see cref="ThemeContext"/>
/// is cascaded, the chip renders an <c>&lt;img&gt;</c> with the resolved URL and the
/// <c>data-sprite-fallback</c> marker (delegated fallback via img-fallback.js — inline
/// <c>onerror</c> is banned under CSP, security.md). All behaviours are exercisable by
/// constructing the DTO directly.
/// </summary>
public class TagChipTests : BunitContext
{
    private static readonly ThemeContext DefaultTheme = new("pokemon", false);

    public TagChipTests()
    {
        // Register the Core sprite service with the default base URL.
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
    }

    // ── tag name renders ─────────────────────────────────────────────────────────

    [Fact]
    public void TagChip_RendersTagName()
    {
        TagChipDto dto = MakeDto(TagTypeEnum.Genre, "My Genre Tag");

        IRenderedComponent<TagChip> cut = Render<TagChip>(p => p.Add(c => c.Tag, dto));

        cut.Markup.Should().Contain("My Genre Tag");
    }

    // ── type-specific colour classes ─────────────────────────────────────────────

    [Theory]
    [InlineData(TagTypeEnum.Character, "bg-emerald-100")]
    [InlineData(TagTypeEnum.Setting, "bg-violet-100")]
    [InlineData(TagTypeEnum.Genre, "bg-sky-100")]
    [InlineData(TagTypeEnum.ContentWarning, "bg-rose-100")]
    [InlineData(TagTypeEnum.CrossoverFandom, "bg-amber-100")]
    public void TagChip_AppliesCorrectBackgroundClassForTagType(TagTypeEnum type, string expectedClass)
    {
        IRenderedComponent<TagChip> cut = Render<TagChip>(p =>
            p.Add(c => c.Tag, MakeDto(type, $"{type} Tag")));

        string spanClass = cut.Find("span").GetAttribute("class") ?? string.Empty;
        spanClass.Should().Contain(expectedClass,
            $"TagTypeEnum.{type} maps to {expectedClass} in TypeClasses");
    }

    // ── sprite image (requires ThemeContext cascading value) ──────────────────────

    [Fact]
    public void TagChip_WhenSpriteIdentifierIsPresent_AndThemeContextCascaded_RendersImg()
    {
        TagChipDto dto = MakeDto(TagTypeEnum.Character, "Pikachu", spriteIdentifier: "pikachu");

        IRenderedComponent<TagChip> cut = Render<TagChip>(p => p
            .Add(c => c.Tag, dto)
            .AddCascadingValue(DefaultTheme));

        IElement img = cut.Find("img");
        // Optimistic URL: static .png (DefaultTheme.PrefersAnimated = false)
        img.GetAttribute("src").Should().Be("/sprites/themes/pokemon/static/pikachu.png");
    }

    [Fact]
    public void TagChip_WhenPrefersAnimated_RendersAnimatedWebpSrc()
    {
        TagChipDto dto = MakeDto(TagTypeEnum.Character, "Pikachu", spriteIdentifier: "pikachu");
        var animatedTheme = new ThemeContext("pokemon", true);

        IRenderedComponent<TagChip> cut = Render<TagChip>(p => p
            .Add(c => c.Tag, dto)
            .AddCascadingValue(animatedTheme));

        IElement img = cut.Find("img");
        img.GetAttribute("src").Should().Be("/sprites/themes/pokemon/animated/pikachu.webp");
    }

    [Fact]
    public void TagChip_WhenSpriteIdentifierIsPresent_CarriesTheSpriteFallbackMarker()
    {
        TagChipDto dto = MakeDto(TagTypeEnum.Character, "Bulbasaur", spriteIdentifier: "bulbasaur");

        IRenderedComponent<TagChip> cut = Render<TagChip>(p => p
            .Add(c => c.Tag, dto)
            .AddCascadingValue(DefaultTheme));

        IElement img = cut.Find("img");
        // data-sprite-fallback (not inline onerror — banned under CSP, security.md): the
        // delegated img-fallback.js listener drives spriteFallback's webp → png → unknown chain.
        img.HasAttribute("data-sprite-fallback").Should().BeTrue(
            "the fallback chain must be wired up for every sprite img");
        img.GetAttribute("data-static").Should().NotBeNullOrEmpty();
        img.GetAttribute("data-unknown").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TagChip_WhenSpriteIdentifierIsNull_DoesNotRenderImg()
    {
        TagChipDto dto = MakeDto(TagTypeEnum.Character, "Generic Character", spriteIdentifier: null);

        IRenderedComponent<TagChip> cut = Render<TagChip>(p => p
            .Add(c => c.Tag, dto)
            .AddCascadingValue(DefaultTheme));

        cut.FindAll("img").Should().BeEmpty("null SpriteIdentifier → no img element");
    }

    [Fact]
    public void TagChip_WhenNoThemeContextCascaded_DoesNotRenderImg()
    {
        // ThemeContext is nullable — if not cascaded, no img should render even with an identifier.
        TagChipDto dto = MakeDto(TagTypeEnum.Character, "Pikachu", spriteIdentifier: "pikachu");

        IRenderedComponent<TagChip> cut = Render<TagChip>(p =>
            p.Add(c => c.Tag, dto));
        // No AddCascadingValue — ThemeContext will be null.

        cut.FindAll("img").Should().BeEmpty("no ThemeContext → img guard fails → no img rendered");
    }

    // ── description tooltip ──────────────────────────────────────────────────────

    [Fact]
    public void TagChip_RendersDescriptionAsTitle()
    {
        TagChipDto dto = MakeDto(TagTypeEnum.Genre, "Adventure", description: "Action-adventure stories");

        IRenderedComponent<TagChip> cut = Render<TagChip>(p => p.Add(c => c.Tag, dto));

        string title = cut.Find("span").GetAttribute("title") ?? string.Empty;
        title.Should().Be("Action-adventure stories");
    }

    // ── remove button ─────────────────────────────────────────────────────────────

    [Fact]
    public void TagChip_WhenOnRemoveHasDelegate_RendersRemoveButton()
    {
        TagChipDto dto = MakeDto(TagTypeEnum.Genre, "Removable");

        IRenderedComponent<TagChip> cut = Render<TagChip>(p => p
            .Add(c => c.Tag, dto)
            .Add(c => c.OnRemove, EventCallback.Empty));

        cut.FindAll("button").Should().HaveCount(1, "the remove button is present when OnRemove has a delegate");
    }

    [Fact]
    public void TagChip_WhenOnRemoveHasNoDelegate_DoesNotRenderRemoveButton()
    {
        TagChipDto dto = MakeDto(TagTypeEnum.Genre, "Non-removable");

        IRenderedComponent<TagChip> cut = Render<TagChip>(p => p.Add(c => c.Tag, dto));
        // OnRemove not supplied → HasDelegate == false

        cut.FindAll("button").Should().BeEmpty("no OnRemove delegate → no remove button");
    }

    [Fact]
    public async Task TagChip_ClickingRemoveButton_InvokesOnRemove()
    {
        bool invoked = false;
        TagChipDto dto = MakeDto(TagTypeEnum.Genre, "Click Me");

        IRenderedComponent<TagChip> cut = Render<TagChip>(p => p
            .Add(c => c.Tag, dto)
            .Add(c => c.OnRemove, () => { invoked = true; }));

        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        invoked.Should().BeTrue("clicking the remove button must invoke the OnRemove callback");
    }

    // ── helper ───────────────────────────────────────────────────────────────────

    private static TagChipDto MakeDto(
        TagTypeEnum type,
        string name,
        string? spriteIdentifier = null,
        string? description = null) =>
        new()
        {
            TagId = 1,
            TagName = name,
            TagTypeId = type,
            SpriteIdentifier = spriteIdentifier,
            Description = description
        };
}
