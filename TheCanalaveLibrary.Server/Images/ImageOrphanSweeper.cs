using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The daily orphan-blob sweep: the fault-class backstop of owner ruling D14 (`layer2-services.md`
/// §"Image Storage — blob ownership and cleanup"). It deletes stored uploads that no row names — the
/// residue of a blob write whose column write then failed, or of an inline delete that failed. It is
/// <b>never</b> the mechanism of erasure: every terminal deletion path deletes its own blobs inline.
///
/// <para>Order of one sweep, and why each guard is mandatory:</para>
/// <list type="number">
/// <item>The reference set is read first, <b>unfiltered</b>, from the write context.</item>
/// <item>The store is streamed (<see cref="IImageStorageService.EnumerateAsync"/>, upload prefixes
/// only) and anti-joined against it, in that direction only: store → DB.</item>
/// <item>A blob is a candidate only if no row names it, its key has the exact generated shape
/// (<see cref="ImageUploadRules.IsGeneratedKey"/>), and it is older than <see cref="GraceWindow"/>.</item>
/// <item>A dry run only logs; a real run attempts at most <see cref="MaxDeletesPerCycle"/> deletes.</item>
/// <item>Rows that name a missing blob are logged and never acted on.</item>
/// </list>
/// Separate from <see cref="ImageOrphanSweepWorker"/> so integration tests drive it directly (the
/// <c>NotificationCleanupSweeper</c> pattern; <c>TestAppFactory</c> removes the worker).
/// </summary>
public sealed class ImageOrphanSweeper(
    ApplicationDbContext writeDb,
    IImageStorageService storage,
    ILogger<ImageOrphanSweeper> logger)
{
    /// <summary>A blob younger than this is never deleted, so the sweep cannot race an upload whose
    /// column write has not landed yet (D14).</summary>
    public static readonly TimeSpan GraceWindow = TimeSpan.FromHours(24);

    /// <summary>Most deletes one real cycle attempts — bounds the blast radius of a wrong predicate
    /// (D14). An implementation constant, not an owner number.</summary>
    public const int MaxDeletesPerCycle = 100;

    /// <summary>How many rows-naming-a-missing-blob paths the Warning quotes.</summary>
    private const int DanglingSampleSize = 10;

    /// <summary>Runs one sweep. <paramref name="dryRun"/> logs what a real cycle would delete and
    /// deletes nothing.</summary>
    public async Task<ImageOrphanSweepResult> SweepAsync(bool dryRun, CancellationToken ct = default)
    {
        using Activity? activity = CanalaveTelemetry.ImageStorage.Source.StartActivity("ImageStorage.OrphanSweep");
        activity?.SetTag("canalave.image.sweep.dry_run", dryRun);

        // (1) The reference set, read FIRST and UNFILTERED. This is the one failure mode that destroys
        // real user data: a filtered read (takedown, draft status, rating, a viewer's mature setting)
        // hides rows that still name their blobs, and every hidden cover would be deleted as an
        // orphan. The write context carries no query filters, and IgnoreQueryFilters() stays on as a
        // second guard, because ReadOnlyApplicationDbContext IS an ApplicationDbContext and a
        // mis-injected read context would otherwise compile and run silently.
        HashSet<string> referenced = new(StringComparer.Ordinal);
        await foreach (string path in writeDb.StoryListings.IgnoreQueryFilters().AsNoTracking()
                           .Where(l => l.CoverArtRelativeUrl != null)
                           .Select(l => l.CoverArtRelativeUrl!)
                           .AsAsyncEnumerable().WithCancellation(ct))
        {
            referenced.Add(path);
        }
        await foreach (string path in writeDb.Users.IgnoreQueryFilters().AsNoTracking()
                           .Where(u => u.ProfilePictureRelativeUrl != null)
                           .Select(u => u.ProfilePictureRelativeUrl!)
                           .AsAsyncEnumerable().WithCancellation(ct))
        {
            referenced.Add(path);
        }

        DateTime cutoff = DateTime.UtcNow - GraceWindow;
        int scanned = 0, candidates = 0, attempts = 0, deleted = 0, deleteFailures = 0, inGrace = 0, foreign = 0;
        bool capReached = false;

        // Matches are recorded in a second set rather than removed from the first: a store that ever
        // listed one key twice must not see it as unreferenced the second time.
        HashSet<string> found = new(StringComparer.Ordinal);

        // (2) Store → DB, never the reverse.
        await foreach (StoredImage blob in storage.EnumerateAsync(ct))
        {
            scanned++;

            if (referenced.Contains(blob.RelativePath))
            {
                found.Add(blob.RelativePath);
                continue;
            }

            // Only a key this service family generated is ours to delete.
            if (!ImageUploadRules.IsGeneratedKey(ImageUploadRules.TryExtractKey(blob.RelativePath)))
            {
                foreign++;
                continue;
            }

            if (blob.LastModifiedUtc > cutoff)
            {
                inGrace++;
                continue;
            }

            candidates++;

            // The cap counts attempts, so a store that fails every delete is still bounded, and a dry
            // run logs exactly the deletes the next real cycle would attempt.
            if (attempts >= MaxDeletesPerCycle)
            {
                capReached = true;
                continue;
            }
            attempts++;

            if (dryRun)
            {
                logger.LogInformation(
                    "Orphan sweep (dry run) would delete {ImagePath}, last modified {LastModifiedUtc:o}",
                    blob.RelativePath, blob.LastModifiedUtc);
                continue;
            }

            try
            {
                await storage.DeleteAsync(blob.RelativePath);
                deleted++;
                CanalaveTelemetry.ImageStorage.OrphansDeleted.Add(1);
                logger.LogInformation(
                    "Orphan sweep deleted {ImagePath}, last modified {LastModifiedUtc:o}",
                    blob.RelativePath, blob.LastModifiedUtc);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                deleteFailures++;
                logger.LogWarning(ex, "Orphan sweep could not delete {ImagePath}; the next cycle retries it",
                    blob.RelativePath);
            }
        }

        // (3) Rows naming a blob the store does not hold: a different, non-destructive defect. Logged,
        // never acted on (D14) — nulling the column would destroy the only trace of what was lost.
        List<string> dangling = referenced
            .Where(path => !found.Contains(path)
                           && ImageUploadRules.IsGeneratedKey(ImageUploadRules.TryExtractKey(path)))
            .ToList();
        if (dangling.Count > 0)
        {
            logger.LogWarning(
                "Orphan sweep found {DanglingCount} row(s) naming a blob the store does not hold; not acted on. Sample: {DanglingSample}",
                dangling.Count, string.Join(", ", dangling.Take(DanglingSampleSize)));
        }

        ImageOrphanSweepResult result = new(
            Scanned: scanned,
            Candidates: candidates,
            Deleted: deleted,
            DeleteFailures: deleteFailures,
            SkippedInGrace: inGrace,
            SkippedForeign: foreign,
            DanglingReferences: dangling.Count,
            CapReached: capReached,
            DryRun: dryRun);

        activity?.SetTag("canalave.image.sweep.scanned", result.Scanned);
        activity?.SetTag("canalave.image.sweep.candidates", result.Candidates);
        activity?.SetTag("canalave.image.sweep.deleted", result.Deleted);
        activity?.SetTag("canalave.image.sweep.dangling", result.DanglingReferences);
        activity?.SetTag("canalave.image.sweep.cap_reached", result.CapReached);

        logger.LogInformation(
            "Orphan sweep finished (dry run: {DryRun}): scanned {Scanned}, candidates {Candidates}, deleted {Deleted}, " +
            "delete failures {DeleteFailures}, in grace {SkippedInGrace}, foreign {SkippedForeign}, dangling {DanglingReferences}",
            result.DryRun, result.Scanned, result.Candidates, result.Deleted, result.DeleteFailures,
            result.SkippedInGrace, result.SkippedForeign, result.DanglingReferences);

        if (result.CapReached)
        {
            logger.LogWarning(
                "Orphan sweep reached its cap of {MaxDeletesPerCycle} deletes with {Candidates} candidates; " +
                "the rest wait for later cycles. Check why so many blobs are orphaned before trusting the predicate.",
                MaxDeletesPerCycle, result.Candidates);
        }

        return result;
    }
}

/// <summary>One sweep's outcome. <see cref="Deleted"/> is always 0 on a dry run, whose would-be
/// deletes (logged one by one) are the first <see cref="ImageOrphanSweeper.MaxDeletesPerCycle"/> of
/// <see cref="Candidates"/>. <see cref="CapReached"/> means more candidates than one cycle may attempt.</summary>
public sealed record ImageOrphanSweepResult(
    int Scanned,
    int Candidates,
    int Deleted,
    int DeleteFailures,
    int SkippedInGrace,
    int SkippedForeign,
    int DanglingReferences,
    bool CapReached,
    bool DryRun);
