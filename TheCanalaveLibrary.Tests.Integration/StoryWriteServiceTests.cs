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
///
/// WU24 additions: AuthorId stamping (CreateStoryAsync ignores client-supplied value; service stamps
/// from IActiveUserContext) and the UpdateStoryAsync ownership gate.
/// Tier: Integration (Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class StoryWriteServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _settingTagId;
    private int _genreTagId;
    private int _authorId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId = await SeedUserAsync();
        (_settingTagId, _genreTagId) = await SeedRequiredTagsAsync();
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

        using IServiceScope scope = Factory.Services.CreateScope();
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

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string? firstSlug = await db.StoryDetails.Where(d => d.StoryId == firstId).Select(d => d.Slug).FirstAsync();
        string? secondSlug = await db.StoryDetails.Where(d => d.StoryId == secondId).Select(d => d.Slug).FirstAsync();

        firstSlug.Should().NotBeNullOrEmpty();
        secondSlug.Should().NotBeNullOrEmpty();
        secondSlug.Should().NotBe(firstSlug);
        secondSlug.Should().StartWith(firstSlug!);
    }

    // --- WU24: AuthorId stamping + update ownership gate ---

    [Fact]
    public async Task CreateStoryAsync_StampsAuthorIdFromActiveUserContext()
    {
        string title = $"Stamping Test {Guid.NewGuid():N}";
        int storyId = await CreateStoryAsync(title);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        int? storedAuthorId = await db.Stories.Where(s => s.StoryId == storyId).Select(s => s.AuthorId).FirstAsync();

        storedAuthorId.Should().Be(_authorId);
    }

    [Fact]
    public async Task UpdateStoryAsync_Owner_CanUpdateTitle()
    {
        int storyId = await CreateStoryAsync($"Owner Update Fixture {Guid.NewGuid():N}");
        SetActiveUser(_authorId);

        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryWriteService writeService = scope.ServiceProvider.GetRequiredService<IStoryWriteService>();

        string newTitle = $"Updated Title {Guid.NewGuid():N}";
        await writeService.Invoking(s => s.UpdateStoryAsync(ValidUpdateDto(storyId, newTitle)))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateStoryAsync_NonOwner_ThrowsUnauthorizedAccessException()
    {
        int storyId = await CreateStoryAsync($"Non-Owner Gate Fixture {Guid.NewGuid():N}");

        int otherUserId = await SeedUserAsync();
        SetActiveUser(otherUserId);

        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryWriteService writeService = scope.ServiceProvider.GetRequiredService<IStoryWriteService>();

        await writeService.Invoking(s => s.UpdateStoryAsync(ValidUpdateDto(storyId, "Spoofed Title")))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // --- MA-201: stored XSS regression — LongDescription was previously saved unsanitized ---

    [Fact]
    public async Task CreateStoryAsync_SanitizesScriptTag_InLongDescription()
    {
        Factory.Services.GetRequiredService<FakeActiveUserContext>().UserId = _authorId;

        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryWriteService writeService = scope.ServiceProvider.GetRequiredService<IStoryWriteService>();

        int storyId = await writeService.CreateStoryAsync(new CreateStoryDTO
        {
            Title               = $"XSS Fixture {Guid.NewGuid():N}",
            ShortDescription    = "Integration test story",
            Rating              = Rating.T,
            StoryStatusId       = StoryStatusEnum.InProgress,
            LongDescription     = "<p>Safe text</p><script>alert('xss')</script>",
            PostApprovalStatus  = StoryStatusEnum.InProgress,
            StoryTags =
            [
                new StoryTagDTO { TagId = _settingTagId, Priority = TagPriority.Primary, TagTypeEnum = TagTypeEnum.Setting },
                new StoryTagDTO { TagId = _genreTagId, Priority = TagPriority.Primary, TagTypeEnum = TagTypeEnum.Genre }
            ]
        });

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        StoryDetail detail = await db.StoryDetails.SingleAsync(d => d.StoryId == storyId);

        detail.LongDescription.Should().NotContain("<script>");
        detail.LongDescription.Should().Contain("Safe text");
    }

    [Fact]
    public async Task UpdateStoryAsync_SanitizesScriptTag_InLongDescription()
    {
        int storyId = await CreateStoryAsync($"XSS Update Fixture {Guid.NewGuid():N}");
        SetActiveUser(_authorId);

        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryWriteService writeService = scope.ServiceProvider.GetRequiredService<IStoryWriteService>();

        StoryUpdateDTO dto = ValidUpdateDto(storyId, "Updated Title");
        dto.LongDescription = "<p>Safe update</p><script>alert('xss')</script>";
        await writeService.UpdateStoryAsync(dto);

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        StoryDetail detail = await db.StoryDetails.SingleAsync(d => d.StoryId == storyId);

        detail.LongDescription.Should().NotContain("<script>");
        detail.LongDescription.Should().Contain("Safe update");
    }

    private StoryUpdateDTO ValidUpdateDto(int storyId, string title) => new()
    {
        StoryId = storyId,
        Title = title,
        ShortDescription = "Integration test story",
        Rating = Rating.T,
        StoryStatusId = StoryStatusEnum.InProgress,
        LongDescription = "Integration test long description",
        PostApprovalStatus = StoryStatusEnum.InProgress,
        // CanSave() requires at least one Setting and one Genre tag.
        StoryTags =
        [
            new StoryTagDTO { TagId = _settingTagId, Priority = TagPriority.Primary, TagTypeEnum = TagTypeEnum.Setting },
            new StoryTagDTO { TagId = _genreTagId, Priority = TagPriority.Primary, TagTypeEnum = TagTypeEnum.Genre }
        ]
    };

    private async Task<int> CreateStoryAsync(string title)
    {
        // AuthorId is server-stamped from IActiveUserContext — not passed via DTO.
        // Story.AuthorId is a real FK to Users, so we need an existing user's id.
        Factory.Services.GetRequiredService<FakeActiveUserContext>().UserId = _authorId;

        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryWriteService writeService = scope.ServiceProvider.GetRequiredService<IStoryWriteService>();

        CreateStoryDTO dto = new()
        {
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
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        Tag settingTag = new() { TagName = $"Fixture Setting {suffix}", TagTypeId = TagTypeEnum.Setting };
        Tag genreTag = new() { TagName = $"Fixture Genre {suffix}", TagTypeId = TagTypeEnum.Genre };

        db.Tags.AddRange(settingTag, genreTag);
        await db.SaveChangesAsync();

        return (settingTag.TagId, genreTag.TagId);
    }

}
