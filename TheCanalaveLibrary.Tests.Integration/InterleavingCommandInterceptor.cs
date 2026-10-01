using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// A deterministic "someone else got there first" for check-then-act guards (testing.md §"Testing a
/// check-then-act guard: interleave, don't race"). A service that pre-reads a row, checks it, and then
/// writes with a conditional <c>WHERE</c> can only be tested on that conditional if the competing write
/// lands strictly BETWEEN the read and the write — two sequential calls are always stopped by the
/// pre-read check, and two racing calls (<c>Task.WhenAll</c>) leave it to timing which guard fires.
/// <para>
/// This interceptor watches the non-query commands (<c>ExecuteUpdate</c>/<c>ExecuteDelete</c>) issued by
/// ONE context and, the first time a command's text contains <see cref="CommandMarker"/>, either runs
/// <see cref="InterleavedSql"/> on a separate autocommit connection just before that command executes
/// (the competing write), or throws <see cref="FailWith"/> instead of executing it (to prove the
/// surrounding transaction rolls back). It fires once.
/// </para>
/// <para>
/// Use <see cref="CreateService{TService}"/> to build the service under test over a context carrying
/// this interceptor; every other dependency resolves from the shared host's scope, so the service is
/// the production type wired as in production apart from its write context.
/// </para>
/// </summary>
public sealed class InterleavingCommandInterceptor(
    string connectionString, string commandMarker,
    string? interleavedSql = null, Exception? failWith = null,
    bool interceptReaders = false) : DbCommandInterceptor
{
    private int _fired;

    /// <summary>Substring identifying the command to interleave before (e.g. <c>UPDATE stories</c>).</summary>
    public string CommandMarker { get; } = commandMarker;

    /// <summary>SQL run on a separate connection just before the marked command executes.</summary>
    public string? InterleavedSql { get; } = interleavedSql;

    /// <summary>When set, thrown in place of executing the marked command.</summary>
    public Exception? FailWith { get; } = failWith;

    /// <summary>True once the marked command was seen — assert it, so a test whose marker stopped
    /// matching (renamed table, changed write shape) fails loudly instead of passing vacuously.</summary>
    public bool Fired => Volatile.Read(ref _fired) == 1;

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await InterleaveIfMarkedAsync(command, cancellationToken);
        return result;
    }

    /// <summary>
    /// Reader commands too, only when the constructor's <c>interceptReaders</c> is set: a
    /// <c>SaveChangesAsync</c> INSERT runs as a reader (<c>INSERT … RETURNING</c> the generated key), so a
    /// test that needs a competing row to land just before an insert — e.g. a unique-index race — opts in
    /// (WU-ModerationIntegrity). Off by default so existing markers keep matching only the non-query
    /// <c>ExecuteUpdate</c>/<c>ExecuteDelete</c> commands they were written for.
    /// </summary>
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (interceptReaders)
            await InterleaveIfMarkedAsync(command, cancellationToken);
        return result;
    }

    private async Task InterleaveIfMarkedAsync(DbCommand command, CancellationToken cancellationToken)
    {
        if (command.CommandText.Contains(CommandMarker, StringComparison.Ordinal)
            && Interlocked.Exchange(ref _fired, 1) == 0)
        {
            if (FailWith is not null)
                throw FailWith;

            if (InterleavedSql is not null)
            {
                await using NpgsqlConnection competing = new(connectionString);
                await competing.OpenAsync(cancellationToken);
                await using NpgsqlCommand competingWrite = new(InterleavedSql, competing);
                await competingWrite.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    /// <summary>
    /// Builds <typeparamref name="TService"/> over a fresh <see cref="ApplicationDbContext"/> that carries
    /// <paramref name="interceptor"/> — configured exactly as <see cref="TestAppFactory"/> registers the
    /// write context — with every other constructor dependency resolved from <paramref name="scope"/>.
    /// The caller owns the returned context (dispose it with the scope).
    /// </summary>
    public static (TService Service, ApplicationDbContext WriteDb) CreateService<TService>(
        IServiceScope scope, string connectionString, InterleavingCommandInterceptor interceptor)
        where TService : notnull
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure())
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptor)
            .Options;
        ApplicationDbContext writeDb = new(options, scope.ServiceProvider.GetRequiredService<IActiveUserContext>());
        TService service = ActivatorUtilities.CreateInstance<TService>(scope.ServiceProvider, writeDb);
        return (service, writeDb);
    }
}
