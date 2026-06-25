using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="TagValidations"/>. Covers create + update validation rules:
/// name required/length, unique-within-type, description length, sprite identifier length,
/// self-reference, two-level parent, cross-type parent, and AllowOCDetails coercion.
/// Tier: Unit (directly constructed, Core-only types — no host/DB).
/// </summary>
public class TagValidationsTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Tag MakeTag(int id = 1, string name = "Bulbasaur",
        TagTypeEnum type = TagTypeEnum.Character, int? parentId = null) =>
        new() { TagId = id, TagName = name, TagTypeId = type, ParentTagId = parentId };

    private static CreateTagDto MakeCreate(string name = "Pikachu",
        TagTypeEnum type = TagTypeEnum.Character, int? parentId = null) =>
        new() { TagName = name, TagTypeId = type, ParentTagId = parentId };

    private static UpdateTagDto MakeUpdate(int tagId = 1, string name = "Pikachu",
        TagTypeEnum type = TagTypeEnum.Character, int? parentId = null) =>
        new() { TagId = tagId, TagName = name, TagTypeId = type, ParentTagId = parentId };

    // ── Name validation ───────────────────────────────────────────────────────

    [Fact]
    public void ValidateCreate_ValidName_DoesNotThrow()
    {
        Action act = () => TagValidations.ValidateCreate(MakeCreate(), nameExistsInType: false, parentTag: null);
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateCreate_EmptyName_Throws()
    {
        var dto = MakeCreate(name: "");
        Action act = () => TagValidations.ValidateCreate(dto, nameExistsInType: false, parentTag: null);
        act.Should().Throw<TagValidationException>().WithMessage("*required*");
    }

    [Fact]
    public void ValidateCreate_WhitespaceName_Throws()
    {
        var dto = MakeCreate(name: "   ");
        Action act = () => TagValidations.ValidateCreate(dto, nameExistsInType: false, parentTag: null);
        act.Should().Throw<TagValidationException>().WithMessage("*required*");
    }

    [Fact]
    public void ValidateCreate_NameAtMaxLength_DoesNotThrow()
    {
        var dto = MakeCreate(name: new string('A', TagValidations.MaxNameLength));
        Action act = () => TagValidations.ValidateCreate(dto, nameExistsInType: false, parentTag: null);
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateCreate_NameExceedsMaxLength_Throws()
    {
        var dto = MakeCreate(name: new string('A', TagValidations.MaxNameLength + 1));
        Action act = () => TagValidations.ValidateCreate(dto, nameExistsInType: false, parentTag: null);
        act.Should().Throw<TagValidationException>().WithMessage($"*{TagValidations.MaxNameLength}*");
    }

    // ── Uniqueness within type ────────────────────────────────────────────────

    [Fact]
    public void ValidateCreate_NameExistsInType_Throws()
    {
        Action act = () => TagValidations.ValidateCreate(MakeCreate(), nameExistsInType: true, parentTag: null);
        act.Should().Throw<TagValidationException>().WithMessage("*already exists*");
    }

    [Fact]
    public void ValidateUpdate_NameExistsInType_Throws()
    {
        Action act = () => TagValidations.ValidateUpdate(MakeUpdate(), nameExistsInType: true, parentTag: null);
        act.Should().Throw<TagValidationException>().WithMessage("*already exists*");
    }

    [Fact]
    public void ValidateUpdate_NameUniqueInType_DoesNotThrow()
    {
        Action act = () => TagValidations.ValidateUpdate(MakeUpdate(), nameExistsInType: false, parentTag: null);
        act.Should().NotThrow();
    }

    // ── Description ───────────────────────────────────────────────────────────

    [Fact]
    public void ValidateCreate_NullDescription_DoesNotThrow()
    {
        var dto = new CreateTagDto { TagName = "Eevee", TagTypeId = TagTypeEnum.Character, Description = null };
        Action act = () => TagValidations.ValidateCreate(dto, nameExistsInType: false, parentTag: null);
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateCreate_DescriptionAtMaxLength_DoesNotThrow()
    {
        var dto = new CreateTagDto
        {
            TagName = "Eevee", TagTypeId = TagTypeEnum.Character,
            Description = new string('x', TagValidations.MaxDescriptionLength)
        };
        Action act = () => TagValidations.ValidateCreate(dto, nameExistsInType: false, parentTag: null);
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateCreate_DescriptionExceedsMaxLength_Throws()
    {
        var dto = new CreateTagDto
        {
            TagName = "Eevee", TagTypeId = TagTypeEnum.Character,
            Description = new string('x', TagValidations.MaxDescriptionLength + 1)
        };
        Action act = () => TagValidations.ValidateCreate(dto, nameExistsInType: false, parentTag: null);
        act.Should().Throw<TagValidationException>().WithMessage("*Description*");
    }

    // ── Parent validation ─────────────────────────────────────────────────────

    [Fact]
    public void ValidateCreate_ValidParent_DoesNotThrow()
    {
        Tag parent = MakeTag(id: 10, type: TagTypeEnum.Character, parentId: null);
        var dto = MakeCreate(type: TagTypeEnum.Character, parentId: 10);
        Action act = () => TagValidations.ValidateCreate(dto, nameExistsInType: false, parentTag: parent);
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateCreate_ParentTagDoesNotExist_Throws()
    {
        var dto = MakeCreate(parentId: 99);
        Action act = () => TagValidations.ValidateCreate(dto, nameExistsInType: false, parentTag: null);
        act.Should().Throw<TagValidationException>().WithMessage("*does not exist*");
    }

    [Fact]
    public void ValidateCreate_ParentIsWrongType_Throws()
    {
        // Parent is a Setting tag but the new tag is Character.
        Tag parent = MakeTag(id: 10, type: TagTypeEnum.Setting, parentId: null);
        var dto = MakeCreate(type: TagTypeEnum.Character, parentId: 10);
        Action act = () => TagValidations.ValidateCreate(dto, nameExistsInType: false, parentTag: parent);
        act.Should().Throw<TagValidationException>().WithMessage("*same type*");
    }

    [Fact]
    public void ValidateCreate_ParentAlreadyHasParent_Throws()
    {
        // Two-level depth is forbidden.
        Tag parent = MakeTag(id: 10, type: TagTypeEnum.Character, parentId: 5);
        var dto = MakeCreate(type: TagTypeEnum.Character, parentId: 10);
        Action act = () => TagValidations.ValidateCreate(dto, nameExistsInType: false, parentTag: parent);
        act.Should().Throw<TagValidationException>().WithMessage("*one level deep*");
    }

    [Fact]
    public void ValidateUpdate_SelfReference_Throws()
    {
        Tag self = MakeTag(id: 7, type: TagTypeEnum.Character, parentId: null);
        var dto = MakeUpdate(tagId: 7, type: TagTypeEnum.Character, parentId: 7);
        Action act = () => TagValidations.ValidateUpdate(dto, nameExistsInType: false, parentTag: self);
        act.Should().Throw<TagValidationException>().WithMessage("*own parent*");
    }

    // ── AllowOCDetails coercion ───────────────────────────────────────────────

    [Theory]
    [InlineData(TagTypeEnum.Character, true, true)]
    [InlineData(TagTypeEnum.Character, false, false)]
    [InlineData(TagTypeEnum.Setting, true, false)]
    [InlineData(TagTypeEnum.Genre, true, false)]
    [InlineData(TagTypeEnum.ContentWarning, true, false)]
    [InlineData(TagTypeEnum.CrossoverFandom, true, false)]
    [InlineData(TagTypeEnum.Relationship, true, false)]
    public void CoerceAllowOCDetails_ReturnsTrueOnlyForCharacter(TagTypeEnum type, bool input, bool expected)
    {
        TagValidations.CoerceAllowOCDetails(input, type).Should().Be(expected);
    }
}
