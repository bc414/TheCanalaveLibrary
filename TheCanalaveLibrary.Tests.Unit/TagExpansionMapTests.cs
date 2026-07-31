using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="TagExpansionMap"/> — the pure value type that carries tag-hierarchy
/// roll-up into <c>ServerStoryReadService.ApplyFilters</c> (WU-ApplyFiltersPurity, closes
/// hidden-deferrals-tracker B12). Pure grouping/lookup logic, no DbContext, no DB.
///
/// <c>Expand</c>'s miss behavior is the highest-likelihood refactor bug this WU introduces: the
/// retired per-request dictionary was keyed on the caller's own ids, so a miss was impossible. A
/// whole-map cache is keyed on parents-with-children, so misses (childless tags, nonexistent ids)
/// are the normal case and must return <c>[id]</c>, never throw.
/// Tier: Unit.
/// </summary>
public class TagExpansionMapTests
{
    [Fact]
    public void Expand_ParentWithChildren_ReturnsSelfFollowedByChildren()
    {
        TagExpansionMap map = TagExpansionMap.FromChildRows([(1, 2), (1, 3)]);

        // Self comes first, then children in row order.
        map.Expand(1).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Expand_ChildlessKnownTag_ReturnsOnlySelf()
    {
        // Tag 5 has children of its own elsewhere in the map, but tag 1 (queried here) does not.
        TagExpansionMap map = TagExpansionMap.FromChildRows([(5, 6)]);

        map.Expand(1).Should().Equal(1);
    }

    [Fact]
    public void Expand_IdAbsentFromMapEntirely_ReturnsSelf_DoesNotThrow()
    {
        TagExpansionMap map = TagExpansionMap.FromChildRows([(1, 2)]);

        // id 999 was never seen as a parent OR a child anywhere in the source rows — e.g. a
        // caller-supplied id that isn't a real tag at all (ApiErrorEnvelopeTests exercises exactly
        // this against the live service). Must not throw.
        Action act = () => map.Expand(999).Should().Equal(999);
        act.Should().NotThrow();
    }

    [Fact]
    public void Empty_Expand_ReturnsSelfForAnyId()
    {
        TagExpansionMap.Empty.Expand(42).Should().Equal(42);
    }

    [Fact]
    public void FromChildRows_MultipleChildrenUnderOneParent_AreAllGrouped()
    {
        TagExpansionMap map = TagExpansionMap.FromChildRows([(10, 11), (10, 12), (10, 13)]);

        map.Expand(10).Should().Equal(10, 11, 12, 13);
        map.ParentCount.Should().Be(1);
    }

    [Fact]
    public void FromChildRows_SeveralDistinctParents_EachExpandsIndependently()
    {
        TagExpansionMap map = TagExpansionMap.FromChildRows([(1, 2), (3, 4), (3, 5)]);

        map.Expand(1).Should().Equal(1, 2);
        map.Expand(3).Should().Equal(3, 4, 5);
        map.ParentCount.Should().Be(2);
    }

    [Fact]
    public void FromChildRows_EmptyInput_ProducesMapEquivalentToEmpty()
    {
        TagExpansionMap map = TagExpansionMap.FromChildRows([]);

        map.ParentCount.Should().Be(0);
        map.Expand(1).Should().Equal(1);
    }
}
