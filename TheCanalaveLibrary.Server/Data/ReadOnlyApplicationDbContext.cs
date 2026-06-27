using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// A DbContext specifically for read-only operations. It disables the change tracker by default
/// for significantly better query performance, and adds all display/visibility query filters so
/// that public reads are automatically scoped to the active user's content ceiling.
///
/// <para>All named visibility filters (<c>"ContentRating"</c>, <c>"GroupAudience"</c>,
/// <c>"IsTakenDown"</c>) live here — NOT on the base <see cref="ApplicationDbContext"/>. The write
/// context sees ground truth; only the read context filters. See <c>cross-cutting.md</c> "Content
/// Rating Filtering" for the architectural principle.</para>
///
/// <para>This context owns no migration history — schema is managed entirely by
/// <see cref="ApplicationDbContext"/>. Query filters don't touch schema.</para>
/// </summary>
public class ReadOnlyApplicationDbContext : ApplicationDbContext
{
    public ReadOnlyApplicationDbContext(DbContextOptions<ReadOnlyApplicationDbContext> options, IActiveUserContext activeUser)
        : base(options, activeUser)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // entity shape, indexes, FKs, IEntityTypeConfiguration<T>

        // Display/visibility filters — all close over _activeUser (protected on ApplicationDbContext)
        // so EF re-evaluates per-instance, never freezing a cached lambda. A parameterless
        // IEntityTypeConfiguration<T> class has no DbContext instance to close over, so these must
        // live here. EF Core caches models per context type, so the base type remains unfiltered.

        // "Mature off ⇒ no trace of M-rated stories" (settled WU12).
        modelBuilder.Entity<Story>().HasQueryFilter("ContentRating",
            s => s.Rating <= (_activeUser.ShowMatureContent ? Rating.M : Rating.T));

        // "Mature off ⇒ no trace of Mature groups" (settled WU32).
        // Applies to Group only; child entities are unreachable once their parent group is filtered.
        modelBuilder.Entity<Group>().HasQueryFilter("GroupAudience",
            g => _activeUser.ShowMatureContent || g.AudienceRating != Rating.M);

        // Hides moderator-removed content from public reads (settled WU34).
        // Elevated read paths (mod queue) use .IgnoreQueryFilters(["IsTakenDown"]) — annotated
        // with // elevated read: so survivors are self-evidently deliberate.
        // A moderator's ContentRating reach equals their own ShowMatureContent; they bypass only
        // IsTakenDown, never ContentRating/GroupAudience.
        // Note: ContentRating is intentionally NOT added to BaseBlogPost — TPT + a filter that
        // closes over _activeUser's ShowMatureContent generates broken EF Core 10 SQL on derived-entity
        // materialization (WU31.5). The simple boolean IsTakenDown filter is safe on TPT roots
        // (confirmed: it was on ApplicationDbContext from WU34 onwards; 1228 tests passed with it).
        modelBuilder.Entity<Story>().HasQueryFilter("IsTakenDown", s => !s.IsTakenDown);
        modelBuilder.Entity<BaseComment>().HasQueryFilter("IsTakenDown", c => !c.IsTakenDown);
        modelBuilder.Entity<BaseBlogPost>().HasQueryFilter("IsTakenDown", b => !b.IsTakenDown);
        modelBuilder.Entity<Recommendation>().HasQueryFilter("IsTakenDown", r => !r.IsTakenDown);
    }
}