using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="GroupFolderValidations.NameErrors"/> (WU-SchemaHardening, service audit
/// §2.10): before it, a blank folder name persisted and a name over the column's 100 characters raised a
/// raw <c>DbUpdateException</c> (a 500). Tier: Unit (a pure static helper, no host or database).
/// </summary>
public class GroupFolderValidationsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void NameErrors_MissingName_IsRefused(string? name)
    {
        GroupFolderValidations.NameErrors(name)
            .Should().ContainSingle().Which.Should().Be(GroupFolderValidations.NameRequired);
    }

    [Fact]
    public void NameErrors_ExactlyTheLimit_Passes()
    {
        GroupFolderValidations.NameErrors(new string('a', GroupConstants.MaxFolderNameLength))
            .Should().BeEmpty();
    }

    [Fact]
    public void NameErrors_OneOverTheLimit_IsRefused()
    {
        GroupFolderValidations.NameErrors(new string('a', GroupConstants.MaxFolderNameLength + 1))
            .Should().ContainSingle().Which.Should().Be(GroupFolderValidations.NameTooLong);
    }

    [Fact]
    public void NameErrors_LengthIsMeasuredAfterTrimming()
    {
        // 100 characters of name padded to 104: the service stores the trimmed 100, which fits.
        string padded = "  " + new string('a', GroupConstants.MaxFolderNameLength) + "  ";

        GroupFolderValidations.NameErrors(padded).Should().BeEmpty();
    }

    [Fact]
    public void NameTooLong_NamesTheLimit()
    {
        GroupFolderValidations.NameTooLong.Should().Contain(GroupConstants.MaxFolderNameLength.ToString());
    }
}
