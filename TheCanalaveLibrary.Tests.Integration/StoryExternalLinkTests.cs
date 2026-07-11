using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for the "Also posted on" external links (Feature 53 reframe, WU38d):
/// the seeded <c>ExternalPlatform</c> lookup, write-service sync semantics (new links start
/// Unverified; unchanged links keep their status; editing a verified link's URL resets it),
/// URL validation, and the story-page projection. Tier: Integration (real Testcontainers
/// Postgres — seed data + sync SQL must be real).
/// </summary>
[Collection("Postgres")]
public class StoryExternalLinkTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;
    private int _settingTagId;
    private int _genreTagId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId = await SeedUserAsync();
        SetActiveUser(_authorId);

        // CanSave requires a Setting + Genre tag (FK parents per testing.md).
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        Tag settingTag = new() { TagName = $"Link Setting {suffix}", TagTypeId = TagTypeEnum.Setting };
        Tag genreTag = new() { TagName = $"Link Genre {suffix}", TagTypeId = TagTypeEnum.Genre };
        db.Tags.AddRange(settingTag, genreTag);
        await db.SaveChangesAsync();
        _settingTagId = settingTag.TagId;
        _genreTagId = genreTag.TagId;
    }

    private CreateStoryDTO NewStoryDto(params StoryExternalLinkEditDto[] links) => new()
    {
        Title = $"Linked Story {Guid.NewGuid():N}",
        ShortDescription = "test",
        LongDescription = "<p>long enough</p>",
        Rating = Rating.E,
        StoryStatusId = StoryStatusEnum.Draft,
        StoryTags =
        [
            new StoryTagDTO { TagId = _settingTagId, TagTypeEnum = TagTypeEnum.Setting, Priority = TagPriority.Primary },
            new StoryTagDTO { TagId = _genreTagId, TagTypeEnum = TagTypeEnum.Genre, Priority = TagPriority.Primary }
        ],
        ExternalLinks = links.ToList()
    };

    private async Task<int> CreateStoryWithLinksAsync(params StoryExternalLinkEditDto[] links)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IStoryWriteService write = scope.ServiceProvider.GetRequiredService<IStoryWriteService>();
        return await write.CreateStoryAsync(NewStoryDto(links));
    }

    private async Task<StoryUpdateDTO> GetForEditAsync(int storyId)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IStoryReadService read = scope.ServiceProvider.GetRequiredService<IStoryReadService>();
        return (await read.GetStoryForEditAsync(storyId))!;
    }

    private async Task UpdateAsync(StoryUpdateDTO dto)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IStoryWriteService write = scope.ServiceProvider.GetRequiredService<IStoryWriteService>();
        await write.UpdateStoryAsync(dto);
    }

    // ── Seeded lookup ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetExternalPlatformsAsync_ReturnsSeededLookup_WithOtherLast()
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IStoryReadService read = scope.ServiceProvider.GetRequiredService<IStoryReadService>();

        IReadOnlyList<ExternalPlatformDto> platforms = await read.GetExternalPlatformsAsync();

        platforms.Should().HaveCount(7);
        platforms.Select(p => p.Name).Should().Contain(
            ["Archive of Our Own", "FanFiction.Net", "Wattpad", "Other"]);
        platforms.Single(p => p.Name == "Other").DomainPattern.Should().BeNull();
        platforms.Single(p => p.Name == "Archive of Our Own").DomainPattern
            .Should().Be("archiveofourown.org");
    }

    // ── Create + projection ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateStory_WithLinks_RowsStartUnverified_AndProjectIntoStoryDetails()
    {
        int storyId = await CreateStoryWithLinksAsync(
            new StoryExternalLinkEditDto { ExternalPlatformId = 1, Url = "https://archiveofourown.org/works/123" },
            new StoryExternalLinkEditDto { ExternalPlatformId = 2, Url = "https://www.fanfiction.net/s/456" });

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IStoryReadService read = scope.ServiceProvider.GetRequiredService<IStoryReadService>();
        StoryDetailsDTO? details = await read.GetStoryByIdAsync(storyId);

        details!.ExternalLinks.Should().HaveCount(2);
        details.ExternalLinks.Should().OnlyContain(l => !l.IsVerified,
            "new links always start Unverified — the checkmark is moderator-granted (WU39)");
        details.ExternalLinks.Select(l => l.PlatformName)
            .Should().Contain(["Archive of Our Own", "FanFiction.Net"]);
    }

    [Fact]
    public async Task CreateStory_BlankAndDuplicateLinkRows_AreDropped()
    {
        int storyId = await CreateStoryWithLinksAsync(
            new StoryExternalLinkEditDto { ExternalPlatformId = 1, Url = "https://archiveofourown.org/works/123" },
            new StoryExternalLinkEditDto { ExternalPlatformId = 1, Url = "https://archiveofourown.org/works/123" },
            new StoryExternalLinkEditDto { ExternalPlatformId = 7, Url = "   " });

        StoryUpdateDTO edit = await GetForEditAsync(storyId);
        edit.ExternalLinks.Should().HaveCount(1, "duplicates and blank rows never reach the table");
    }

    [Fact]
    public async Task CreateStory_InvalidUrl_ThrowsValidation()
    {
        Func<Task> act = () => CreateStoryWithLinksAsync(
            new StoryExternalLinkEditDto { ExternalPlatformId = 7, Url = "not a url" });

        await act.Should().ThrowAsync<StoryValidationException>();
    }

    // ── Update sync semantics ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStory_UnchangedLinkKeepsVerification_ChangedUrlResetsIt()
    {
        int storyId = await CreateStoryWithLinksAsync(
            new StoryExternalLinkEditDto { ExternalPlatformId = 1, Url = "https://archiveofourown.org/works/123" },
            new StoryExternalLinkEditDto { ExternalPlatformId = 2, Url = "https://www.fanfiction.net/s/456" });

        // Moderator verifies both (direct DB — the WU39 workflow isn't built yet).
        using (IServiceScope dbScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = dbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (StoryExternalLink link in db.StoryExternalLinks.Where(l => l.StoryId == storyId))
            {
                link.VerificationStatus = VerificationStatusEnum.Verified;
            }
            await db.SaveChangesAsync();
        }

        // Author edits: AO3 link unchanged, FFN link's URL changed.
        StoryUpdateDTO edit = await GetForEditAsync(storyId);
        edit.ExternalLinks.Single(l => l.ExternalPlatformId == 2).Url = "https://www.fanfiction.net/s/789";
        await UpdateAsync(edit);

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        List<StoryExternalLink> rows = verifyDb.StoryExternalLinks.Where(l => l.StoryId == storyId).ToList();

        rows.Should().HaveCount(2);
        rows.Single(l => l.ExternalPlatformId == 1).VerificationStatus
            .Should().Be(VerificationStatusEnum.Verified, "an untouched link keeps its checkmark");
        rows.Single(l => l.ExternalPlatformId == 2).VerificationStatus
            .Should().Be(VerificationStatusEnum.Unverified,
                "changing a verified link's URL resets verification — it's a different claim");
        rows.Single(l => l.ExternalPlatformId == 2).Url.Should().Be("https://www.fanfiction.net/s/789");
    }

    [Fact]
    public async Task UpdateStory_RemovedLinkRow_IsDeleted()
    {
        int storyId = await CreateStoryWithLinksAsync(
            new StoryExternalLinkEditDto { ExternalPlatformId = 1, Url = "https://archiveofourown.org/works/123" },
            new StoryExternalLinkEditDto { ExternalPlatformId = 2, Url = "https://www.fanfiction.net/s/456" });

        StoryUpdateDTO edit = await GetForEditAsync(storyId);
        edit.ExternalLinks.RemoveAll(l => l.ExternalPlatformId == 2);
        await UpdateAsync(edit);

        StoryUpdateDTO after = await GetForEditAsync(storyId);
        after.ExternalLinks.Should().ContainSingle()
            .Which.ExternalPlatformId.Should().Be((short)1);
    }

    // ── Original dates round-trip ────────────────────────────────────────────────

    [Fact]
    public async Task OriginalDates_RoundTripThroughEditAndDisplay()
    {
        int storyId = await CreateStoryWithLinksAsync();

        StoryUpdateDTO edit = await GetForEditAsync(storyId);
        edit.OriginalPublishedDate = new DateOnly(2009, 5, 17);
        edit.OriginalLastUpdatedDate = new DateOnly(2014, 11, 2);
        await UpdateAsync(edit);

        StoryUpdateDTO after = await GetForEditAsync(storyId);
        after.OriginalPublishedDate.Should().Be(new DateOnly(2009, 5, 17));
        after.OriginalLastUpdatedDate.Should().Be(new DateOnly(2014, 11, 2));

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IStoryReadService read = scope.ServiceProvider.GetRequiredService<IStoryReadService>();
        StoryDetailsDTO? details = await read.GetStoryByIdAsync(storyId);
        details!.OriginalPublishDate.Should().Be(new DateOnly(2009, 5, 17));
    }
}
