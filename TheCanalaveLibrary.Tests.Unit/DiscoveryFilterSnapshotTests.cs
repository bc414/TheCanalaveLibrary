using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="DiscoveryFilterSnapshot"/> — the device-local persisted form of a
/// <c>/discover</c> filter (decision row 13). Pure transforms, no JS runtime and no DB.
///
/// The prune cases matter most: the snapshot lives in the viewer's own browser and can outlive the
/// tags it references, so rehydration has to drop what the viewer can no longer see rather than
/// filtering against ids the server would refuse.
/// Tier: Unit.
/// </summary>
public class DiscoveryFilterSnapshotTests
{
    private static StoryFilterDto FullFilter() => new()
    {
        TextQuery = "rain",
        IncludedTagIds = [1, 2],
        ExcludedTagIds = [3],
        IncludeMode = TagIncludeMode.Or,
        ExcludedInteractions = [UserStoryInteractionTypeEnum.Ignore],
        IncludedShips = [new ShipFilterDto { MemberTagIds = [10, 11], PairingType = CharacterPairingType.Romantic }],
        ExcludedShips = [new ShipFilterDto { MemberTagIds = [12], PairingType = null }],
        Sort = DefaultSortOrder.DatePublished,
        Page = 4,
        PageSize = 20
    };

    [Fact]
    public void From_ThenToFilter_RoundTripsEveryPersistedAxis()
    {
        DiscoveryFilterSnapshot snapshot = DiscoveryFilterSnapshot.From(FullFilter());

        StoryFilterDto restored = snapshot.ToFilter(new HashSet<int> { 1, 2, 3, 10, 11, 12 });

        restored.TextQuery.Should().Be("rain");
        restored.IncludedTagIds.Should().Equal(1, 2);
        restored.ExcludedTagIds.Should().Equal(3);
        restored.IncludeMode.Should().Be(TagIncludeMode.Or);
        restored.ExcludedInteractions.Should().Equal(UserStoryInteractionTypeEnum.Ignore);
        restored.Sort.Should().Be(DefaultSortOrder.DatePublished);
        restored.IncludedShips.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { MemberTagIds = new[] { 10, 11 }, PairingType = CharacterPairingType.Romantic });
        restored.ExcludedShips.Should().ContainSingle()
            .Which.MemberTagIds.Should().Equal(12);
    }

    [Fact]
    public void ToFilter_ResetsToPageOne()
    {
        // Restoring someone onto page 4 of a result set that has changed since the save is worse
        // than restoring them to the top — Page is deliberately not persisted.
        StoryFilterDto restored = DiscoveryFilterSnapshot.From(FullFilter())
            .ToFilter(new HashSet<int> { 1, 2, 3, 10, 11, 12 });

        restored.Page.Should().Be(1);
    }

    [Fact]
    public void ToFilter_DropsTagIdsTheViewerCanNoLongerSee()
    {
        StoryFilterDto restored = DiscoveryFilterSnapshot.From(FullFilter())
            .ToFilter(new HashSet<int> { 1, 10, 11 }); // 2, 3 and 12 have vanished

        restored.IncludedTagIds.Should().Equal(1);
        restored.ExcludedTagIds.Should().BeEmpty();
    }

    [Fact]
    public void ToFilter_NarrowsAShipThatLostSomeMembers_RatherThanWideningIt()
    {
        // Keeping the surviving member is the conservative reading: the viewer asked to narrow, so
        // a partially-resolvable ship must not silently become "any ship".
        StoryFilterDto restored = DiscoveryFilterSnapshot.From(FullFilter())
            .ToFilter(new HashSet<int> { 10 }); // member 11 gone

        restored.IncludedShips.Should().ContainSingle()
            .Which.MemberTagIds.Should().Equal(10);
    }

    [Fact]
    public void ToFilter_DropsAShipWhoseMembersAllVanished()
    {
        StoryFilterDto restored = DiscoveryFilterSnapshot.From(FullFilter())
            .ToFilter(new HashSet<int> { 1 }); // every ship member gone

        restored.IncludedShips.Should().BeEmpty();
        restored.ExcludedShips.Should().BeEmpty();
    }

    [Fact]
    public void ToFilter_TreatsWhitespaceTextAsNoTextQuery()
    {
        DiscoveryFilterSnapshot snapshot = DiscoveryFilterSnapshot.From(
            FullFilter() with { TextQuery = "   " });

        snapshot.ToFilter(new HashSet<int>()).TextQuery.Should().BeNull();
    }

    [Fact]
    public void AllTagIds_CollectsBothTagAxesAndEveryShipMember_Deduplicated()
    {
        StoryFilterDto filter = FullFilter() with
        {
            // 1 appears as both an included tag and a ship member — one batch read, one entry.
            IncludedShips = [new ShipFilterDto { MemberTagIds = [1, 10] }]
        };

        DiscoveryFilterSnapshot.From(filter).AllTagIds()
            .Should().BeEquivalentTo([1, 2, 3, 10, 12]);
    }

    [Fact]
    public void AllTagIds_IsEmpty_WhenNoAxisReferencesATag()
    {
        StoryFilterDto textOnly = new() { TextQuery = "rain" };

        DiscoveryFilterSnapshot.From(textOnly).AllTagIds().Should().BeEmpty();
    }
}
