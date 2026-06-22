using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Regression coverage for the two WU12 create-path bugs that were previously caught only by a human
/// reading <c>/dev/wu12/*</c> probe output: the <see cref="StoryMappers.ToStory"/> NRE (covered at the
/// unit level too, but here against the real EF graph-insert behavior) and the <c>Attach</c>-vs-<c>Add</c>
/// bug, where <c>StoryListing</c>/<c>StoryDetail</c> rows were silently never inserted. Also covers slug
/// generation and disambiguation (spec §3.7).
/// </summary>
[Collection("Postgres")]
public class StoryWriteServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestAppFactory _factory = null!;
    private int _settingTagId;
    private int _genreTagId;
    private int _authorId;

    public async Task InitializeAsync()
    {
        _factory = new TestAppFactory(postgres.ConnectionString);
        // Forces the host (and Program.cs's DataSeeder) to build now — DataSeeder creates "TestUser",
        // which this class uses as a real, FK-satisfying author rather than an arbitrary int.
        _ = _factory.Services;

        (_settingTagId, _genreTagId) = await SeedRequiredTagsAsync();
        _authorId = await GetTestUserIdAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateStoryAsync_InsertsStoryListingAndStoryDetailRows()
    {
        // The Attach-vs-Add regression: Attach() marks the graph Unchanged, which made
        // SaveChangesAsync skip inserting the listing/detail rows entirely even though CreateStoryAsync
        // returned a story id with no exception. Asserting the partition rows actually exist in the DB
        // (not just that the call didn't throw) is the point of this test.
        string title = $"Create Path Story {Guid.NewGuid():N}";
        int storyId = await CreateStoryAsync(title);

        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        StoryListing? listing = await db.StoryListings.FirstOrDefaultAsync(l => l.StoryId == storyId);
        StoryDetail? detail = await db.StoryDetails.FirstOrDefaultAsync(d => d.StoryId == storyId);

        listing.Should().NotBeNull();
        listing!.StoryTitle.Should().Be(title);
        detail.Should().NotBeNull();
        detail!.Slug.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateStoryAsync_DoesNotThrow_DespiteTheFormerNullReferenceException()
    {
        Func<Task> act = async () => await CreateStoryAsync($"NRE Regression {Guid.NewGuid():N}");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CreateStoryAsync_TwiceWithSameTitle_ProducesADisambiguatedSlug()
    {
        string title = $"Duplicate Title {Guid.NewGuid():N}";

        int firstId = await CreateStoryAsync(title);
        int secondId = await CreateStoryAsync(title);

        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string? firstSlug = await db.StoryDetails.Where(d => d.StoryId == firstId).Select(d => d.Slug).FirstAsync();
        string? secondSlug = await db.StoryDetails.Where(d => d.StoryId == secondId).Select(d => d.Slug).FirstAsync();

        firstSlug.Should().NotBeNullOrEmpty();
        secondSlug.Should().NotBeNullOrEmpty();
        secondSlug.Should().NotBe(firstSlug);
        secondSlug.Should().StartWith(firstSlug!);
    }

    private async Task<int> CreateStoryAsync(string title)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IStoryWriteService writeService = scope.ServiceProvider.GetRequiredService<IStoryWriteService>();

        CreateStoryDTO dto = new()
        {
            // Story.AuthorId is a real FK to Users — must be an existing user's id, not an arbitrary
            // int. Author identity isn't itself under test here, so the DataSeeder's "TestUser" stands in.
            AuthorId = _authorId,
            Title = title,
            ShortDescription = "Integration test story",
            Rating = Rating.T,
            StoryStatusId = StoryStatusEnum.InProgress,
            LongDescription = "Integration test long description",
            PostApprovalStatus = StoryStatusEnum.InProgress,
            StoryTags =
            [
                new StoryTagDTO { TagId = _settingTagId, Priority = TagPriority.Primary, TagTypeEnum = TagTypeEnum.Setting },
                new StoryTagDTO { TagId = _genreTagId, Priority = TagPriority.Primary, TagTypeEnum = TagTypeEnum.Genre }
            ]
        };

        return await writeService.CreateStoryAsync(dto);
    }

    private async Task<(int SettingTagId, int GenreTagId)> SeedRequiredTagsAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        Tag settingTag = new() { TagName = $"Fixture Setting {suffix}", TagTypeId = TagTypeEnum.Setting };
        Tag genreTag = new() { TagName = $"Fixture Genre {suffix}", TagTypeId = TagTypeEnum.Genre };

        db.Tags.AddRange(settingTag, genreTag);
        await db.SaveChangesAsync();

        return (settingTag.TagId, genreTag.TagId);
    }

    private async Task<int> GetTestUserIdAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users.Where(u => u.UserName == "TestUser").Select(u => u.Id).FirstAsync();
    }
}
