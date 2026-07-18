using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Base class for all integration test classes in the "Postgres" collection.
/// Owns the per-test reset/seeding infrastructure — see testing.md
/// "Integration tests reset between every test (Respawn)."
///
/// <b>Lifecycle per test:</b>
/// <list type="number">
///   <item><description><see cref="InitializeAsync"/> (this base): resets DB rows via Respawn, then
///   resets the shared host's in-memory state (<see cref="ResetSharedHostState"/>). The host itself
///   (<see cref="Factory"/>) is built once collection-wide by <see cref="PostgresFixture"/> — never
///   per test.</description></item>
///   <item><description><see cref="InitializeAsync"/> (override in child): seeds the rows this test
///   needs (users, stories) via the helper methods below.</description></item>
///   <item><description>Test body runs.</description></item>
///   <item><description><see cref="DisposeAsync"/>: nothing to tear down — the shared host outlives
///   the test, and Respawn wipes application rows before the next test.</description></item>
/// </list>
///
/// <b>Override contract:</b> if a child class overrides <see cref="InitializeAsync"/>, it MUST
/// call <c>await base.InitializeAsync()</c> first so the reset is done before any seeding helper
/// is used.
/// </summary>
public abstract class IntegrationTestBase(PostgresFixture postgres) : IAsyncLifetime
{
    /// <summary>
    /// The collection-wide host, shared across every test (built once by <see cref="PostgresFixture"/>).
    /// Its DB is reset per test by Respawn; its in-memory state by <see cref="ResetSharedHostState"/>.
    /// </summary>
    protected TestAppFactory Factory => postgres.Factory;

    public virtual async Task InitializeAsync()
    {
        await postgres.ResetAsync();
        ResetSharedHostState();
    }

    public virtual Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Restores the shared host's in-memory singleton state to its clean baseline before each test.
    /// Respawn (above) resets DB <i>rows</i>; this resets the process-memory state that would
    /// otherwise leak across tests on the shared host. <b>This is the COMPLETE set of stateful
    /// singletons in the host</b> (the real rate-limiter is swapped for a stateless fake; the import
    /// service and sprite probe are stateless) — if a new stateful singleton is ever registered in
    /// <c>Program.cs</c>, it MUST be reset here. See testing.md "Integration test host is shared
    /// collection-wide."
    /// </summary>
    private void ResetSharedHostState()
    {
        // Anonymous() == new() == the default a freshly-built host's FakeActiveUserContext would have.
        SetActiveUser(FakeActiveUserContext.Anonymous());
        Factory.Services.GetRequiredService<ViewCountBuffer>().Clear();
        Factory.Services.GetRequiredService<ReadingProgressBuffer>().Clear();
        Factory.Services.GetRequiredService<UserActivityBuffer>().Clear();
    }

    // ── Seeding helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new user via <see cref="UserManager{TUser}"/> and returns the new <c>UserId</c>.
    /// Each call produces a unique GUID-suffixed username/email so tests are independent of each other,
    /// even when the same <paramref name="name"/> label is passed. The label is for human-readable
    /// failure messages only; always use the returned <c>UserId</c> int, never the username string.
    /// Never query by username ("TestUser") or hardcode an id.
    /// </summary>
    protected async Task<int> SeedUserAsync(string? name = null, bool showMature = false)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        UserManager<User> userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        User user = new()
        {
            // Always append a GUID suffix — even when a label is supplied — so that tests using
            // the same label (e.g. "NS-B") in the same test run never collide. The label is for
            // human readability in failure messages only; tests must use the returned UserId.
            UserName      = name != null ? $"{name}-{suffix}" : $"TestUser-{suffix}",
            Email         = $"test-{suffix}@test.invalid",
            EmailConfirmed = true,
            ShowMatureContent = showMature,
            ThemeId       = 1   // "Pokémon" default — seeded by EF HasData, survives Respawn
        };

        IdentityResult result = await userManager.CreateAsync(user, "Password123!");
        result.Succeeded.Should().BeTrue(
            $"SeedUserAsync: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        return user.Id;
    }

    /// <summary>
    /// Inserts a minimal story row and returns the new <c>StoryId</c>.
    /// <paramref name="authorId"/> is optional; pass the seeded user's id when a test needs
    /// author-owned story semantics (e.g. Hidden-Gem notification, highlight-ownership guard).
    /// </summary>
    protected async Task<int> SeedStoryAsync(int? authorId = null, Rating rating = Rating.E)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        Story story = new()
        {
            AuthorId        = authorId,
            Rating          = rating,
            StoryStatusId   = StoryStatusEnum.InProgress,
            PublishedDate   = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow,
            StoryListing    = new StoryListing  { StoryTitle = $"Test Story {suffix}", ShortDescription = "test" },
            StoryDetail     = new StoryDetail   { LongDescription = "test", PostApprovalStatus = StoryStatusEnum.InProgress }
        };
        db.Stories.Add(story);
        await db.SaveChangesAsync();
        return story.StoryId;
    }

    /// <summary>
    /// Swaps the <see cref="FakeActiveUserContext"/> singleton that the current factory's
    /// <see cref="IActiveUserContext"/> resolves to. Call before the service call under test.
    /// Use <see cref="FakeActiveUserContext.AuthenticatedUser"/> or <see cref="FakeActiveUserContext.Anonymous"/>
    /// to construct the value.
    /// </summary>
    protected void SetActiveUser(FakeActiveUserContext ctx)
    {
        FakeActiveUserContext fake = Factory.Services.GetRequiredService<FakeActiveUserContext>();
        fake.UserId                 = ctx.UserId;
        fake.IsAuthenticated        = ctx.IsAuthenticated;
        fake.ShowMatureContent      = ctx.ShowMatureContent;
        fake.Theme                  = ctx.Theme;
        fake.PrefersAnimatedSprites = ctx.PrefersAnimatedSprites;
        fake.IsModerator            = ctx.IsModerator;
        fake.IsAdmin                = ctx.IsAdmin;
    }

    /// <summary>
    /// Convenience overload: sets the fake to authenticated as <paramref name="userId"/> with
    /// <c>ShowMatureContent = false</c>. Equivalent to
    /// <c>SetActiveUser(FakeActiveUserContext.AuthenticatedUser(userId, showMatureContent: false))</c>.
    /// </summary>
    protected void SetActiveUser(int userId)
    {
        FakeActiveUserContext fake = Factory.Services.GetRequiredService<FakeActiveUserContext>();
        fake.UserId            = userId;
        fake.IsAuthenticated   = true;
        fake.ShowMatureContent = false;
        fake.IsModerator       = false;
        fake.IsAdmin           = false;
    }
}
