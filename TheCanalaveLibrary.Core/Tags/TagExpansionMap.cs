namespace TheCanalaveLibrary.Core;

/// <summary>
/// Immutable snapshot of the tag parent→children hierarchy, in the one shape discovery filtering
/// needs: <c>Expand(id) → {self} ∪ children</c>. Hierarchy is exactly one level deep (enforced by
/// <c>TagValidations</c>), so expansion is a lookup, never a walk.
///
/// <para>Passing this as an explicit argument is what makes the story-filter predicate a pure
/// function of its inputs — reproducible, loggable, replayable (hidden-deferrals-tracker B12
/// complaint 1). See layer2-services.md §"Reference-Data Caching" and §"Tag Hierarchy Roll-Up".</para>
/// </summary>
public sealed class TagExpansionMap
{
    /// <summary>parentId → [parent, ...children], precomputed so a hit allocates nothing.</summary>
    private readonly Dictionary<int, int[]> _selfAndChildren;

    public static readonly TagExpansionMap Empty = new([]);

    private TagExpansionMap(Dictionary<int, int[]> selfAndChildren) => _selfAndChildren = selfAndChildren;

    /// <summary>
    /// Builds a map from raw child rows (every tag with a non-null parent, one row per child).
    /// Grouping lives here, in Core, so it is Unit-testable without a DbContext.
    /// </summary>
    public static TagExpansionMap FromChildRows(IEnumerable<(int ParentTagId, int TagId)> rows)
    {
        Dictionary<int, List<int>> byParent = [];
        foreach ((int parent, int child) in rows)
        {
            if (!byParent.TryGetValue(parent, out List<int>? kids))
                byParent[parent] = kids = [];
            kids.Add(child);
        }
        return new TagExpansionMap(byParent.ToDictionary(p => p.Key, p => (int[])[p.Key, .. p.Value]));
    }

    /// <summary>
    /// <c>{self} ∪ children</c>. An id with no children — INCLUDING an id that does not exist in
    /// <c>tags</c> at all — returns <c>[id]</c>, never throws. A whole-map cache is keyed on
    /// parents-with-children, so a miss is the normal case for most ids (childless tags, or ids that
    /// don't exist at all — callers may pass ids the caller never validated as real tags).
    /// </summary>
    public int[] Expand(int tagId) =>
        _selfAndChildren.TryGetValue(tagId, out int[]? set) ? set : [tagId];

    /// <summary>Distinct parents with at least one child. Diagnostics/tests only.</summary>
    public int ParentCount => _selfAndChildren.Count;
}
