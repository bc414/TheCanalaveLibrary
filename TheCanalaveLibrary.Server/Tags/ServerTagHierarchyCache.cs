using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Process-local cache of the whole tag parent→children map (~136 rows today; a few KB).
///
/// <para><b>Why this is cacheable when ISiteSettingsReadService deliberately is not:</b> the map
/// is tiny, identical for every viewer (no query filter touches <c>Tag</c> — verified against
/// <see cref="ReadOnlyApplicationDbContext"/>'s <c>OnModelCreating</c>), and changes only when a
/// moderator writes a tag through the single choke point <see cref="ServerTagWriteService"/>. Site
/// settings are a single-row read whose contract is "a mod edit takes effect on the next read"; a
/// hierarchy edit tolerating one cycle of staleness does not have that contract. See
/// layer2-services.md §"Reference-Data Caching".</para>
///
/// <para><b>Lifetime:</b> singleton. <c>IDbContextFactory&lt;ReadOnlyApplicationDbContext&gt;</c> is
/// registered scoped, so it cannot be injected here — the loader takes
/// <see cref="IServiceScopeFactory"/> and opens a fresh scope, the same discipline
/// <see cref="ViewCountFlusher"/> uses for its periodic flush. That scope's
/// <c>IActiveUserContext</c> resolves anonymous (no <c>HttpContext</c>), which is irrelevant
/// precisely because <c>Tag</c> carries no query filter.</para>
/// </summary>
public sealed class ServerTagHierarchyCache(
    IServiceScopeFactory scopeFactory,
    ILogger<ServerTagHierarchyCache> logger) : ITagHierarchyReadService
{
    /// <summary>
    /// Absolute ceiling on staleness, on top of write-invalidation. Two jobs: (1) it catches tag
    /// rows written outside <see cref="ServerTagWriteService"/> (DataSeeder, SeedTool, direct SQL);
    /// (2) it is what makes this design self-healing at N≥2 with ZERO shared-store work — each node
    /// converges within one window, so no Valkey dependency is added to the N≥2 checklist
    /// (horizontal-scaling.md). Cost at the ceiling: one ~136-row query per minute per process, and
    /// only when a filtered read actually happens.
    /// </summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly SemaphoreSlim _loadGate = new(1, 1);

    // Single reference, swapped atomically. Readers never lock: they take the reference once and
    // either use it or fall through to the gated reload. A reader racing an Invalidate() at worst
    // triggers one redundant load.
    private volatile Snapshot? _snapshot;

    private sealed record Snapshot(TagExpansionMap Map, long LoadedAt);

    public async Task<TagExpansionMap> GetExpansionMapAsync(CancellationToken cancellationToken = default)
    {
        Snapshot? current = _snapshot;
        if (current is not null && !IsExpired(current)) return current.Map; // warm path: no lock, no I/O

        await _loadGate.WaitAsync(cancellationToken);
        try
        {
            current = _snapshot; // re-check under the gate — a concurrent loader may have just finished
            if (current is not null && !IsExpired(current)) return current.Map;

            TagExpansionMap map = await LoadAsync(cancellationToken);
            _snapshot = new Snapshot(map, Stopwatch.GetTimestamp());
            return map;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    /// <summary>
    /// Drops the snapshot so the next read reloads. Called after every successful
    /// <see cref="ServerTagWriteService"/> write (broad trigger — any Tag write, not just
    /// ParentTagId changes: trivially correct and nearly free, per B12's own framing), and by
    /// <c>IntegrationTestBase.ResetSharedHostState</c> for per-test isolation.
    /// </summary>
    public void Invalidate() => _snapshot = null;

    private static bool IsExpired(Snapshot s) => Stopwatch.GetElapsedTime(s.LoadedAt) >= Ttl;

    private async Task<TagExpansionMap> LoadAsync(CancellationToken cancellationToken)
    {
        // Fresh scope: the read-context factory is scoped and must never be captured by a
        // singleton (layer2-services.md "Signal Buffering", flusher rule).
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IDbContextFactory<ReadOnlyApplicationDbContext> factory =
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<ReadOnlyApplicationDbContext>>();
        await using ReadOnlyApplicationDbContext readDb = await factory.CreateDbContextAsync(cancellationToken);

        // Same predicate as the retired per-request ExpandWithChildrenAsync, minus the ids.Contains
        // narrowing — this loads every child row once instead of only the caller's named ids.
        // ix_tags_parent_tag_id exists (EF FK convention); at 136 rows the planner correctly prefers
        // a seq scan (measured 0.02 ms, WU-TagFanon — audit/Tags.md L6 paragraph).
        var rows = await readDb.Tags
            .Where(t => t.ParentTagId != null)
            .Select(t => new { Parent = t.ParentTagId!.Value, t.TagId })
            .ToListAsync(cancellationToken);

        TagExpansionMap map = TagExpansionMap.FromChildRows(rows.Select(r => (r.Parent, r.TagId)));
        logger.LogDebug("Tag hierarchy map loaded: {ChildCount} child tags under {ParentCount} parents.",
            rows.Count, map.ParentCount);
        return map;

        // Deliberately no try/catch here: a failed load leaves _snapshot null (no poisoned
        // snapshot) and the exception surfaces at the same call sites the old per-request query
        // threw from. logging.md §"No Silent Catches".
    }
}
