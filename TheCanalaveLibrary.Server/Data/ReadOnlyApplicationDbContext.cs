using Microsoft.EntityFrameworkCore;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// A DbContext specifically for read-only operations. It disables the change
/// tracker by default for significantly better query performance.
/// </summary>
public class ReadOnlyApplicationDbContext : ApplicationDbContext
{
    public ReadOnlyApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }
}