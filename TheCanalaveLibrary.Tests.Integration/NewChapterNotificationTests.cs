using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for the new-chapter fan-out (<c>NewChapterOnFollowedStory</c> = 10;
/// WU-InertFeatures — service audit §2.3.1, worksheet D2 anchor + D1 anti-bump rider + D16/D17).
/// <list type="bullet">
///   <item>The first publish of a chapter notifies every story follower once:
///   <c>RelatedEntityId = chapterId</c>, source = the author; the author's own follow is dropped; a
///   favoriter who doesn't follow gets nothing (no private-follow flag — D17).</item>
///   <item>First publication only, permanently: unpublish → republish (even after the first row was
///   read), adding an alternate version, promoting it, and editing content never notify.</item>
///   <item>The unpublished-story default (roadmap row 17): a Draft or taken-down story's chapter
///   publish notifies nobody.</item>
///   <item>Enrichment: the chapter deep link plus the story title as context.</item>
/// </list>
/// Seeding: users and the story via the base helpers; the follower relationships as direct
/// <c>UserStoryInteraction</c> rows (FK parents: the users + story); chapters through the real
/// <see cref="IChapterWriteService"/> as the author. Tier: <b>Integration</b> (Testcontainers Postgres).
/// </summary>
[Collection("Postgres")]
public class NewChapterNotificationTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;
    private int _followerA;
    private int _followerB;
    private int _favoriterOnly;
    private int _storyId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId = await SeedUserAsync("ChapterAuthor");
        _followerA = await SeedUserAsync("FollowerA");
        _followerB = await SeedUserAsync("FollowerB");
        _favoriterOnly = await SeedUserAsync("FavoriterOnly");
        _storyId = await SeedStoryAsync(_authorId);

        await SeedInteractionAsync(_followerA, _storyId, followed: true);
        await SeedInteractionAsync(_followerB, _storyId, followed: true, favorite: true);
        await SeedInteractionAsync(_favoriterOnly, _storyId, favorite: true, hiddenFavorite: true);
        await SeedInteractionAsync(_authorId, _storyId, followed: true); // drop-self pin

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorId, showMatureContent: true));
    }

    [Fact]
    public async Task FirstPublish_NotifiesEachFollowerOnce_WithTheChapterAndTheAuthor()
    {
        int chapterId = await CreateChapterAsync(_storyId);

        await SetPublishedAsync(chapterId, true);

        List<Notification> rows = await LoadType10Async();
        rows.Select(n => n.RecipientUserId).Should().BeEquivalentTo([_followerA, _followerB]);
        rows.Should().OnlyContain(n => n.RelatedEntityId == chapterId && n.SourceUserId == _authorId,
            "D16-conformant anchor: the chapter joins up to its story");
    }

    [Fact]
    public async Task FirstPublish_AuthorFollowingTheirOwnStory_GetsNothing_AndAFavoriterWhoDoesntFollowGetsNothing()
    {
        int chapterId = await CreateChapterAsync(_storyId);

        await SetPublishedAsync(chapterId, true);

        List<Notification> rows = await LoadType10Async();
        rows.Should().NotContain(n => n.RecipientUserId == _authorId, "drop-self: the author is the source");
        rows.Should().NotContain(n => n.RecipientUserId == _favoriterOnly,
            "recipients are IsFollowed only — no private-follow flag exists, so a hidden favorite doesn't enter (D17)");
    }

    [Fact]
    public async Task UnpublishThenRepublish_AfterTheFirstWasRead_NeverNotifiesAgain()
    {
        int chapterId = await CreateChapterAsync(_storyId);
        await SetPublishedAsync(chapterId, true);
        await MarkAllReadAsync(); // so unread-dedup cannot be what suppresses a second row

        await SetPublishedAsync(chapterId, false);
        await SetPublishedAsync(chapterId, true);

        (await LoadType10Async()).Should().HaveCount(2,
            "D1/D2: first publication only, permanently, per artifact — republication is not a publication event");
    }

    [Fact]
    public async Task AlternateVersion_PrimarySwitch_AndContentEdit_NeverNotify()
    {
        int chapterId = await CreateChapterAsync(_storyId);
        await SetPublishedAsync(chapterId, true);
        await MarkAllReadAsync();

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            IChapterWriteService chapters = scope.ServiceProvider.GetRequiredService<IChapterWriteService>();
            long altId = await chapters.AddAlternateVersionAsync(chapterId, new CreateChapterDto
            {
                StoryId = _storyId, Title = "Alt", ChapterText = "<p>Another take.</p>", Rating = Rating.E,
            });
            await chapters.SetPrimaryVersionAsync(chapterId, altId);
            await chapters.UpdateChapterContentAsync(new UpdateChapterContentDto
            {
                ChapterContentId = altId, Title = "Alt, revised", ChapterText = "<p>Revised.</p>",
            });
        }

        (await LoadType10Async()).Should().HaveCount(2, "adding or promoting a version and editing are updates, never publish events");
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("TakenDown")]
    public async Task FirstPublish_WhileTheStoryIsNotPubliclyPublished_NotifiesNobody(string storyState)
    {
        int storyId = await SeedStoryAsync(_authorId,
            status: storyState == "Draft" ? StoryStatusEnum.Draft : StoryStatusEnum.InProgress);
        await SeedInteractionAsync(_followerA, storyId, followed: true);
        if (storyState == "TakenDown")
        {
            using IServiceScope scope = Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Stories
                .Where(s => s.StoryId == storyId)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.IsTakenDown, true));
        }
        int chapterId = await CreateChapterAsync(storyId);

        await SetPublishedAsync(chapterId, true);

        (await LoadType10Async()).Should().BeEmpty(
            "the default (roadmap row 17): no fan-out while the story isn't publicly published — and the " +
            "anchor is now stamped, so it never will");
        using IServiceScope verify = Factory.Services.CreateScope();
        (await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().Chapters
            .Where(c => c.ChapterId == chapterId).Select(c => c.FirstPublishedDate).SingleAsync())
            .Should().NotBeNull("the chapter's own first publish still stamps its anchor (D2)");
    }

    [Fact]
    public async Task Enrichment_DeepLinksTheChapter_AndNamesTheStory()
    {
        int chapterId = await CreateChapterAsync(_storyId, title: "The Storm");
        await SetPublishedAsync(chapterId, true);

        string storyTitle;
        using (IServiceScope scope = Factory.Services.CreateScope())
            storyTitle = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().StoryListings
                .Where(s => s.StoryId == _storyId).Select(s => s.StoryTitle).SingleAsync();

        SetActiveUser(_followerA);
        NotificationDto dto;
        using (IServiceScope scope = Factory.Services.CreateScope())
            dto = (await scope.ServiceProvider.GetRequiredService<INotificationReadService>().GetNotificationsAsync(1, 10))
                .Single(n => n.NotificationTypeId == NotificationTypeEnum.NewChapterOnFollowedStory);

        dto.TargetTitle.Should().Be("The Storm");
        dto.TargetUrl.Should().Be($"/story/{_storyId}/1");
        dto.TargetContextTitle.Should().Be(storyTitle);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private async Task<int> CreateChapterAsync(int storyId, string title = "Chapter")
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IChapterWriteService>().CreateChapterAsync(
            new CreateChapterDto { StoryId = storyId, Title = title, ChapterText = "<p>Words.</p>", Rating = Rating.E });
    }

    private async Task SetPublishedAsync(int chapterId, bool isPublished)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IChapterWriteService>().SetPublishedAsync(chapterId, isPublished);
    }

    private async Task<List<Notification>> LoadType10Async()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Notifications
            .Where(n => n.NotificationTypeId == NotificationTypeEnum.NewChapterOnFollowedStory)
            .ToListAsync();
    }

    private async Task MarkAllReadAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Notifications
            .ExecuteUpdateAsync(u => u.SetProperty(n => n.IsRead, true));
    }

    private async Task SeedInteractionAsync(int userId, int storyId,
        bool followed = false, bool favorite = false, bool hiddenFavorite = false)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserStoryInteractions.Add(new UserStoryInteraction
        {
            UserId = userId, StoryId = storyId, IsFollowed = followed,
            IsFavorite = favorite, IsHiddenFavorite = hiddenFavorite,
        });
        await db.SaveChangesAsync();
    }
}
