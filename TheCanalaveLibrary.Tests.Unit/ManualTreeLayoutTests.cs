using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Tidy-tree layout math for the Manual Tree Search canvas (WU40) — pure deterministic
/// slot/depth assignment, no host, no DOM (the whole point of keeping layout in C#; tier: Unit
/// per testing.md). Fan-out cases mirror the taxonomy's real ≤5 cap.
/// </summary>
public sealed class ManualTreeLayoutTests
{
    private static ManualTreeNode Story(int id) => new() { EntityId = id, IsStory = true };

    [Fact]
    public void SingleNode_TakesSlotZero_DepthZero()
    {
        ManualTreeNode root = Story(1);

        var (leafCount, maxDepth) = ManualTreeLayout.Arrange(root);

        leafCount.Should().Be(1);
        maxDepth.Should().Be(0);
        root.Slot.Should().Be(0);
        root.Depth.Should().Be(0);
    }

    [Fact]
    public void FiveChildren_GetSequentialSlots_ParentCentersOverThem()
    {
        // The taxonomy's real max fan-out (≤5) — the case the mock was stress-tested at.
        ManualTreeNode root = Story(1);
        for (int i = 0; i < 5; i++)
            root.AddChild(isStory: false, entityId: 100 + i, ManualTreeEdge.StoryFavoriter, ghost: false);

        var (leafCount, maxDepth) = ManualTreeLayout.Arrange(root);

        leafCount.Should().Be(5);
        maxDepth.Should().Be(1);
        root.Children.Select(c => c.Slot).Should().BeEquivalentTo([0.0, 1.0, 2.0, 3.0, 4.0],
            o => o.WithStrictOrdering());
        root.Slot.Should().Be(2.0, "a parent centers over its children (midpoint of slots 0..4)");
        root.Children.Should().OnlyContain(c => c.Depth == 1);
    }

    [Fact]
    public void TwoSubtrees_DoNotOverlap_LeavesGetDistinctSlots()
    {
        // root → (A with 3 children, B with 2 children): B's leaves must start after A's.
        ManualTreeNode root = Story(1);
        ManualTreeNode a = root.AddChild(false, 10, ManualTreeEdge.StoryFavoriter, false);
        ManualTreeNode b = root.AddChild(false, 11, ManualTreeEdge.StoryRecommender, false);
        for (int i = 0; i < 3; i++) a.AddChild(true, 200 + i, ManualTreeEdge.UserFavoriteStory, false);
        for (int i = 0; i < 2; i++) b.AddChild(true, 300 + i, ManualTreeEdge.UserRecommendedStory, false);

        ManualTreeLayout.Arrange(root);

        double[] allLeafSlots = [.. a.Children.Concat(b.Children).Select(c => c.Slot)];
        allLeafSlots.Should().OnlyHaveUniqueItems("every leaf owns its own slot column");
        b.Children.Min(c => c.Slot).Should().BeGreaterThan(a.Children.Max(c => c.Slot),
            "sibling subtrees never overlap");
        a.Slot.Should().Be(1.0, "midpoint of leaf slots 0..2");
        b.Slot.Should().Be(3.5, "midpoint of leaf slots 3..4");
        root.Slot.Should().Be((a.Slot + b.Slot) / 2);
    }

    [Fact]
    public void DeepChain_DepthGrowsPerHop_WidthStaysOneSlot()
    {
        // A pure chain-of-trust walk (Deep Dive's signature shape): story→author→gem→author…
        ManualTreeNode root = Story(1);
        ManualTreeNode author = root.AddChild(false, 10, ManualTreeEdge.StoryAuthor, false);
        ManualTreeNode gem = author.AddChild(true, 2, ManualTreeEdge.UserGemmedStory, false);
        ManualTreeNode author2 = gem.AddChild(false, 11, ManualTreeEdge.StoryAuthor, false);

        var (leafCount, maxDepth) = ManualTreeLayout.Arrange(root);

        leafCount.Should().Be(1, "a chain has exactly one leaf");
        maxDepth.Should().Be(3);
        new[] { root, author, gem, author2 }.Select(n => n.Depth).Should().Equal(0, 1, 2, 3);
        new[] { root, author, gem, author2 }.Should().OnlyContain(n => n.Slot == 0);
    }

    [Fact]
    public void CanvasSize_ScalesWithLeavesAndDepth()
    {
        var (w1, h1) = ManualTreeLayout.CanvasSize(leafCount: 1, maxDepth: 0);
        var (w5, h3) = ManualTreeLayout.CanvasSize(leafCount: 5, maxDepth: 3);

        w5.Should().BeGreaterThan(w1);
        h3.Should().BeGreaterThan(h1);
        w5.Should().Be(5 * ManualTreeLayout.SlotWidth - ManualTreeLayout.HGap + ManualTreeLayout.Pad * 2);
        h3.Should().Be(4 * ManualTreeLayout.VGap + ManualTreeLayout.Pad * 2);
    }

    [Fact]
    public void Center_IsInsideTheItemBlock()
    {
        ManualTreeNode root = Story(1);
        root.AddChild(false, 10, ManualTreeEdge.StoryAuthor, false);
        ManualTreeLayout.Arrange(root);

        var (cx, cy) = ManualTreeLayout.Center(root);
        var (left, top) = ManualTreeLayout.ItemOrigin(root);

        cx.Should().BeGreaterThan(left).And.BeLessThan(left + ManualTreeLayout.CaptionWidth);
        cy.Should().Be(top + ManualTreeLayout.NodeSize / 2.0);
    }
}

/// <summary>
/// Client-tree model rules (WU40): the IDs+edges-only JSON persistence contract (settled
/// 2026-07-12) and the rehydration prune. Tier: Unit.
/// </summary>
public sealed class ManualTreeNodeTests
{
    [Fact]
    public void JsonRoundTrip_PreservesStructure_EdgesAndGhostState()
    {
        ManualTreeNode root = new() { EntityId = 1, IsStory = true };
        ManualTreeNode author = root.AddChild(false, 10, ManualTreeEdge.StoryAuthor, ghost: false);
        author.AddChild(true, 2, ManualTreeEdge.UserGemmedStory, ghost: true);

        ManualTreeNode? restored = ManualTreeNode.FromJson(root.ToJson());

        restored.Should().NotBeNull();
        restored!.EntityId.Should().Be(1);
        restored.IsStory.Should().BeTrue();
        restored.Children.Should().ContainSingle();
        ManualTreeNode restoredAuthor = restored.Children[0];
        restoredAuthor.EntityId.Should().Be(10);
        restoredAuthor.Edge.Should().Be(ManualTreeEdge.StoryAuthor);
        restoredAuthor.Parent.Should().BeSameAs(restored, "parent links are rebuilt on load");
        ManualTreeNode gem = restoredAuthor.Children.Should().ContainSingle().Subject;
        gem.Edge.Should().Be(ManualTreeEdge.UserGemmedStory);
        gem.Ghost.Should().BeTrue("the frontier state survives the round trip");
    }

    [Fact]
    public void PersistedJson_CarriesNoDisplayData()
    {
        // The settled contract: IDs + edges ONLY — titles/covers always rehydrate server-side,
        // so a taken-down story's title can never linger in the viewer's localStorage.
        ManualTreeNode root = new() { EntityId = 42, IsStory = true };
        string json = root.ToJson();

        json.Should().NotContainAny("Title", "title", "ImageUrl", "imageUrl", "NodeId", "nodeId");
    }

    [Fact]
    public void FromJson_CorruptPayload_ReturnsNull_NeverThrows()
    {
        ManualTreeNode.FromJson("{not json").Should().BeNull();
        ManualTreeNode.FromJson("""{"unexpected":"shape"}""").Should().NotBeNull("missing fields default");
    }

    [Fact]
    public void PruneMissing_RemovesVanishedSubtrees_AndCountsAllRemovedNodes()
    {
        ManualTreeNode root = new() { EntityId = 1, IsStory = true };
        ManualTreeNode alive = root.AddChild(false, 10, ManualTreeEdge.StoryAuthor, false);
        alive.AddChild(true, 2, ManualTreeEdge.UserGemmedStory, false);          // alive
        ManualTreeNode dead = root.AddChild(false, 11, ManualTreeEdge.StoryFavoriter, false);
        dead.AddChild(true, 3, ManualTreeEdge.UserFavoriteStory, false);         // removed with parent

        int removed = root.PruneMissing(
            storyIds: new HashSet<int> { 1, 2 },   // story 3 vanished
            userIds: new HashSet<int> { 10 });     // user 11 vanished

        removed.Should().Be(2, "the dead user hop AND its whole subtree are removed");
        root.Children.Should().ContainSingle().Which.EntityId.Should().Be(10);
        alive.Children.Should().ContainSingle("the alive branch is untouched");
    }

    [Fact]
    public void HasChildEntity_DedupsPerNode_NotAcrossBranches()
    {
        // Per-node dedup only — duplicates across DIFFERENT branches are allowed by design
        // (the tree reflects the path the user took; settled path-reflecting rule).
        ManualTreeNode root = new() { EntityId = 1, IsStory = true };
        ManualTreeNode a = root.AddChild(false, 10, ManualTreeEdge.StoryFavoriter, false);
        ManualTreeNode b = root.AddChild(false, 11, ManualTreeEdge.StoryFavoriter, false);
        a.AddChild(true, 99, ManualTreeEdge.UserFavoriteStory, false);

        a.HasChildEntity(true, 99).Should().BeTrue();
        b.HasChildEntity(true, 99).Should().BeFalse("cross-branch duplicates are allowed");
        root.HasChildEntity(false, 10).Should().BeTrue();
    }
}
