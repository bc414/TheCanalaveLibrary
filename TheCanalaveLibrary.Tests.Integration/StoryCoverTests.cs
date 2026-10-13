using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Owner ruling D14's sub-ruling (WU-BlobCleanup): cover upload persists its own reference. Write blob →
/// persist column → best-effort delete the old blob, all inside <c>UploadCoverArtAsync</c>; clearing is
/// its own call (<c>RemoveCoverArtAsync</c>); and <c>UpdateStoryAsync</c> can no longer touch the cover.
/// The inverse-hazard test matters most: before D14 an update that said nothing about the cover nulled
/// the column and deleted the live blob. The write service is built over a <see cref="TempBlobStore"/>, so
/// file assertions see only this test's files. Tier: Integration.
/// </summary>
[Collection("Postgres")]
public sealed class StoryCoverTests(PostgresFixture postgres) : IntegrationTestBase(postgres), IDisposable
{
    private readonly TempBlobStore _store = new();
    private readonly List<IServiceScope> _scopes = [];
    private int _authorId;
    private int _storyId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId = await SeedUserAsync("cover-author");
        SetActiveUser(_authorId);

        // A story UpdateStoryAsync will accept: CanSave needs a Setting and a Genre tag (FK parents per
        // testing.md), so it is created through the service rather than SeedStoryAsync.
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        Tag setting = new() { TagName = $"Cover Setting {suffix}", TagTypeId = TagTypeEnum.Setting };
        Tag genre = new() { TagName = $"Cover Genre {suffix}", TagTypeId = TagTypeEnum.Genre };
        db.Tags.AddRange(setting, genre);
        await db.SaveChangesAsync();

        _storyId = await scope.ServiceProvider.GetRequiredService<IStoryWriteService>().CreateStoryAsync(new CreateStoryDTO
        {
            Title = $"Cover Story {suffix}",
            ShortDescription = "test",
            LongDescription = "<p>long enough</p>",
            Rating = Rating.E,
            StoryTags =
            [
                new StoryTagDTO { TagId = setting.TagId, TagTypeEnum = TagTypeEnum.Setting, Priority = TagPriority.Primary },
                new StoryTagDTO { TagId = genre.TagId, TagTypeEnum = TagTypeEnum.Genre, Priority = TagPriority.Primary },
            ],
        });
    }

    public void Dispose()
    {
        foreach (IServiceScope scope in _scopes) scope.Dispose();
        _store.Dispose();
    }

    /// <summary>A write service over the test's own store (one scope, like one circuit).</summary>
    private ServerStoryWriteService Writes()
    {
        IServiceScope scope = Factory.Services.CreateScope();
        _scopes.Add(scope);
        return ActivatorUtilities.CreateInstance<ServerStoryWriteService>(
            scope.ServiceProvider, (IImageStorageService)_store.Create(scope.ServiceProvider));
    }

    private static async Task<string> UploadAsync(ServerStoryWriteService writes, int storyId)
    {
        using MemoryStream png = new(TestImages.Png(4, 4));
        return await writes.UploadCoverArtAsync(png, "image/png", storyId);
    }

    private async Task<(string? Cover, DateTime LastUpdated)> ReadStoredAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.Stories.Where(s => s.StoryId == storyId)
            .Select(s => new { s.StoryListing.CoverArtRelativeUrl, s.LastUpdatedDate }).SingleAsync();
        return (row.CoverArtRelativeUrl, row.LastUpdatedDate);
    }

    private string StoryFolder(int storyId) => Path.Combine(_store.WebRoot, "uploads", "stories", storyId.ToString());

    // ── UploadCoverArtAsync persists ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_Alone_PersistsTheCover_WithNoFollowUpUpdate()
    {
        (_, DateTime before) = await ReadStoredAsync(_storyId);

        string path = await UploadAsync(Writes(), _storyId);

        (string? cover, DateTime lastUpdated) = await ReadStoredAsync(_storyId);
        cover.Should().Be(path, "the upload commits its own reference (D14)");
        lastUpdated.Should().BeAfter(before, "a cover change stamps LastUpdatedDate like any property edit");
        _store.Exists(path).Should().BeTrue();

        using IServiceScope scope = Factory.Services.CreateScope();
        StoryEditDTO? edit = await scope.ServiceProvider.GetRequiredService<IStoryReadService>()
            .GetStoryForEditAsync(_storyId);
        edit!.CoverArtRelativeUrl.Should().Be(path, "the editor's read echo shows the persisted cover");
    }

    [Fact]
    public async Task ASecondUpload_PersistsTheNewPath_AndDeletesTheOldFile()
    {
        string first = await UploadAsync(Writes(), _storyId);

        string second = await UploadAsync(Writes(), _storyId);

        (await ReadStoredAsync(_storyId)).Cover.Should().Be(second);
        _store.Exists(first).Should().BeFalse("the replaced blob is deleted after the new one is persisted");
        _store.Exists(second).Should().BeTrue();
    }

    [Fact]
    public async Task TwoUploadsThroughOneScope_PersistTheSecond_AndDeleteTheFirst()
    {
        // One scope models one circuit: the tracked story from the first upload is reused by the second.
        ServerStoryWriteService writes = Writes();
        string first = await UploadAsync(writes, _storyId);

        string second = await UploadAsync(writes, _storyId);

        (await ReadStoredAsync(_storyId)).Cover.Should().Be(second);
        _store.Exists(first).Should().BeFalse();
    }

    // ── The inverse hazard: UpdateStoryAsync cannot touch the cover ──────────────────────────────

    [Fact]
    public async Task UploadThenUpdate_FromTheEditDto_LeavesTheCoverAndItsFileIntact()
    {
        string path = await UploadAsync(Writes(), _storyId);
        StoryEditDTO edit;
        using (IServiceScope scope = Factory.Services.CreateScope())
            edit = (await scope.ServiceProvider.GetRequiredService<IStoryReadService>().GetStoryForEditAsync(_storyId))!;
        edit.Title = "Edited title";
        edit.CoverArtRelativeUrl = null; // a stale or hostile echo: the write path must never read it

        await Writes().UpdateStoryAsync(edit);

        (await ReadStoredAsync(_storyId)).Cover.Should().Be(path,
            "an update says nothing about the cover, so the cover stays — the D14 inverse-hazard regression");
        _store.Exists(path).Should().BeTrue("and its blob is never deleted by a property edit");
    }

    [Fact]
    public async Task UploadThenUpdate_ThroughOneScope_LeavesTheCoverIntact()
    {
        // The editor's real sequence on one circuit: pick a cover (immediate upload), then Save.
        ServerStoryWriteService writes = Writes();
        string path = await UploadAsync(writes, _storyId);
        StoryEditDTO edit;
        using (IServiceScope scope = Factory.Services.CreateScope())
            edit = (await scope.ServiceProvider.GetRequiredService<IStoryReadService>().GetStoryForEditAsync(_storyId))!;
        edit.ShortDescription = "Edited description";

        await writes.UpdateStoryAsync(new StoryUpdateDTO
        {
            StoryId = edit.StoryId, Title = edit.Title, ShortDescription = edit.ShortDescription,
            Rating = edit.Rating, LongDescription = edit.LongDescription,
            PostApprovalStatus = edit.PostApprovalStatus, StoryTags = edit.StoryTags,
            StoryCharacters = edit.StoryCharacters, StoryCharacterPairings = edit.StoryCharacterPairings,
            ExternalLinks = edit.ExternalLinks,
        });

        (await ReadStoredAsync(_storyId)).Cover.Should().Be(path);
        _store.Exists(path).Should().BeTrue();
    }

    // ── The gate ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ANonAuthor_IsRefused_AndNoFileIsWritten()
    {
        int otherId = await SeedUserAsync("cover-other");
        SetActiveUser(otherId);

        Func<Task> act = () => UploadAsync(Writes(), _storyId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        Directory.Exists(StoryFolder(_storyId)).Should().BeFalse("the gate runs before the blob write");
        (await ReadStoredAsync(_storyId)).Cover.Should().BeNull();
    }

    [Fact]
    public async Task AMissingStory_IsKeyNotFound()
    {
        Func<Task> act = () => UploadAsync(Writes(), int.MaxValue);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AnAuthorlessStory_IsUnauthorized_NotKeyNotFound()
    {
        // Service audit §2.12's banned (int?) item: the old projection read a NULL author (the author
        // deleted their account) as "no such story".
        int authorless = await SeedStoryAsync(authorId: null);

        Func<Task> act = () => UploadAsync(Writes(), authorless);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        Directory.Exists(StoryFolder(authorless)).Should().BeFalse();
    }

    // ── RemoveCoverArtAsync ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Remove_NullsTheColumn_AndDeletesTheFile()
    {
        string path = await UploadAsync(Writes(), _storyId);
        (_, DateTime afterUpload) = await ReadStoredAsync(_storyId);

        await Writes().RemoveCoverArtAsync(_storyId);

        (string? cover, DateTime lastUpdated) = await ReadStoredAsync(_storyId);
        cover.Should().BeNull();
        lastUpdated.Should().BeAfter(afterUpload);
        _store.Exists(path).Should().BeFalse();
    }

    [Fact]
    public async Task Remove_ByANonAuthor_IsRefused_AndTheCoverStays()
    {
        string path = await UploadAsync(Writes(), _storyId);
        int otherId = await SeedUserAsync("cover-other");
        SetActiveUser(otherId);

        Func<Task> act = () => Writes().RemoveCoverArtAsync(_storyId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await ReadStoredAsync(_storyId)).Cover.Should().Be(path);
        _store.Exists(path).Should().BeTrue();
    }

    [Fact]
    public async Task Remove_WithNoCover_IsANoOp()
    {
        (_, DateTime before) = await ReadStoredAsync(_storyId);

        await Writes().RemoveCoverArtAsync(_storyId);

        (string? cover, DateTime after) = await ReadStoredAsync(_storyId);
        cover.Should().BeNull();
        after.Should().Be(before, "nothing to clear, so nothing is written");
    }

    [Fact]
    public async Task Remove_OfAMissingStory_IsKeyNotFound()
    {
        Func<Task> act = () => Writes().RemoveCoverArtAsync(int.MaxValue);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
