using System.Text.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// One node of the client-curated Manual Tree Search tree (Feature 33 / WU40). Pure client-side
/// UI state — never crosses the service boundary (hence no Dto suffix; see the slim-bag naming
/// rule in layer3.5-structure.md). Carries identity + provenance only; display data (title,
/// thumbnail) lives in the owning tab's display map and is rehydrated server-side on load.
/// </summary>
public sealed class ManualTreeNode
{
    public string NodeId { get; } = Guid.NewGuid().ToString("N")[..8];
    public required int EntityId { get; init; }
    public required bool IsStory { get; init; }
    /// <summary>The (edge, direction) pair this node was reached via; null on the root.</summary>
    public ManualTreeEdge? Edge { get; init; }
    /// <summary>Explore only: added but not yet explored (dashed frontier). Selecting solidifies.</summary>
    public bool Ghost { get; set; }
    public ManualTreeNode? Parent { get; set; }
    public List<ManualTreeNode> Children { get; } = [];

    // Assigned by ManualTreeLayout.Arrange — not persisted.
    public double Slot { get; set; }
    public int Depth { get; set; }

    public ManualTreeNode? Find(string nodeId)
    {
        if (NodeId == nodeId) return this;
        foreach (ManualTreeNode child in Children)
            if (child.Find(nodeId) is { } found) return found;
        return null;
    }

    public IEnumerable<ManualTreeNode> EnumerateAll()
    {
        yield return this;
        foreach (ManualTreeNode child in Children)
            foreach (ManualTreeNode n in child.EnumerateAll())
                yield return n;
    }

    /// <summary>True when this node already has a child for the given entity (per-node dedup —
    /// the settled rule; duplicates across DIFFERENT branches remain allowed).</summary>
    public bool HasChildEntity(bool isStory, int entityId) =>
        Children.Any(c => c.IsStory == isStory && c.EntityId == entityId);

    public ManualTreeNode AddChild(bool isStory, int entityId, ManualTreeEdge edge, bool ghost)
    {
        ManualTreeNode child = new() { EntityId = entityId, IsStory = isStory, Edge = edge, Ghost = ghost, Parent = this };
        Children.Add(child);
        return child;
    }

    // ── localStorage persistence (IDs + edges ONLY — settled 2026-07-12) ─────────────────────────

    private sealed record Persisted(int E, bool S, ManualTreeEdge? D, bool G, List<Persisted> C);

    private static readonly JsonSerializerOptions JsonOpts = JsonSerializerOptions.Web;

    public string ToJson() => JsonSerializer.Serialize(ToPersisted(this), JsonOpts);

    private static Persisted ToPersisted(ManualTreeNode n) =>
        new(n.EntityId, n.IsStory, n.Edge, n.Ghost, [.. n.Children.Select(ToPersisted)]);

    /// <summary>Deserializes a persisted tree; null on corrupt/legacy payloads (treated as no
    /// saved tree, never thrown — same discipline as DraftStore).</summary>
    public static ManualTreeNode? FromJson(string json)
    {
        try
        {
            Persisted? p = JsonSerializer.Deserialize<Persisted>(json, JsonOpts);
            return p is null ? null : FromPersisted(p, null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ManualTreeNode FromPersisted(Persisted p, ManualTreeNode? parent)
    {
        ManualTreeNode node = new() { EntityId = p.E, IsStory = p.S, Edge = p.D, Ghost = p.G, Parent = parent };
        // p.C can be null on legacy/partial payloads (System.Text.Json leaves a missing list
        // property null) — treat as leaf rather than throw; corrupt trees degrade, never crash.
        foreach (Persisted c in p.C ?? [])
            node.Children.Add(FromPersisted(c, node));
        return node;
    }

    /// <summary>
    /// Removes every subtree whose root entity is not in the survivor sets (rehydration prune:
    /// the entity vanished, was taken down, or is rating-gated for this viewer). The tree root
    /// itself is never pruned here — the owner handles a dead root by resetting.
    /// Returns the number of nodes removed.
    /// </summary>
    public int PruneMissing(IReadOnlySet<int> storyIds, IReadOnlySet<int> userIds)
    {
        int removed = 0;
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            ManualTreeNode child = Children[i];
            bool alive = child.IsStory ? storyIds.Contains(child.EntityId) : userIds.Contains(child.EntityId);
            if (!alive)
            {
                removed += child.EnumerateAll().Count();
                Children.RemoveAt(i);
            }
            else
            {
                removed += child.PruneMissing(storyIds, userIds);
            }
        }
        return removed;
    }
}

/// <summary>Display data for one tree-node chip (title + thumbnail), keyed by (IsStory, EntityId)
/// in the owning tab's display map. UI-state record, not a service DTO.</summary>
public sealed record ManualTreeNodeDisplay(string Title, string? ImageUrl);
