using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;
using Xunit;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// WU-AccessGate2 — the "StoryStatus" named filter (Class-A: Draft/PendingApproval/Rejected are
/// confidential to everyone except their own author; the filter's author clause makes drafts
/// self-visible everywhere with no per-path elevation), the chapter-level gated-existence read
/// (M alternate version of a non-M story gets a consent path), and the expanded sitemap.
/// Tier: Integration.
/// </summary>
[Collection("Postgres")]
public class StoryVisibilityTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    // ── StoryStatus filter ────────────────────────────────────────────────────────

    [Fact]
    public async Task HiddenStatusStory_InvisibleToOthers_VisibleToAuthor()
    {
        int authorId = await SeedUserAsync("author");
        int draftId = await SeedStoryAsync(authorId, status: StoryStatusEnum.Draft);
        int otherId = await SeedUserAsync("other");

        SetActiveUser(FakeActiveUserContext.Anonymous());
        (await CallStoryReadAsync(s => s.GetStoryByIdAsync(draftId)))
            .Should().BeNull("a Draft story is confidential — anonymous viewers get a true 404");
        (await CallStoryReadAsync(s => s.GetStoryGateAsync(draftId)))
            .Should().BeNull("hidden-status stories never gate — no acknowledgment, just 404");

        SetActiveUser(otherId);
        (await CallStoryReadAsync(s => s.GetStoryByIdAsync(draftId)))
            .Should().BeNull("other signed-in users don't see drafts either");

        SetActiveUser(authorId);
        (await CallStoryReadAsync(s => s.GetStoryByIdAsync(draftId)))
            .Should().NotBeNull("the filter's author clause keeps an author's own drafts visible everywhere");
    }

    [Fact]
    public async Task PendingStory_AbsentFromListings_PresentInModQueue()
    {
        int authorId = await SeedUserAsync("author");
        int pendingId = await SeedStoryAsync(authorId, status: StoryStatusEnum.PendingApproval);
        int modId = await SeedUserAsync("mod");

        SetActiveUser(FakeActiveUserContext.Anonymous());
        (StoryListingDto[] items, _) = await CallStoryReadAsync(s =>
            s.GetListingsAsync(new StoryFilterDto { PageSize = 50 }));
        items.Should().NotContain(i => i.StoryId == pendingId,
            "PendingApproval stories are hidden from search/browse");

        SetActiveUser(FakeActiveUserContext.Moderator(modId));
        using IServiceScope scope = Factory.Services.CreateScope();
        StorySubmissionQueueItemDto[] queue = await scope.ServiceProvider
            .GetRequiredService<IModerationReadService>().GetPendingSubmissionsAsync();
        queue.Should().Contain(q => q.StoryId == pendingId,
            "the pending-submissions work surface bypasses the StoryStatus filter by name");
    }

    [Fact]
    public async Task AuthoredDisclosure_ExcludesHiddenStatusMatureStory()
    {
        int authorId = await SeedUserAsync("author");
        int publishedM = await SeedStoryAsync(authorId, Rating.M);
        int pendingM = await SeedStoryAsync(authorId, Rating.M, StoryStatusEnum.PendingApproval);
        SetActiveUser(FakeActiveUserContext.Anonymous());

        IReadOnlyList<GatedMetadataDto> cards =
            await CallStoryReadAsync(s => s.GetGatedStoriesByAuthorAsync(authorId));

        cards.Should().ContainSingle("only the PUBLISHED M story is disclosed — hidden-status " +
                                     "work is confidential, not merely gated")
            .Which.RevealTargetId.Should().Be(publishedM);
        cards.Should().NotContain(c => c.RevealTargetId == pendingM);
    }

    [Fact]
    public async Task AuthorsOwnDraft_HydratesInPersonalScopeListings()
    {
        int authorId = await SeedUserAsync("author");
        int draftId = await SeedStoryAsync(authorId, status: StoryStatusEnum.Draft);
        SetActiveUser(authorId);

        (StoryListingDto[] items, _) = await CallStoryReadAsync(s =>
            s.GetListingsAsync(new StoryFilterDto { PageSize = 10 }, [draftId], personalScope: true));

        items.Should().ContainSingle("MyStories hydration shows the author their own draft " +
                                     "(author clause on the StoryStatus filter)")
            .Which.StoryId.Should().Be(draftId);
    }

    // ── Chapter-level gate (M alternate version of a non-M story) ────────────────

    [Fact]
    public async Task MatureAltVersionOfTeenStory_GetsConsentPath_AndRevealUnlocksIt()
    {
        int authorId = await SeedUserAsync("author");
        int storyId = await SeedStoryAsync(authorId, Rating.T);
        await SeedChapterWithMatureAltVersionAsync(storyId, authorId);
        int readerId = await SeedUserAsync("reader", showMature: false);
        SetActiveUser(readerId);

        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterReadService chapters = scope.ServiceProvider.GetRequiredService<IChapterReadService>();

        (await chapters.GetChapterForReadingAsync(storyId, 1, versionOrder: 2))
            .Should().BeNull("the M version exceeds the viewer's ceiling");

        GatedMetadataDto? gate = await chapters.GetChapterGateAsync(storyId, 1, versionOrder: 2);
        gate.Should().NotBeNull("the version exists and is rating-blocked → interstitial, not 404 " +
                                "(the WU-AccessGate silent-404 regression, fixed)");
        gate!.RevealTarget.Should().Be(RevealedEntityType.Story,
            "one story consent covers the whole subtree, versions included");
        gate.RevealTargetId.Should().Be(storyId);

        (await chapters.GetChapterGateAsync(storyId, 1, versionOrder: 99))
            .Should().BeNull("a nonexistent version is a true 404 — the old heuristic's false positive");

        using (IServiceScope writeScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = writeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.UserContentReveals.Add(new UserContentReveal
            {
                UserId = readerId, EntityType = RevealedEntityType.Story, EntityId = storyId,
                DateRevealed = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        (await chapters.GetChapterForReadingAsync(storyId, 1, versionOrder: 2))
            .Should().NotBeNull("the story reveal unlocks its M alternate version");
    }

    // ── Expanded sitemap ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Sitemap_IncludesProfilesGroupsBlogs_ExcludesPrivateAndHidden()
    {
        int publicUserId = await SeedUserAsync("public-user");
        int privateUserId = await SeedUserAsync("private-user");
        int draftStoryId = await SeedStoryAsync(publicUserId, status: StoryStatusEnum.Draft);
        int groupId;
        int postId;
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            User privateUser = (await db.Users.FindAsync(privateUserId))!;
            privateUser.PrivacySettings.ProfileVisibility = ProfileVisibility.Private;

            Group group = new()
            {
                GroupName = "Sitemap Group", CreatorId = publicUserId,
                AudienceRating = Rating.E, MaxContentRating = Rating.M, DateCreated = DateTime.UtcNow,
            };
            db.Groups.Add(group);

            ProfileBlogPost post = new()
            {
                AuthorId = publicUserId, Title = "Sitemap Post", Content = "<p>x</p>",
                Rating = Rating.E, IsPublished = true,
                DateCreated = DateTime.UtcNow, LastUpdatedDate = DateTime.UtcNow,
            };
            db.ProfileBlogPosts.Add(post);

            await db.SaveChangesAsync();
            groupId = group.GroupId;
            postId = post.BlogPostId;
        }

        HttpClient client = Factory.CreateClient();
        string body = await client.GetStringAsync("/sitemap.xml");

        body.Should().Contain($"/user/{publicUserId}", "Public-visibility profiles are original content homes");
        body.Should().NotContain($"/user/{privateUserId}", "Private profiles stay out");
        body.Should().Contain($"/group/{groupId}");
        body.Should().Contain($"/blog/{postId}");
        body.Should().NotContain($"/story/{draftStoryId}", "hidden-status stories stay out (filter-driven)");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private async Task SeedChapterWithMatureAltVersionAsync(int storyId, int authorId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Chapter chapter = new()
        {
            StoryId = storyId, ChapterNumber = 1, Title = "Chapter 1", IsPublished = true,
            VersionCount = 2,
        };
        ChapterContent primary = new()
        {
            AuthorId = authorId, ChapterText = "<p>primary</p>", WordCount = 1,
            SortOrder = 1, Rating = null, PublishDate = DateTime.UtcNow,
        };
        ChapterContent matureAlt = new()
        {
            AuthorId = authorId, ChapterText = "<p>mature alternate</p>", WordCount = 2,
            SortOrder = 2, Rating = Rating.M, VersionName = "Mature Version",
            PublishDate = DateTime.UtcNow,
        };
        chapter.ChapterContents.Add(primary);
        chapter.ChapterContents.Add(matureAlt);
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();

        // PrimaryContentId is only known post-save (Chapter ⇄ ChapterContent circular FK) —
        // same fix-up the DataSeeder does.
        chapter.PrimaryContentId = primary.ChapterContentId;
        await db.SaveChangesAsync();
    }

    private async Task<T> CallStoryReadAsync<T>(Func<IStoryReadService, Task<T>> call)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await call(scope.ServiceProvider.GetRequiredService<IStoryReadService>());
    }
}
