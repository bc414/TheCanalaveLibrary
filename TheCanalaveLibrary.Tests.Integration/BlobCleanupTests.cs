using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Owner ruling D14's inline sites (WU-BlobCleanup): every terminal deletion path deletes the blobs its
/// rows named, after the commit, best-effort. Built at account deletion (the avatar) and the moderation
/// hard delete (the story cover). Each fault test injects a store whose delete throws and checks that the
/// deletion still completes, a Warning names the path, and the daily sweep then collects the blob — the
/// two owners composing. The Garage half of the hard delete is <see cref="BlobCleanupGarageTests"/>.
/// Tier: Integration.
/// </summary>
[Collection("Postgres")]
public sealed class BlobCleanupTests(PostgresFixture postgres) : IntegrationTestBase(postgres), IDisposable
{
    private static readonly TimeSpan PastGrace = ImageOrphanSweeper.GraceWindow + TimeSpan.FromHours(1);

    private readonly TempBlobStore _store = new();
    private readonly List<IServiceScope> _scopes = [];

    public void Dispose()
    {
        foreach (IServiceScope scope in _scopes) scope.Dispose();
        _store.Dispose();
    }

    private IServiceProvider NewScope()
    {
        IServiceScope scope = Factory.Services.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider;
    }

    private async Task<string> SaveAsync(IImageStorageService storage, ImageKind kind, int ownerId)
    {
        using MemoryStream png = new(TestImages.Png(4, 4));
        return await storage.SaveAsync(png, "image/png", kind, ownerId);
    }

    private async Task SetCoverAsync(int storyId, string path)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.StoryListings.Where(l => l.StoryId == storyId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.CoverArtRelativeUrl, path));
    }

    private async Task SetAvatarAsync(int userId, string path)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.ProfilePictureRelativeUrl, path));
    }

    private ImageOrphanSweeper SweeperOverTheStore(IServiceProvider services) =>
        ActivatorUtilities.CreateInstance<ImageOrphanSweeper>(services, _store.Create(services),
            new FakeLogger<ImageOrphanSweeper>());

    // ── Account deletion: the avatar goes, the surviving stories keep their covers ─────────────────

    [Fact]
    public async Task DeletingAUser_DeletesTheAvatarBlob_AndTheirSurvivingStoriesKeepTheirCovers()
    {
        int userId = await SeedUserAsync("blob-deleted-user");
        int storyA = await SeedStoryAsync(userId);
        int storyB = await SeedStoryAsync(userId);
        IServiceProvider services = NewScope();
        LocalImageStorageService storage = _store.Create(services);
        string avatar = await SaveAsync(storage, ImageKind.ProfilePicture, userId);
        string coverA = await SaveAsync(storage, ImageKind.Cover, storyA);
        string coverB = await SaveAsync(storage, ImageKind.Cover, storyB);
        await SetAvatarAsync(userId, avatar);
        await SetCoverAsync(storyA, coverA);
        await SetCoverAsync(storyB, coverB);

        bool deleted = await ActivatorUtilities.CreateInstance<UserDeletionService>(services, storage)
            .DeleteUserAsync(userId);

        deleted.Should().BeTrue();
        _store.Exists(avatar).Should().BeFalse("erasure: the photograph is gone when the account is gone (D14)");

        // Adapted from D14's test (its spec's X1): stories survive their author anonymized (D13), so their
        // covers stay referenced and intact. Whether to strip them is roadmap decision row 25.
        using (IServiceScope check = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stories = await db.Stories.Where(s => s.StoryId == storyA || s.StoryId == storyB)
                .Select(s => new { s.AuthorId, s.StoryListing.CoverArtRelativeUrl })
                .ToListAsync();
            stories.Should().HaveCount(2).And.OnlyContain(s => s.AuthorId == null);
            stories.Select(s => s.CoverArtRelativeUrl).Should().BeEquivalentTo([coverA, coverB]);
        }
        _store.Exists(coverA).Should().BeTrue();
        _store.Exists(coverB).Should().BeTrue();

        // ...and a later sweep, with every blob aged past the grace window, finds nothing to do.
        _store.Age(coverA, PastGrace);
        _store.Age(coverB, PastGrace);
        ImageOrphanSweepResult sweep = await SweeperOverTheStore(NewScope()).SweepAsync(dryRun: false);
        sweep.Candidates.Should().Be(0);
        sweep.DanglingReferences.Should().Be(0);
        _store.Exists(coverA).Should().BeTrue();
        _store.Exists(coverB).Should().BeTrue();
    }

    [Fact]
    public async Task DeletingAUserWithNoAvatar_TouchesNoBlob()
    {
        int userId = await SeedUserAsync("blob-no-avatar");
        IServiceProvider services = NewScope();
        ThrowingDeleteImageStorageService storage = new(_store.Create(services));

        bool deleted = await ActivatorUtilities.CreateInstance<UserDeletionService>(services, (IImageStorageService)storage)
            .DeleteUserAsync(userId);

        deleted.Should().BeTrue();
        storage.DeleteAttempts.Should().BeEmpty();
    }

    [Fact]
    public async Task DeletingAUser_WhenTheBlobDeleteFails_StillDeletes_Warns_AndTheSweepCollectsTheOrphan()
    {
        int userId = await SeedUserAsync("blob-delete-fails");
        IServiceProvider services = NewScope();
        LocalImageStorageService realStorage = _store.Create(services);
        string avatar = await SaveAsync(realStorage, ImageKind.ProfilePicture, userId);
        await SetAvatarAsync(userId, avatar);
        ThrowingDeleteImageStorageService failing = new(realStorage);
        FakeLogger<UserDeletionService> log = new();

        bool deleted = await ActivatorUtilities
            .CreateInstance<UserDeletionService>(services, (IImageStorageService)failing, log)
            .DeleteUserAsync(userId);

        deleted.Should().BeTrue("a failed blob delete never fails the account deletion");
        using (IServiceScope check = Factory.Services.CreateScope())
            (await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.AnyAsync(u => u.Id == userId))
                .Should().BeFalse();
        failing.DeleteAttempts.Should().Equal([avatar], "attempted once, after the commit");
        log.Collector.GetSnapshot().Should().ContainSingle(r => r.Level == LogLevel.Warning)
            .Which.StructuredState.Should().Contain(new KeyValuePair<string, string?>("ImagePath", avatar));
        _store.Exists(avatar).Should().BeTrue("the blob is orphaned, not lost");

        _store.Age(avatar, PastGrace);
        ImageOrphanSweepResult sweep = await SweeperOverTheStore(NewScope()).SweepAsync(dryRun: false);

        sweep.Deleted.Should().Be(1);
        _store.Exists(avatar).Should().BeFalse("the backstop collects what the inline delete missed");
    }

    // ── Moderation hard delete: the cover goes with the story; a soft removal keeps it ─────────────

    private async Task<(int StoryId, long ReportId, int ReporterId, int ModId)> SeedReportedStoryAsync(string coverPath, int storyId, int authorId)
    {
        await SetCoverAsync(storyId, coverPath);
        int reporterId = await SeedUserAsync("blob-reporter");
        int modId = await SeedUserAsync("blob-moderator");
        short reasonId;
        using (IServiceScope scope = Factory.Services.CreateScope())
            reasonId = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ReportReasons
                .OrderBy(r => r.ReportReasonId).Select(r => r.ReportReasonId).FirstAsync();

        SetActiveUser(reporterId);
        using (IServiceScope scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IReportSubmissionService>()
                .SubmitReportAsync(new SubmitReportRequest(ReportedEntityType.Story, storyId, reasonId, "illegal"));

        long reportId;
        using (IServiceScope scope = Factory.Services.CreateScope())
            reportId = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Reports
                .Where(r => r.ReportedEntityType == ReportedEntityType.Story && r.ReportedEntityId == storyId)
                .Select(r => r.ReportId).SingleAsync();

        SetActiveUser(FakeActiveUserContext.Moderator(modId));
        return (storyId, reportId, reporterId, modId);
    }

    [Fact]
    public async Task HardDelete_ErasesTheCover_AndItsUrlThen404s()
    {
        // The shared host's storage, because the shared host's static files serve /uploads/… (Local mode).
        int authorId = await SeedUserAsync("blob-author");
        int storyId = await SeedStoryAsync(authorId);
        string cover;
        using (IServiceScope scope = Factory.Services.CreateScope())
            cover = await SaveAsync(scope.ServiceProvider.GetRequiredService<IImageStorageService>(), ImageKind.Cover, storyId);
        (long reportId, _) = await SeedAndGetReportAsync(cover, storyId, authorId);
        using HttpClient client = Factory.CreateClient();
        (await client.GetAsync(cover)).StatusCode.Should().Be(HttpStatusCode.OK, "precondition: the cover is served");

        using (IServiceScope scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IModerationWriteService>()
                .ResolveWithRemovalAsync(reportId, "Illegal content.", hardDelete: true);

        (await client.GetAsync(cover)).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "the illegal image is gone when the row is gone (D14)");
    }

    [Fact]
    public async Task SoftRemoval_NeverTouchesTheCover()
    {
        int authorId = await SeedUserAsync("blob-author");
        int storyId = await SeedStoryAsync(authorId);
        IServiceProvider services = NewScope();
        ThrowingDeleteImageStorageService storage = new(_store.Create(services));
        string cover = await SaveAsync(storage, ImageKind.Cover, storyId);
        (long reportId, _) = await SeedAndGetReportAsync(cover, storyId, authorId);

        await ActivatorUtilities.CreateInstance<ServerModerationWriteService>(NewScope(), (IImageStorageService)storage)
            .ResolveWithRemovalAsync(reportId, "Takedown.", hardDelete: false);

        storage.DeleteAttempts.Should().BeEmpty("a takedown is reversible, so its blob must stay (D14)");
        _store.Exists(cover).Should().BeTrue();
        using IServiceScope check = Factory.Services.CreateScope();
        (await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().StoryListings
                .Where(l => l.StoryId == storyId).Select(l => l.CoverArtRelativeUrl).SingleAsync())
            .Should().Be(cover);
    }

    [Fact]
    public async Task HardDelete_WhenTheBlobDeleteFails_StillResolves_Notifies_AndWarns()
    {
        int authorId = await SeedUserAsync("blob-author");
        int storyId = await SeedStoryAsync(authorId);
        IServiceProvider services = NewScope();
        ThrowingDeleteImageStorageService failing = new(_store.Create(services));
        string cover = await SaveAsync(failing, ImageKind.Cover, storyId);
        (long reportId, int reporterId) = await SeedAndGetReportAsync(cover, storyId, authorId);
        FakeLogger<ServerModerationWriteService> log = new();

        await ActivatorUtilities
            .CreateInstance<ServerModerationWriteService>(NewScope(), (IImageStorageService)failing, log)
            .ResolveWithRemovalAsync(reportId, "Illegal content.", hardDelete: true);

        failing.DeleteAttempts.Should().Equal([cover], "captured before the row delete, attempted after the commit");
        log.Collector.GetSnapshot().Should().ContainSingle(r => r.Level == LogLevel.Warning)
            .Which.StructuredState.Should().Contain(new KeyValuePair<string, string?>("ImagePath", cover))
            .And.Contain(new KeyValuePair<string, string?>("ReportId", reportId.ToString()));
        _store.Exists(cover).Should().BeTrue("orphaned for the sweep, not lost");

        using IServiceScope check = Factory.Services.CreateScope();
        ApplicationDbContext db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Stories.AnyAsync(s => s.StoryId == storyId)).Should().BeFalse("the hard delete committed");
        (await db.Reports.Where(r => r.ReportId == reportId).Select(r => r.ReportStatusId).SingleAsync())
            .Should().Be(ReportStatusEnum.ResolvedActionTaken);
        (await db.Notifications.AnyAsync(n => n.RecipientUserId == reporterId
                                              && n.NotificationTypeId == NotificationTypeEnum.ReportResolved))
            .Should().BeTrue("an erasure failure must not skip the notifications behind it");
    }

    private async Task<(long ReportId, int ReporterId)> SeedAndGetReportAsync(string cover, int storyId, int authorId)
    {
        (_, long reportId, int reporterId, _) = await SeedReportedStoryAsync(cover, storyId, authorId);
        return (reportId, reporterId);
    }
}
