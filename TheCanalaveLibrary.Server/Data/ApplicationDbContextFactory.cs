using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Design-time-only factory so `dotnet ef migrations` commands can build <see cref="ApplicationDbContext"/>
/// without going through the full web host DI pipeline (which requires Aspire service discovery and an
/// <c>IDbContextFactory&lt;T&gt;</c> registration the runtime app doesn't have wired up — see
/// cross-cutting.md "Dual-Configuration Strategy"). The connection string here is a placeholder: it is
/// never opened for `migrations add` (EF only needs the provider + naming convention to build the model),
/// but IS opened for `database update`/`dotnet ef database update` — point it at a real instance (or pass
/// `--connection`) before running those.
/// </summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder
            .UseNpgsql("Host=localhost;Database=canalavedb;Username=postgres;Password=postgres")
            .UseSnakeCaseNamingConvention();

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
