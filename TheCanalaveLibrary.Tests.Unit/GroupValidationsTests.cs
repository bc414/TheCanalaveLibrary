using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="CreateGroupDtoValidations"/> and
/// <see cref="UpdateGroupDtoValidations"/> (WU32). Mirrors <see cref="BlogPostValidationsTests"/>.
/// Tier: Unit (directly constructed, Core-only types).
/// </summary>
public class GroupValidationsTests
{
    // ── CreateGroupDto ────────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidDto_ReturnsNoErrors()
    {
        var dto = new CreateGroupDto { GroupName = "Pokémon Fic Writers" };
        dto.CanSave().Should().BeEmpty();
    }

    [Fact]
    public void Create_EmptyName_ReturnsError()
    {
        var dto = new CreateGroupDto { GroupName = "" };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("name");
    }

    [Fact]
    public void Create_WhitespaceNameOnly_ReturnsError()
    {
        var dto = new CreateGroupDto { GroupName = "   " };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("name");
    }

    [Fact]
    public void Create_NameExceedsMaxLength_ReturnsError()
    {
        var dto = new CreateGroupDto { GroupName = new string('A', GroupConstants.MaxGroupNameLength + 1) };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("name");
    }

    [Fact]
    public void Create_NameAtMaxLength_ReturnsNoErrors()
    {
        var dto = new CreateGroupDto { GroupName = new string('A', GroupConstants.MaxGroupNameLength) };
        dto.CanSave().Should().BeEmpty();
    }

    [Fact]
    public void Create_DescriptionExceedsMaxLength_ReturnsError()
    {
        var dto = new CreateGroupDto
        {
            GroupName   = "Valid Name",
            Description = new string('x', GroupConstants.MaxDescriptionLength + 1)
        };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("Description");
    }

    [Fact]
    public void Create_DescriptionAtMaxLength_ReturnsNoErrors()
    {
        var dto = new CreateGroupDto
        {
            GroupName   = "Valid Name",
            Description = new string('x', GroupConstants.MaxDescriptionLength)
        };
        dto.CanSave().Should().BeEmpty();
    }

    [Fact]
    public void Create_NullDescription_ReturnsNoErrors()
    {
        var dto = new CreateGroupDto { GroupName = "My Group", Description = null };
        dto.CanSave().Should().BeEmpty();
    }

    // ── UpdateGroupDto ────────────────────────────────────────────────────────

    [Fact]
    public void Update_ValidDto_ReturnsNoErrors()
    {
        var dto = new UpdateGroupDto { GroupId = 1, GroupName = "Updated Name" };
        dto.CanSave().Should().BeEmpty();
    }

    [Fact]
    public void Update_EmptyName_ReturnsError()
    {
        var dto = new UpdateGroupDto { GroupId = 1, GroupName = "" };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("name");
    }

    [Fact]
    public void Update_NameExceedsMaxLength_ReturnsError()
    {
        var dto = new UpdateGroupDto
        {
            GroupId   = 1,
            GroupName = new string('A', GroupConstants.MaxGroupNameLength + 1)
        };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("name");
    }
}
