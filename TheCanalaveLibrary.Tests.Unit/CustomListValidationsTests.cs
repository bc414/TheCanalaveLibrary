using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="CustomListValidations"/> (Feature 51, WU-CustomLists) — pure domain
/// rules, directly constructed, no host/DB. Mirrors the SavedTagSelectionValidations test shape.
/// Tier: Unit.
/// </summary>
public class CustomListValidationsTests
{
    // ── ValidateListName ──────────────────────────────────────────────────────

    [Fact]
    public void ValidateListName_Valid_ReturnsNoErrors()
    {
        CustomListValidations.ValidateListName("Comfort re-reads", nameExists: false, listCount: 5)
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateListName_EmptyOrWhitespace_ReturnsError(string? name)
    {
        CustomListValidations.ValidateListName(name, nameExists: false)
            .Should().ContainSingle(e => e.Contains("must not be empty"));
    }

    [Fact]
    public void ValidateListName_TooLong_ReturnsError()
    {
        string name = new('a', CustomListValidations.MaxListNameLength + 1);
        CustomListValidations.ValidateListName(name, nameExists: false)
            .Should().ContainSingle(e => e.Contains("characters or fewer"));
    }

    [Fact]
    public void ValidateListName_ExactlyMaxLength_IsValid()
    {
        string name = new('a', CustomListValidations.MaxListNameLength);
        CustomListValidations.ValidateListName(name, nameExists: false).Should().BeEmpty();
    }

    [Fact]
    public void ValidateListName_DuplicateName_ReturnsError()
    {
        CustomListValidations.ValidateListName("Mine", nameExists: true)
            .Should().ContainSingle(e => e.Contains("already have a list named"));
    }

    [Fact]
    public void ValidateListName_AtListCap_ReturnsError()
    {
        CustomListValidations.ValidateListName("One more", nameExists: false,
                listCount: CustomListValidations.MaxListsPerUser)
            .Should().ContainSingle(e => e.Contains($"at most {CustomListValidations.MaxListsPerUser}"));
    }

    [Fact]
    public void ValidateListName_JustBelowCap_IsValid()
    {
        CustomListValidations.ValidateListName("Last slot", nameExists: false,
                listCount: CustomListValidations.MaxListsPerUser - 1)
            .Should().BeEmpty();
    }

    [Fact]
    public void ValidateListName_MultipleFailures_ReturnsAllErrors()
    {
        CustomListValidations.ValidateListName("", nameExists: true,
                listCount: CustomListValidations.MaxListsPerUser)
            .Should().HaveCount(3);
    }

    // ── DisambiguateCloneName ─────────────────────────────────────────────────

    [Fact]
    public void DisambiguateCloneName_NoCollision_AppendsCopy()
    {
        HashSet<string> existing = new(StringComparer.OrdinalIgnoreCase) { "Faves" };
        CustomListValidations.DisambiguateCloneName("Faves", existing).Should().Be("Faves (copy)");
    }

    [Fact]
    public void DisambiguateCloneName_CopyTaken_EscalatesToNumberedSuffix()
    {
        HashSet<string> existing = new(StringComparer.OrdinalIgnoreCase)
        {
            "Faves", "Faves (copy)", "Faves (copy 2)"
        };
        CustomListValidations.DisambiguateCloneName("Faves", existing).Should().Be("Faves (copy 3)");
    }

    [Fact]
    public void DisambiguateCloneName_CollisionCheckIsCaseInsensitive()
    {
        HashSet<string> existing = new(StringComparer.OrdinalIgnoreCase) { "FAVES (COPY)" };
        CustomListValidations.DisambiguateCloneName("Faves", existing).Should().Be("Faves (copy 2)");
    }

    [Fact]
    public void DisambiguateCloneName_ResultNeverExceedsMaxLength()
    {
        string longName = new('x', CustomListValidations.MaxListNameLength);
        HashSet<string> existing = new(StringComparer.OrdinalIgnoreCase);
        CustomListValidations.DisambiguateCloneName(longName, existing)
            .Length.Should().BeLessThanOrEqualTo(CustomListValidations.MaxListNameLength);
    }
}
