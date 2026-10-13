using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// <see cref="ImageOrphanSweeper"/> — the daily backstop of owner ruling D14 (WU-BlobCleanup). Each test
/// builds the sweeper over its own <see cref="TempBlobStore"/> (the shared host's upload folder is never
/// reset) and the scope's database, writes key-shaped files directly, and ages them with
/// <c>File.SetLastWriteTimeUtc</c>. The query-filter trap is the test that matters most: a referenced
/// blob must survive even when the row naming it is invisible to readers (taken down, a draft, M-rated
/// for a mature-off viewer). Tier: Integration (the anti-join needs real Postgres rows).
/// </summary>
[Collection("Postgres")]
public sealed class ImageOrphanSweeperTests(PostgresFixture postgres) : IntegrationTestBase(postgres), IDisposable
{
    private static readonly TimeSpan PastGrace = ImageOrphanSweeper.GraceWindow + TimeSpan.FromHours(1);
    private static readonly TimeSpan InsideGrace = TimeSpan.FromHours(1);

    private readonly TempBlobStore _store = new();
    private readonly FakeLogger<ImageOrphanSweeper> _log = new();
    private readonly List<IServiceScope> _scopes = [];

    public void Dispose()
    {
        foreach (IServiceScope scope in _scopes) scope.Dispose();
        _store.Dispose();
    }

    private ImageOrphanSweeper BuildSweeper(IImageStorageService? storage = null)
    {
        IServiceScope scope = Factory.Services.CreateScope();
        _scopes.Add(scope);
        return ActivatorUtilities.CreateInstance<ImageOrphanSweeper>(
            scope.ServiceProvider, storage ?? _store.Create(scope.ServiceProvider), _log);
    }

    private static string CoverKey(int storyId) => ImageUploadRules.BuildKey(ImageKind.Cover, storyId, "png");
    private static string AvatarKey(int userId) => ImageUploadRules.BuildKey(ImageKind.ProfilePicture, userId, "png");

    private async Task SetCoverAsync(int storyId, string path, Action<Story>? alsoSet = null)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Story story = await db.Stories.Include(s => s.StoryListing).SingleAsync(s => s.StoryId == storyId);
        story.StoryListing.CoverArtRelativeUrl = path;
        alsoSet?.Invoke(story);
        await db.SaveChangesAsync();
    }

    private async Task SetAvatarAsync(int userId, string path)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.ProfilePictureRelativeUrl, path));
    }

    /// <summary>Three stories no reader sees and one avatar, each naming an aged blob, plus one aged
    /// orphan (so the sweep provably deletes in the same run).</summary>
    private async Task<(string[] Referenced, string Orphan)> SeedTheFilterTrapAsync()
    {
        int authorId = await SeedUserAsync("trap-author");
        int takenDown = await SeedStoryAsync(authorId);
        int draft = await SeedStoryAsync(authorId, status: StoryStatusEnum.Draft);
        int mature = await SeedStoryAsync(authorId, rating: Rating.M);

        string takenDownCover = _store.WriteBlob(CoverKey(takenDown), PastGrace);
        string draftCover = _store.WriteBlob(CoverKey(draft), PastGrace);
        string matureCover = _store.WriteBlob(CoverKey(mature), PastGrace);
        string avatar = _store.WriteBlob(AvatarKey(authorId), PastGrace);
        string orphan = _store.WriteBlob(CoverKey(takenDown), PastGrace);

        await SetCoverAsync(takenDown, takenDownCover, s =>
        {
            s.IsTakenDown = true;
            s.TakedownDate = DateTime.UtcNow;
            s.TakedownReason = "test";
        });
        await SetCoverAsync(draft, draftCover);
        await SetCoverAsync(mature, matureCover);
        await SetAvatarAsync(authorId, avatar);

        // A mature-off viewer who is not the author: every filter on the read context hides these stories.
        int viewerId = await SeedUserAsync("trap-viewer");
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(viewerId, showMatureContent: false));

        return ([takenDownCover, draftCover, matureCover, avatar], orphan);
    }

    // ── The query-filter trap (the one failure mode that destroys real user data) ────────────────

    [Fact]
    public async Task ReferencedBlobs_Survive_EvenWhenNoReaderCanSeeTheirRows()
    {
        // Fails if the reference read stops seeing a row no reader can see: a reference query that
        // filters by takedown, status or rating itself (through a join, or a read service). It does NOT
        // catch a swap to the read context on its own: StoryListing and User carry no query filter on
        // either context, so that swap changes nothing here. A read routed through the filtered Story
        // entity of the read context without IgnoreQueryFilters is what the next test catches.
        (string[] referenced, string orphan) = await SeedTheFilterTrapAsync();

        ImageOrphanSweepResult result = await BuildSweeper().SweepAsync(dryRun: false);

        referenced.Should().OnlyContain(path => _store.Exists(path), "a referenced blob always survives");
        _store.Exists(orphan).Should().BeFalse("the same run does delete an unreferenced aged blob");
        result.Should().BeEquivalentTo(new ImageOrphanSweepResult(
            Scanned: 5, Candidates: 1, Deleted: 1, DeleteFailures: 0, SkippedInGrace: 0, SkippedForeign: 0,
            DanglingReferences: 0, CapReached: false, DryRun: false));
    }

    [Fact]
    public async Task ReferencedBlobs_Survive_EvenOverAMisInjectedReadContext()
    {
        // ReadOnlyApplicationDbContext IS an ApplicationDbContext, so a mis-injection compiles. Over it,
        // the reference read must stay unfiltered: today because it reads StoryListing and User, which
        // carry no filter, and IgnoreQueryFilters() as the second guard. Rerouting it through the
        // filtered Story entity without IgnoreQueryFilters fails this test (mutation-checked); the
        // precondition pins that the trap is real.
        (string[] referenced, string orphan) = await SeedTheFilterTrapAsync();
        IServiceScope scope = Factory.Services.CreateScope();
        _scopes.Add(scope);
        await using ReadOnlyApplicationDbContext readDb = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<ReadOnlyApplicationDbContext>>().CreateDbContextAsync();
        (await readDb.Stories.CountAsync()).Should().Be(0, "precondition: the read context hides all three stories");
        (await readDb.Stories.Select(s => s.StoryListing.CoverArtRelativeUrl).ToListAsync()).Should().BeEmpty(
            "precondition: a cover read through Story on this context misses every trap cover");

        ImageOrphanSweeper sweeper = new(readDb, _store.Create(scope.ServiceProvider), _log);
        ImageOrphanSweepResult result = await sweeper.SweepAsync(dryRun: false);

        referenced.Should().OnlyContain(path => _store.Exists(path));
        _store.Exists(orphan).Should().BeFalse();
        result.Deleted.Should().Be(1);
    }

    // ── Grace window, scope, direction ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AnUnreferencedBlob_PastTheGraceWindow_IsDeleted_AndOneInsideIt_IsKept()
    {
        string old = _store.WriteBlob(CoverKey(10), PastGrace);
        string fresh = _store.WriteBlob(AvatarKey(11), InsideGrace);

        ImageOrphanSweepResult result = await BuildSweeper().SweepAsync(dryRun: false);

        _store.Exists(old).Should().BeFalse();
        _store.Exists(fresh).Should().BeTrue("a blob under 24 h old may belong to an upload whose column write has not landed");
        result.Deleted.Should().Be(1);
        result.SkippedInGrace.Should().Be(1);
        _log.Collector.GetSnapshot().Should().Contain(r =>
            r.Level == LogLevel.Information && r.StructuredState!.Any(kv => kv.Key == "ImagePath" && kv.Value == old));
    }

    [Fact]
    public async Task AFileThatIsNotAGeneratedKey_IsNeverDeleted()
    {
        string[] foreign =
        [
            _store.WriteBlob("stories/5/notes.txt", PastGrace),
            _store.WriteBlob("stories/5/cover-not-a-guid.png", PastGrace),
            _store.WriteBlob("users/5/cover-0f8fad5b-d9cb-469f-a165-70867728950e.png", PastGrace), // kind ≠ folder
            _store.WriteBlob("stories/5/nested/cover-0f8fad5b-d9cb-469f-a165-70867728950e.png", PastGrace),
        ];
        string outsideTheKeySpace = _store.WriteBlob("sprites/pokemon/1.png", PastGrace);

        ImageOrphanSweepResult result = await BuildSweeper().SweepAsync(dryRun: false);

        foreign.Should().OnlyContain(path => _store.Exists(path));
        _store.Exists(outsideTheKeySpace).Should().BeTrue();
        result.SkippedForeign.Should().Be(4, "the four files inside the upload prefixes are listed and skipped");
        result.Scanned.Should().Be(4, "nothing outside stories/ and users/ is ever listed");
        result.Deleted.Should().Be(0);
    }

    [Fact]
    public async Task ARowNamingAMissingBlob_IsCountedAndLogged_NeverActedOn()
    {
        int authorId = await SeedUserAsync("dangling-author");
        int storyId = await SeedStoryAsync(authorId);
        string missing = ImageUploadRules.UploadsPrefix + CoverKey(storyId);
        await SetCoverAsync(storyId, missing);

        ImageOrphanSweepResult result = await BuildSweeper().SweepAsync(dryRun: false);

        result.DanglingReferences.Should().Be(1);
        _log.Collector.GetSnapshot().Should().Contain(r => r.Level == LogLevel.Warning
            && r.StructuredState!.Any(kv => kv.Key == "DanglingCount" && kv.Value == "1")
            && r.Message.Contains(missing));

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.StoryListings.Where(l => l.StoryId == storyId).Select(l => l.CoverArtRelativeUrl).SingleAsync())
            .Should().Be(missing, "direction is store → DB only: the row is left exactly as it was");
    }

    // ── Blast radius: dry run and the per-cycle cap ──────────────────────────────────────────────

    [Fact]
    public async Task ADryRun_DeletesNothing_ButReportsAndLogsTheCandidates()
    {
        string a = _store.WriteBlob(CoverKey(20), PastGrace);
        string b = _store.WriteBlob(AvatarKey(21), PastGrace);

        ImageOrphanSweepResult result = await BuildSweeper().SweepAsync(dryRun: true);

        _store.Exists(a).Should().BeTrue();
        _store.Exists(b).Should().BeTrue();
        result.Should().BeEquivalentTo(new ImageOrphanSweepResult(
            Scanned: 2, Candidates: 2, Deleted: 0, DeleteFailures: 0, SkippedInGrace: 0, SkippedForeign: 0,
            DanglingReferences: 0, CapReached: false, DryRun: true));
        _log.Collector.GetSnapshot().Count(r => r.Message.Contains("would delete")).Should().Be(2);
    }

    [Fact]
    public async Task MoreCandidatesThanTheCap_DeletesExactlyTheCap_AndReportsCapReached()
    {
        List<string> orphans = [];
        for (int i = 0; i < ImageOrphanSweeper.MaxDeletesPerCycle + 2; i++)
            orphans.Add(_store.WriteBlob(CoverKey(30), PastGrace));

        ImageOrphanSweepResult result = await BuildSweeper().SweepAsync(dryRun: false);

        result.Candidates.Should().Be(ImageOrphanSweeper.MaxDeletesPerCycle + 2);
        result.Deleted.Should().Be(ImageOrphanSweeper.MaxDeletesPerCycle);
        result.CapReached.Should().BeTrue();
        orphans.Count(path => _store.Exists(path)).Should().Be(2, "the rest wait for the next cycle");
        _log.Collector.GetSnapshot().Should().Contain(r =>
            r.Level == LogLevel.Warning && r.Message.Contains("reached its cap"));
    }

    [Fact]
    public async Task AFailedDelete_IsCountedAndLogged_AndTheSweepGoesOn()
    {
        string a = _store.WriteBlob(CoverKey(40), PastGrace);
        string b = _store.WriteBlob(CoverKey(41), PastGrace);
        IServiceScope scope = Factory.Services.CreateScope();
        _scopes.Add(scope);
        ThrowingDeleteImageStorageService failing = new(_store.Create(scope.ServiceProvider));

        ImageOrphanSweepResult result = await BuildSweeper(failing).SweepAsync(dryRun: false);

        failing.DeleteAttempts.Should().BeEquivalentTo([a, b], "one failure does not stop the cycle");
        result.DeleteFailures.Should().Be(2);
        result.Deleted.Should().Be(0);
        _log.Collector.GetSnapshot().Count(r => r.Level == LogLevel.Warning && r.Exception is IOException)
            .Should().Be(2);
    }
}
