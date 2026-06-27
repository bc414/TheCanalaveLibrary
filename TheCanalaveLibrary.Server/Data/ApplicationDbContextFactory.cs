using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TheCanalaveLibrary.Core;

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

        // Design-time only — this runs outside the DI/auth pipeline for `dotnet ef` tooling, so there's
        // no real viewer. The content-rating query filter (settled WU12, cross-cutting.md "Content
        // Rating Filtering") gets the same safe anonymous/Teen-ceiling default ServerActiveUserContext
        // falls back to at runtime — irrelevant here, since migration generation only inspects model
        // shape, never query results.
        return new ApplicationDbContext(optionsBuilder.Options, new DesignTimeActiveUserContext());
    }

    private sealed class DesignTimeActiveUserContext : IActiveUserContext
    {
        public int? UserId => null;
        public bool IsAuthenticated => false;
        public bool ShowMatureContent => false;
        public string Theme => "pokemon"; // URL-safe slug; display name is "Pokémon"
        public bool PrefersAnimatedSprites => true;
        public bool IsModerator => false;
        public bool IsAdmin => false;
    }
}
