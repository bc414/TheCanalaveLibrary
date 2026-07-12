namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// Tidy-tree layout for the Manual Tree Search canvas (Feature 33 / WU40): a 2D top-down
/// node-link diagram — root at top, children fanning out beneath, depth = row. Leaves take
/// sequential horizontal slots; each parent centers over its own children — the classic rule
/// that keeps every subtree non-overlapping without a global constraint pass.
///
/// Pure deterministic math (Unit-tier testable), recomputed only on structural change; per-frame
/// pan/zoom never re-runs this (it's a CSS transform in manual-tree-search.js).
/// </summary>
public static class ManualTreeLayout
{
    public const int NodeSize = 56;      // square chip side (circular for users)
    public const int HGap = 40;          // horizontal gap between slot columns
    public const int VGap = 84;          // vertical distance between depth rows
    public const int CaptionWidth = 96;  // caption block width, centered under the chip
    public const int Pad = 26;           // canvas padding around the diagram

    public static int SlotWidth => NodeSize + HGap;

    /// <summary>
    /// Assigns <see cref="ManualTreeNode.Slot"/> and <see cref="ManualTreeNode.Depth"/> across
    /// the whole tree. Returns (leafCount, maxDepth) for canvas sizing.
    /// </summary>
    public static (int LeafCount, int MaxDepth) Arrange(ManualTreeNode root)
    {
        int nextLeafSlot = 0;
        int maxDepth = 0;

        void Assign(ManualTreeNode node, int depth)
        {
            node.Depth = depth;
            maxDepth = Math.Max(maxDepth, depth);
            if (node.Children.Count == 0)
            {
                node.Slot = nextLeafSlot++;
                return;
            }
            foreach (ManualTreeNode child in node.Children)
                Assign(child, depth + 1);
            node.Slot = (node.Children[0].Slot + node.Children[^1].Slot) / 2.0;
        }

        Assign(root, 0);
        return (Math.Max(nextLeafSlot, 1), maxDepth);
    }

    /// <summary>Canvas pixel size for an arranged tree.</summary>
    public static (double Width, double Height) CanvasSize(int leafCount, int maxDepth) =>
        (leafCount * SlotWidth - HGap + Pad * 2, (maxDepth + 1) * VGap + Pad * 2);

    /// <summary>Chip-center coordinates of an arranged node (SVG line endpoints).</summary>
    public static (double X, double Y) Center(ManualTreeNode node) =>
        (Pad + node.Slot * SlotWidth + NodeSize / 2.0, Pad + node.Depth * VGap + NodeSize / 2.0);

    /// <summary>Top-left of the caption-width item block (the chip centers inside it).</summary>
    public static (double Left, double Top) ItemOrigin(ManualTreeNode node) =>
        (Pad + node.Slot * SlotWidth - (CaptionWidth - NodeSize) / 2.0, Pad + node.Depth * VGap);
}
