using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="GroupAudienceTypeMapper"/> (WU32).
/// Verifies round-trip fidelity for all three presets and that the inverse mapper
/// handles edge cases correctly. Dependency-free, no host/DB.
/// Tier: Unit (directly constructed, Core-only types).
/// </summary>
public class GroupAudienceTypeMapperTests
{
    // ── ToRatings (preset → rating pair) ─────────────────────────────────────

    [Fact]
    public void ToRatings_Standard_ReturnsExpectedPair()
    {
        (Rating audience, Rating max) = GroupAudienceTypeMapper.ToRatings(GroupAudienceType.Standard);
        audience.Should().Be(Rating.E);
        max.Should().Be(Rating.M);
    }

    [Fact]
    public void ToRatings_SfwOnly_ReturnsExpectedPair()
    {
        (Rating audience, Rating max) = GroupAudienceTypeMapper.ToRatings(GroupAudienceType.SfwOnly);
        audience.Should().Be(Rating.E);
        max.Should().Be(Rating.T);
    }

    [Fact]
    public void ToRatings_Mature_ReturnsExpectedPair()
    {
        (Rating audience, Rating max) = GroupAudienceTypeMapper.ToRatings(GroupAudienceType.Mature);
        audience.Should().Be(Rating.M);
        max.Should().Be(Rating.M);
    }

    [Fact]
    public void ToRatings_UnknownValue_Throws()
    {
        Action act = () => GroupAudienceTypeMapper.ToRatings((GroupAudienceType)99);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ── FromRatings (pair → preset) ────────────────────────────────────────

    [Fact]
    public void FromRatings_StandardPair_ReturnsStandard()
    {
        GroupAudienceType result = GroupAudienceTypeMapper.FromRatings(Rating.E, Rating.M);
        result.Should().Be(GroupAudienceType.Standard);
    }

    [Fact]
    public void FromRatings_SfwOnlyPair_ReturnsSfwOnly()
    {
        GroupAudienceType result = GroupAudienceTypeMapper.FromRatings(Rating.E, Rating.T);
        result.Should().Be(GroupAudienceType.SfwOnly);
    }

    [Fact]
    public void FromRatings_MaturePair_ReturnsMature()
    {
        GroupAudienceType result = GroupAudienceTypeMapper.FromRatings(Rating.M, Rating.M);
        result.Should().Be(GroupAudienceType.Mature);
    }

    [Fact]
    public void FromRatings_MatureWithTMax_ReturnsMature()
    {
        // Mature AudienceRating dominates — max content rating is irrelevant (spec: M groups may hold any content).
        GroupAudienceType result = GroupAudienceTypeMapper.FromRatings(Rating.M, Rating.T);
        result.Should().Be(GroupAudienceType.Mature);
    }

    // ── Round-trip ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(GroupAudienceType.Standard)]
    [InlineData(GroupAudienceType.SfwOnly)]
    [InlineData(GroupAudienceType.Mature)]
    public void RoundTrip_ToRatingsThenFromRatings_IsIdentity(GroupAudienceType original)
    {
        (Rating audience, Rating max) = GroupAudienceTypeMapper.ToRatings(original);
        GroupAudienceType roundTripped = GroupAudienceTypeMapper.FromRatings(audience, max);
        roundTripped.Should().Be(original);
    }
}
