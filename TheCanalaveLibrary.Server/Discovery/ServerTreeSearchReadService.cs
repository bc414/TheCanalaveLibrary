using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server implementation of <see cref="ITreeSearchReadService"/> (Feature 59, Automatic Tree
/// Search): one live recursive CTE over the <c>user_story_tree_search_entries</c> edge-list mart.
///
/// <para><b>Traversal shape</b> (layer8-data-marts.md §"The Automatic Tree Search consumer"):
/// the bipartite User↔Story graph alternates node kinds each degree. The recursive term is lean
/// (node kind + id + degree only — no score column; every edge is worth 1) and uses a LATERAL
/// union so each direction rides its covering index
/// (<c>ix_tree_search_story_edge</c> / <c>ix_tree_search_user_edge</c>) with a per-node
/// fan-out <c>LIMIT</c> — the wide-mode supernode guard (deep mode is structurally protected by
/// the ≤5 caps on HiddenGem/AuthorSpotlight). The PG14+ <c>CYCLE</c> clause prunes revisits and
/// materializes the path natively — no hand-rolled visited tracking. AD3's narrow mart is what
/// makes this static SQL: edge selection is <c>edge_type = ANY(@edges)</c>, not dynamic JOIN
/// composition.</para>
///
/// <para><b>Filters at the presentation join (AD7), never in traversal:</b> visibility, the
/// viewer's content rating, and the viewer's effective §8.7 exclusions for the AutoTreeSearch
/// mode. A mature story can therefore be a silent bridge node — used as a connector, never
/// returned. Traversal returns story IDs only; edge-owner identity is never exposed.</para>
///
/// <para><b>Ordering:</b> two sort orders over the same result set — random shuffle, or minimum
/// degree-to-reach ascending (random within a degree). No path score, no edge weights.</para>
/// </summary>
public class ServerTreeSearchReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser,
    IDiscoveryDefaultsReadService discoveryDefaults) : ITreeSearchReadService
{
    /// <summary>Per-node expansion cap inside the recursive step (wide-mode flooding guard).
    /// Deep chain-of-trust edges never approach it (≤5 structurally).</summary>
    internal const int FanOutCap = 50;

    /// <summary>Hard ceiling on <see cref="TreeSearchRequest.MaxDegrees"/> — beyond ~8 the
    /// bipartite graph is effectively fully reachable and the walk is pure cost.</summary>
    internal const int MaxDegreesCeiling = 8;

    /// <summary>The only edge sets that may materialize paths: the truly-capped chain-of-trust
    /// edges, where a single shortest path is meaningful (AD1/AD2).</summary>
    internal static readonly IReadOnlySet<TreeSearchEdgeType> PathCapableEdges =
        new HashSet<TreeSearchEdgeType> { TreeSearchEdgeType.HiddenGem, TreeSearchEdgeType.AuthorSpotlight };

    public async Task<TreeSearchResultDto> TraverseAsync(TreeSearchRequest request, CancellationToken ct = default)
    {
        Validate(request);

        using Activity? activity = CanalaveTelemetry.Discovery.Source.StartActivity("Discovery.TreeSearchTraverse");
        activity?.SetTag("canalave.treesearch.max_degrees", request.MaxDegrees);
        activity?.SetTag("canalave.treesearch.edge_type_count", request.EdgeTypes.Count);
        activity?.SetTag("canalave.treesearch.include_paths", request.IncludePaths);
        long startTimestamp = Stopwatch.GetTimestamp();

        IReadOnlyList<UserStoryInteractionTypeEnum> exclusions =
            await discoveryDefaults.GetDefaultExcludedInteractionsAsync(SiteSearchModes.AutoTreeSearch);

        const string sql = $"""
            WITH RECURSIVE traversal (is_story, node_id, degree) AS (
                SELECT @rootIsStory, @rootId, 0
                UNION ALL
                SELECT nxt.next_is_story, nxt.next_id, t.degree + 1
                FROM traversal t
                JOIN LATERAL (
                    (SELECT e.user_id AS next_id, false AS next_is_story
                     FROM {DiscoveryMartSchema.TreeSearchTable} e
                     WHERE t.is_story AND e.story_id = t.node_id AND e.edge_type = ANY(@edges)
                     LIMIT @fanOutCap)
                    UNION ALL
                    (SELECT e2.story_id AS next_id, true AS next_is_story
                     FROM {DiscoveryMartSchema.TreeSearchTable} e2
                     WHERE (NOT t.is_story) AND e2.user_id = t.node_id AND e2.edge_type = ANY(@edges)
                     LIMIT @fanOutCap)
                ) nxt ON true
                WHERE t.degree < @maxDegrees
            ) CYCLE is_story, node_id SET is_cycle USING path,
            hits AS (
                SELECT t.node_id AS story_id,
                       MIN(t.degree) AS degree,
                       (array_agg(t.path::text ORDER BY t.degree ASC))[1] AS shortest_path
                FROM traversal t
                WHERE t.is_story AND t.degree > 0
                GROUP BY t.node_id
            ),
            filtered AS (
                SELECT h.story_id, h.degree, h.shortest_path
                FROM hits h
                JOIN stories s ON s.story_id = h.story_id
                WHERE {DiscoveryMartSchema.VisibleStory}
                  AND s.rating <= @maxRating
                  AND NOT (@rootIsStory AND h.story_id = @rootId)
                  AND NOT EXISTS (
                      SELECT 1 FROM user_story_interactions x
                      WHERE x.user_id = @viewerId AND x.story_id = h.story_id
                        AND ((@exFavorite AND x.is_favorite)
                          OR (@exHiddenFavorite AND x.is_hidden_favorite)
                          OR (@exFollowed AND x.is_followed)
                          OR (@exCompleted AND x.is_completed)
                          OR (@exReadItLater AND x.is_read_it_later)
                          OR (@exIgnored AND x.is_ignored)))
            )
            SELECT f.story_id, f.degree,
                   CASE WHEN @includePaths THEN f.shortest_path END AS shortest_path,
                   COUNT(*) OVER () AS total_hits
            FROM filtered f
            ORDER BY CASE WHEN @sortByDegree THEN f.degree END ASC NULLS LAST, random()
            LIMIT @resultCap
            """;

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync(ct);
        await readDb.Database.OpenConnectionAsync(ct);
        DbConnection connection = readDb.Database.GetDbConnection();

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new NpgsqlParameter("rootIsStory", request.RootStoryId.HasValue));
        command.Parameters.Add(new NpgsqlParameter("rootId", request.RootStoryId ?? request.RootUserId!.Value));
        command.Parameters.Add(new NpgsqlParameter("maxDegrees", request.MaxDegrees));
        command.Parameters.Add(new NpgsqlParameter("fanOutCap", FanOutCap));
        command.Parameters.Add(new NpgsqlParameter("edges", request.EdgeTypes.Select(e => (short)e).Distinct().ToArray()));
        command.Parameters.Add(new NpgsqlParameter("maxRating", (short)(activeUser.ShowMatureContent ? Rating.M : Rating.T)));
        command.Parameters.Add(new NpgsqlParameter("viewerId", activeUser.UserId ?? -1));
        command.Parameters.Add(new NpgsqlParameter("includePaths", request.IncludePaths));
        command.Parameters.Add(new NpgsqlParameter("sortByDegree", request.Sort == TreeSearchSortOrder.ByDegree));
        command.Parameters.Add(new NpgsqlParameter("resultCap", request.ResultCap));
        ServerCoOccurrenceReadService.AddExclusionParameters(command, exclusions);

        List<TreeSearchHitDto> hits = [];
        long totalHits = 0;
        await using (DbDataReader reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                hits.Add(new TreeSearchHitDto
                {
                    StoryId = reader.GetInt32(0),
                    Degree = reader.GetInt32(1),
                    Path = reader.IsDBNull(2) ? null : reader.GetString(2),
                });
                totalHits = reader.GetInt64(3);
            }
        }

        int degreesReached = hits.Count > 0 ? hits.Max(h => h.Degree) : 0;
        bool truncated = totalHits > hits.Count;

        double durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        CanalaveTelemetry.Discovery.TraversalDuration.Record(durationMs);
        CanalaveTelemetry.Discovery.DegreesReached.Record(degreesReached);
        CanalaveTelemetry.Discovery.ResultCount.Record(hits.Count);
        if (truncated) CanalaveTelemetry.Discovery.CapTruncations.Add(1);
        activity?.SetTag("canalave.treesearch.degrees_reached", degreesReached);
        activity?.SetTag("canalave.treesearch.result_count", hits.Count);
        activity?.SetTag("canalave.treesearch.cap_truncated", truncated);

        return new TreeSearchResultDto
        {
            Hits = hits,
            DegreesReached = degreesReached,
            ResultCapTruncated = truncated,
        };
    }

    /// <summary>Request-shape rules (unit-tested): exactly one root; ≥1 edge type; sane degree
    /// and cap bounds; paths only on the chain-of-trust edge sets.</summary>
    internal static void Validate(TreeSearchRequest request)
    {
        if (request.RootStoryId.HasValue == request.RootUserId.HasValue)
            throw new ArgumentException("Exactly one of RootStoryId / RootUserId must be set.", nameof(request));
        if (request.EdgeTypes is not { Count: > 0 })
            throw new ArgumentException("At least one edge type must be selected.", nameof(request));
        if (request.MaxDegrees < 1 || request.MaxDegrees > MaxDegreesCeiling)
            throw new ArgumentException($"MaxDegrees must be between 1 and {MaxDegreesCeiling}.", nameof(request));
        if (request.ResultCap < 1)
            throw new ArgumentException("ResultCap must be positive.", nameof(request));
        if (request.IncludePaths && !request.EdgeTypes.All(PathCapableEdges.Contains))
            throw new ArgumentException(
                "Path materialization is only available when the selected edges are all chain-of-trust " +
                "edges (HiddenGem, AuthorSpotlight) — unbounded edges yield combinatorially noisy paths.",
                nameof(request));
    }
}
