using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>Unit tests for <see cref="PublicVerificationCode"/> (Feature 53, WU39) — pure format transform, no DbContext.</summary>
public class PublicVerificationCodeTests
{
    [Fact]
    public void New_HasExpectedPrefixAndLength()
    {
        string code = PublicVerificationCode.New();

        code.Should().StartWith("TCL-Verify-");
        code.Length.Should().Be("TCL-Verify-".Length + 6);
    }

    [Fact]
    public void New_UsesOnlyUnambiguousAlphabetCharacters()
    {
        // No 0/O, 1/I/L — an author transcribing this by hand onto an external site shouldn't
        // hit ambiguous glyphs.
        string code = PublicVerificationCode.New();
        string suffix = code["TCL-Verify-".Length..];

        suffix.Should().MatchRegex("^[A-HJ-KM-NP-Z2-9]{6}$");
    }

    [Fact]
    public void New_ProducesDifferentCodesAcrossCalls()
    {
        // Not a strict uniqueness guarantee (that's the DB's job) — just confirms it's not a
        // constant / degenerate generator.
        HashSet<string> codes = [.. Enumerable.Range(0, 50).Select(_ => PublicVerificationCode.New())];
        codes.Count.Should().BeGreaterThan(1);
    }
}
