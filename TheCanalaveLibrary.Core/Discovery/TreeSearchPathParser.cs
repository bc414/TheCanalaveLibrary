using System.Text.RegularExpressions;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Parses <see cref="TreeSearchHitDto.Path"/> / <see cref="TreeSearchListingItemDto.Path"/> — the raw
/// Postgres <c>CYCLE ... USING path</c> clause text emitted by <c>ServerTreeSearchReadService</c>'s
/// recursive CTE, a composite-array literal shaped like <c>{"(f,12)","(t,45)","(f,88)"}</c> where each
/// tuple is <c>(is_story, node_id)</c>.
///
/// <para>Dependency-free, unit-testable — no DbContext, no host. Privacy (corrected 2026-07-12,
/// WU40): chain-of-trust paths — the only paths that exist — carry no anonymized contributor, so
/// user hops MAY be resolved to identity; the viewer-filtered hydration lives in
/// <c>ServerTreeSearchReadService.AttachPathHopsAsync</c> (a hop the viewer cannot see gets no
/// label). <see cref="PathNode.IsStory"/> distinguishes hop kinds for that hydration.</para>
/// </summary>
public static class TreeSearchPathParser
{
    public readonly record struct PathNode(bool IsStory, int NodeId);

    private static readonly Regex TupleRegex = new(@"\((t|f|true|false),(\d+)\)", RegexOptions.Compiled);

    public static IReadOnlyList<PathNode> Parse(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath)) return [];

        List<PathNode> nodes = [];
        foreach (Match m in TupleRegex.Matches(rawPath))
        {
            bool isStory = m.Groups[1].Value is "t" or "true";
            int nodeId = int.Parse(m.Groups[2].Value);
            nodes.Add(new PathNode(isStory, nodeId));
        }
        return nodes;
    }

    /// <summary>The story-typed node ids along the path, in traversal order (root story included when
    /// the root itself was a story). User hops are dropped entirely — never surfaced, per the privacy
    /// model.</summary>
    public static IReadOnlyList<int> StoryIdsOnly(string? rawPath) =>
        Parse(rawPath).Where(n => n.IsStory).Select(n => n.NodeId).ToArray();
}
