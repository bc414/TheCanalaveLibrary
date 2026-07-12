using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="CreateStoryLineageDtoValidations"/> (Feature 10, WU42). Shape-only
/// checks (positive ids, source != target, type selected) — existence checks (target story/type
/// actually exist in the DB) are the write service's job, covered at the Integration tier.
/// Tier: Unit (directly constructed, Core-only types).
/// </summary>
public class StoryLineageValidationsTests
{
    [Fact]
    public void Create_ValidDto_ReturnsNoErrors()
    {
        var dto = new CreateStoryLineageDto { SourceStoryId = 1, TargetStoryId = 2, TypeId = 3 };
        dto.CanSave().Should().BeEmpty();
    }

    [Fact]
    public void Create_ZeroSourceStoryId_ReturnsError()
    {
        var dto = new CreateStoryLineageDto { SourceStoryId = 0, TargetStoryId = 2, TypeId = 3 };
        dto.CanSave().Should().Contain(e => e.Contains("source"));
    }

    [Fact]
    public void Create_ZeroTargetStoryId_ReturnsError()
    {
        var dto = new CreateStoryLineageDto { SourceStoryId = 1, TargetStoryId = 0, TypeId = 3 };
        dto.CanSave().Should().Contain(e => e.Contains("target"));
    }

    [Fact]
    public void Create_SourceEqualsTarget_ReturnsError()
    {
        var dto = new CreateStoryLineageDto { SourceStoryId = 5, TargetStoryId = 5, TypeId = 1 };
        dto.CanSave().Should().Contain(e => e.Contains("itself"));
    }

    [Fact]
    public void Create_ZeroTypeId_ReturnsError()
    {
        var dto = new CreateStoryLineageDto { SourceStoryId = 1, TargetStoryId = 2, TypeId = 0 };
        dto.CanSave().Should().Contain(e => e.Contains("type"));
    }

    [Fact]
    public void Create_NegativeIds_ReturnMultipleErrors()
    {
        var dto = new CreateStoryLineageDto { SourceStoryId = -1, TargetStoryId = -2, TypeId = -1 };
        dto.CanSave().Should().HaveCount(3);
    }
}
