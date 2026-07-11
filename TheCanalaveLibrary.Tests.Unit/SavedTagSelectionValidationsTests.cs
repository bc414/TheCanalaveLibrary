using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="SavedTagSelectionValidations"/> (WU43). Mirrors
/// <see cref="SeriesValidationsTests"/>/<see cref="StorySlugTests"/> in shape.
/// Tier: Unit (directly constructed, Core-only types, no EF/DbContext).
/// </summary>
public class SavedTagSelectionValidationsTests
{
    private static SavedTagSelectionInput ValidInput(
        string nickname = "My Fluff Combo",
        string? description = null,
        bool isPublic = false,
        IReadOnlyList<int>? included = null,
        IReadOnlyList<int>? excluded = null) =>
        new(nickname, description, isPublic, included ?? [1, 2], excluded ?? []);

    // ── CanSave ──────────────────────────────────────────────────────────────────

    [Fact]
    public void CanSave_ValidInput_ReturnsNoErrors()
    {
        ValidInput().CanSave(nicknameExists: false).Should().BeEmpty();
    }

    [Fact]
    public void CanSave_EmptyNickname_ReturnsError()
    {
        ValidInput(nickname: "").CanSave(nicknameExists: false)
            .Should().ContainSingle().Which.Should().Contain("Nickname");
    }

    [Fact]
    public void CanSave_WhitespaceNicknameOnly_ReturnsError()
    {
        ValidInput(nickname: "   ").CanSave(nicknameExists: false)
            .Should().ContainSingle().Which.Should().Contain("Nickname");
    }

    [Fact]
    public void CanSave_NicknameExceedsMaxLength_ReturnsError()
    {
        ValidInput(nickname: new string('A', SavedTagSelectionValidations.MaxNicknameLength + 1))
            .CanSave(nicknameExists: false)
            .Should().ContainSingle().Which.Should().Contain("Nickname");
    }

    [Fact]
    public void CanSave_NicknameAtMaxLength_ReturnsNoErrors()
    {
        ValidInput(nickname: new string('A', SavedTagSelectionValidations.MaxNicknameLength))
            .CanSave(nicknameExists: false)
            .Should().BeEmpty();
    }

    [Fact]
    public void CanSave_DescriptionExceedsMaxLength_ReturnsError()
    {
        ValidInput(description: new string('x', SavedTagSelectionValidations.MaxDescriptionLength + 1))
            .CanSave(nicknameExists: false)
            .Should().ContainSingle().Which.Should().Contain("Description");
    }

    [Fact]
    public void CanSave_DescriptionAtMaxLength_ReturnsNoErrors()
    {
        ValidInput(description: new string('x', SavedTagSelectionValidations.MaxDescriptionLength))
            .CanSave(nicknameExists: false)
            .Should().BeEmpty();
    }

    [Fact]
    public void CanSave_NullDescription_ReturnsNoErrors()
    {
        ValidInput(description: null).CanSave(nicknameExists: false).Should().BeEmpty();
    }

    [Fact]
    public void CanSave_EmptyIncludedAndExcluded_ReturnsError()
    {
        ValidInput(included: [], excluded: []).CanSave(nicknameExists: false)
            .Should().ContainSingle().Which.Should().Contain("at least one tag");
    }

    [Fact]
    public void CanSave_OnlyExcludedTags_ReturnsNoErrors()
    {
        // Include-only was the frozen L1 shape; WU43 additively supports exclude-only too.
        ValidInput(included: [], excluded: [5]).CanSave(nicknameExists: false).Should().BeEmpty();
    }

    [Fact]
    public void CanSave_NicknameExists_ReturnsError()
    {
        ValidInput().CanSave(nicknameExists: true)
            .Should().ContainSingle().Which.Should().Contain("already have a saved selection");
    }

    [Fact]
    public void CanSave_MultipleViolations_ReturnsAllErrors()
    {
        SavedTagSelectionInput dto = ValidInput(nickname: "", included: [], excluded: []);
        dto.CanSave(nicknameExists: false).Should().HaveCount(2);
    }

    // ── DisambiguateCopyNickname ─────────────────────────────────────────────────

    [Fact]
    public void Disambiguate_NoCollision_ReturnsFirstCopySuffix()
    {
        SavedTagSelectionValidations.DisambiguateCopyNickname("Fluff", [])
            .Should().Be("Fluff (copy)");
    }

    [Fact]
    public void Disambiguate_FirstCollision_ReturnsSecondCopySuffix()
    {
        HashSet<string> existing = new(StringComparer.OrdinalIgnoreCase) { "Fluff (copy)" };
        SavedTagSelectionValidations.DisambiguateCopyNickname("Fluff", existing)
            .Should().Be("Fluff (copy 2)");
    }

    [Fact]
    public void Disambiguate_SeveralCollisions_SkipsToFirstFreeSuffix()
    {
        HashSet<string> existing = new(StringComparer.OrdinalIgnoreCase)
        {
            "Fluff (copy)", "Fluff (copy 2)", "Fluff (copy 3)"
        };
        SavedTagSelectionValidations.DisambiguateCopyNickname("Fluff", existing)
            .Should().Be("Fluff (copy 4)");
    }

    [Fact]
    public void Disambiguate_IsCaseInsensitive()
    {
        HashSet<string> existing = new(StringComparer.OrdinalIgnoreCase) { "fluff (COPY)" };
        SavedTagSelectionValidations.DisambiguateCopyNickname("Fluff", existing)
            .Should().Be("Fluff (copy 2)");
    }

    [Fact]
    public void Disambiguate_ResultExceedingMaxLength_IsTruncated()
    {
        string longName = new('A', SavedTagSelectionValidations.MaxNicknameLength);
        string result = SavedTagSelectionValidations.DisambiguateCopyNickname(longName, []);
        result.Length.Should().Be(SavedTagSelectionValidations.MaxNicknameLength);
    }
}
