using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="CreateSeriesDtoValidations"/> and
/// <see cref="UpdateSeriesDtoValidations"/> (WU41). Mirrors <see cref="GroupValidationsTests"/>.
/// Tier: Unit (directly constructed, Core-only types).
/// </summary>
public class SeriesValidationsTests
{
    // ── CreateSeriesDto ──────────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidDto_ReturnsNoErrors()
    {
        var dto = new CreateSeriesDto { Name = "The Kanto Chronicles" };
        dto.CanSave().Should().BeEmpty();
    }

    [Fact]
    public void Create_EmptyName_ReturnsError()
    {
        var dto = new CreateSeriesDto { Name = "" };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("name");
    }

    [Fact]
    public void Create_WhitespaceNameOnly_ReturnsError()
    {
        var dto = new CreateSeriesDto { Name = "   " };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("name");
    }

    [Fact]
    public void Create_NameExceedsMaxLength_ReturnsError()
    {
        var dto = new CreateSeriesDto { Name = new string('A', SeriesConstants.MaxNameLength + 1) };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("name");
    }

    [Fact]
    public void Create_NameAtMaxLength_ReturnsNoErrors()
    {
        var dto = new CreateSeriesDto { Name = new string('A', SeriesConstants.MaxNameLength) };
        dto.CanSave().Should().BeEmpty();
    }

    [Fact]
    public void Create_DescriptionExceedsMaxLength_ReturnsError()
    {
        var dto = new CreateSeriesDto
        {
            Name        = "Valid Name",
            Description = new string('x', SeriesConstants.MaxDescriptionLength + 1)
        };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("Description");
    }

    [Fact]
    public void Create_DescriptionAtMaxLength_ReturnsNoErrors()
    {
        var dto = new CreateSeriesDto
        {
            Name        = "Valid Name",
            Description = new string('x', SeriesConstants.MaxDescriptionLength)
        };
        dto.CanSave().Should().BeEmpty();
    }

    [Fact]
    public void Create_NullDescription_ReturnsNoErrors()
    {
        var dto = new CreateSeriesDto { Name = "My Series", Description = null };
        dto.CanSave().Should().BeEmpty();
    }

    // ── UpdateSeriesDto ──────────────────────────────────────────────────────────

    [Fact]
    public void Update_ValidDto_ReturnsNoErrors()
    {
        var dto = new UpdateSeriesDto { SeriesId = 1, Name = "Updated Name" };
        dto.CanSave().Should().BeEmpty();
    }

    [Fact]
    public void Update_EmptyName_ReturnsError()
    {
        var dto = new UpdateSeriesDto { SeriesId = 1, Name = "" };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("name");
    }

    [Fact]
    public void Update_NameExceedsMaxLength_ReturnsError()
    {
        var dto = new UpdateSeriesDto
        {
            SeriesId = 1,
            Name     = new string('A', SeriesConstants.MaxNameLength + 1)
        };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("name");
    }
}
