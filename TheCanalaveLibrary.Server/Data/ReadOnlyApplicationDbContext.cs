using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// A DbContext specifically for read-only operations. It disables the change
/// tracker by default for significantly better query performance.
/// </summary>
public class ReadOnlyApplicationDbContext : ApplicationDbContext
{
    public ReadOnlyApplicationDbContext(DbContextOptions<ReadOnlyApplicationDbContext> options, IActiveUserContext activeUser)
        : base(options, activeUser)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }
}