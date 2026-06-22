using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="TagChip"/> (WU4). The chip is a pure leaf — it injects no service,
/// takes a <see cref="TagChipDto"/> and an optional <see cref="EventCallback"/> OnRemove, and
/// renders the tag name, a type-coloured badge, an optional sprite image, and an optional remove
/// button. All behaviours are exercisable by constructing the DTO directly.
/// </summary>
public class TagChipTests : TestContext
{
    // ── tag name renders ─────────────────────────────────────────────────────────

    [Fact]
    public void TagChip_RendersTagName()
    {
        TagChipDto dto = MakeDto(TagTypeEnum.Genre, "My Genre Tag");

        IRenderedComponent<TagChip> cut = RenderComponent<TagChip>(p => p.Add(c => c.Tag, dto));

        cut.Markup.Should().Contain("My Genre Tag");
    }

    // ── type-specific colour classes ─────────────────────────────────────────────

    [Theory]
    [InlineData(TagTypeEnum.Character, "bg-emerald-100")]
    [InlineData(TagTypeEnum.Setting, "bg-violet-100")]
    [InlineData(TagTypeEnum.Genre, "bg-sky-100")]
    [InlineData(TagTypeEnum.ContentWarning, "bg-rose-100")]
    [InlineData(TagTypeEnum.CrossoverFandom, "bg-amber-100")]
    [InlineData(TagTypeEnum.Relationship, "bg-pink-100")]
    public void TagChip_AppliesCorrectBackgroundClassForTagType(TagTypeEnum type, string expectedClass)
    {
        IRenderedComponent<TagChip> cut = RenderComponent<TagChip>(p =>
            p.Add(c => c.Tag, MakeDto(type, $"{type} Tag")));

        string spanClass = cut.Find("span").GetAttribute("class") ?? string.Empty;
        spanClass.Should().Contain(expectedClass,
            $"TagTypeEnum.{type} maps to {expectedClass} in TypeClasses");
    }

    // ── sprite image ─────────────────────────────────────────────────────────────

    [Fact]
    public void TagChip_WhenSpriteUrlIsPresent_RendersImg()
    {
        TagChipDto dto = MakeDto(TagTypeEnum.Character, "Pikachu", spriteUrl: "/sprites/pikachu.png");

        IRenderedComponent<TagChip> cut = RenderComponent<TagChip>(p => p.Add(c => c.Tag, dto));

        IElement img = cut.Find("img");
        img.GetAttribute("src").Should().Be("/sprites/pikachu.png");
    }

    [Fact]
    public void TagChip_WhenSpriteUrlIsNull_DoesNotRenderImg()
    {
        TagChipDto dto = MakeDto(TagTypeEnum.Character, "Generic Character", spriteUrl: null);

        IRenderedComponent<TagChip> cut = RenderComponent<TagChip>(p => p.Add(c => c.Tag, dto));

        cut.FindAll("img").Should().BeEmpty("no sprite URL → no img element");
    }

    // ── description tooltip ──────────────────────────────────────────────────────

    [Fact]
    public void TagChip_RendersDescriptionAsTitle()
    {
        TagChipDto dto = MakeDto(TagTypeEnum.Genre, "Adventure", description: "Action-adventure stories");

        IRenderedComponent<TagChip> cut = RenderComponent<TagChip>(p => p.Add(c => c.Tag, dto));

        string title = cut.Find("span").GetAttribute("title") ?? string.Empty;
        title.Should().Be("Action-adventure stories");
    }

    // ── remove button ─────────────────────────────────────────────────────────────

    [Fact]
    public void TagChip_WhenOnRemoveHasDelegate_RendersRemoveButton()
    {
        TagChipDto dto = MakeDto(TagTypeEnum.Genre, "Removable");

        IRenderedComponent<TagChip> cut = RenderComponent<TagChip>(p => p
            .Add(c => c.Tag, dto)
            .Add(c => c.OnRemove, EventCallback.Empty));

        cut.FindAll("button").Should().HaveCount(1, "the remove button is present when OnRemove has a delegate");
    }

    [Fact]
    public void TagChip_WhenOnRemoveHasNoDelegate_DoesNotRenderRemoveButton()
    {
        TagChipDto dto = MakeDto(TagTypeEnum.Genre, "Non-removable");

        IRenderedComponent<TagChip> cut = RenderComponent<TagChip>(p => p.Add(c => c.Tag, dto));
        // OnRemove not supplied → HasDelegate == false

        cut.FindAll("button").Should().BeEmpty("no OnRemove delegate → no remove button");
    }

    [Fact]
    public async Task TagChip_ClickingRemoveButton_InvokesOnRemove()
    {
        bool invoked = false;
        TagChipDto dto = MakeDto(TagTypeEnum.Genre, "Click Me");

        IRenderedComponent<TagChip> cut = RenderComponent<TagChip>(p => p
            .Add(c => c.Tag, dto)
            .Add(c => c.OnRemove, () => { invoked = true; }));

        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        invoked.Should().BeTrue("clicking the remove button must invoke the OnRemove callback");
    }

    // ── helper ───────────────────────────────────────────────────────────────────

    private static TagChipDto MakeDto(
        TagTypeEnum type,
        string name,
        string? spriteUrl = null,
        string? description = null) =>
        new()
        {
            TagId = 1,
            TagName = name,
            TagTypeId = type,
            SpriteUrl = spriteUrl,
            Description = description
        };
}
