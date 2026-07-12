namespace TheCanalaveLibrary.Core;

/// <summary>
/// Manual Tree Search (Feature 33 / WU40): stateless, degree-1 pivot queries over LIVE tables —
/// never the discovery mart (manual needs freshness and edge detail — recommendation bodies,
/// badges — the IDs-only mart cannot carry). Each pivot is one fresh query; no traversal state
/// is ever persisted server-side (the client-curated tree lives in browser localStorage).
///
/// <para><b>Privacy model:</b> every edge this service exposes is a genuinely public action —
/// hidden favorites are excluded entirely (regardless of the owner's mart consent flag), and
/// incoming vouches are never traversable from a story (owner-private per spec §5.8). All
/// returned nodes carry real identity. See `audit/Discovery.md` Feature 33.</para>
///
/// <para>Direction is enforced by the request types: <see cref="StoryNeighborsRequest"/> asks
/// only story→user questions, <see cref="UserNeighborsRequest"/> only user→story ones. Content
/// rating and taken-down visibility ride the read context's global query filters; interaction
/// exclusion does NOT apply here (this is a relationship browser, not a results feed).</para>
/// </summary>
public interface IManualTreeSearchReadService
{
    /// <summary>All requested neighbor sections of one story, in one round trip.</summary>
    Task<ManualTreeNeighborsDto> GetStoryNeighborsAsync(StoryNeighborsRequest request, CancellationToken ct = default);

    /// <summary>All requested neighbor sections of one user, in one round trip.</summary>
    Task<ManualTreeNeighborsDto> GetUserNeighborsAsync(UserNeighborsRequest request, CancellationToken ct = default);

    /// <summary>
    /// Batch chip-display rehydration for a localStorage-persisted tree (IDs + edges only on the
    /// client). Returns display data only for entities the viewer can still see — callers prune
    /// tree nodes whose ids are absent from the result.
    /// </summary>
    Task<ManualTreeNodeDisplaysDto> GetNodeDisplaysAsync(
        IReadOnlyCollection<int> storyIds, IReadOnlyCollection<int> userIds, CancellationToken ct = default);
}
