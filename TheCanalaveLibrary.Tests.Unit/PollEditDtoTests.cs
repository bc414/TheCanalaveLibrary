using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="PollEditDto.CanSave"/> (Feature 37) — tier-2 validation, mirrors
/// <see cref="BlogPostValidationsTests"/>. Tier: Unit.
/// </summary>
public class PollEditDtoTests
{
    private static PollEditDto Valid() => new()
    {
        PollName = "Favorite starter?",
        Options = [new PollOptionEditDto(null, "Turtwig"), new PollOptionEditDto(null, "Chimchar")],
    };

    [Fact]
    public void CanSave_ValidDto_NoErrors() => Valid().CanSave().Should().BeEmpty();

    [Fact]
    public void CanSave_MissingName_Errors()
    {
        PollEditDto dto = Valid();
        dto.PollName = "  ";
        dto.CanSave().Should().ContainSingle(e => e.Contains("name"));
    }

    [Fact]
    public void CanSave_NameTooLong_Errors()
    {
        PollEditDto dto = Valid();
        dto.PollName = new string('x', 257);
        dto.CanSave().Should().NotBeEmpty();
    }

    [Fact]
    public void CanSave_FewerThanTwoOptions_Errors()
    {
        PollEditDto dto = Valid();
        dto.Options.RemoveAt(1);
        dto.CanSave().Should().Contain(e => e.Contains("two options"));
    }

    [Fact]
    public void CanSave_BlankOption_Errors()
    {
        PollEditDto dto = Valid();
        dto.Options.Add(new PollOptionEditDto(null, "   "));
        dto.CanSave().Should().Contain(e => e.Contains("blank"));
    }

    [Fact]
    public void CanSave_DuplicateOptions_Errors()
    {
        PollEditDto dto = Valid();
        dto.Options.Add(new PollOptionEditDto(null, "Turtwig"));
        dto.CanSave().Should().Contain(e => e.Contains("unique"));
    }

    [Fact]
    public void CanSave_CloseBeforeOpen_Errors()
    {
        PollEditDto dto = Valid();
        dto.DateOpened = new DateTime(2026, 7, 12, 12, 0, 0, DateTimeKind.Utc);
        dto.DateClosed = dto.DateOpened.Value.AddHours(-1);
        dto.CanSave().Should().Contain(e => e.Contains("close date"));
    }

    [Fact]
    public void CanSave_NullDates_IsValid()
    {
        // Null open = "immediately"; null close = indefinite (settled 2026-07-12).
        PollEditDto dto = Valid();
        dto.DateOpened = null;
        dto.DateClosed = null;
        dto.CanSave().Should().BeEmpty();
    }
}
