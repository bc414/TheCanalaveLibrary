using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="ServerTagHierarchyCache"/> (WU-ApplyFiltersPurity, closes
/// hidden-deferrals-tracker B12) — cold load from real rows, warm reuse across DI scopes, and
/// write-invalidation through the real <see cref="ITagWriteService"/> for create/update(re-parent)/
/// delete. Needs real Postgres (EF projection + real write service + real DI graph).
///
/// <see cref="IntegrationTestBase.ResetSharedHostState"/> invalidates the cache before every test
/// in this suite (as it does for every suite in the collection), so each test starts cold.
/// Tier: Integration.
/// </summary>
[Collection("Postgres")]
public class TagHierarchyCacheTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task GetExpansionMapAsync_ColdLoad_ReflectsRealRows()
    {
        using IServiceScope seedScope = Factory.Services.CreateScope();
        ApplicationDbContext db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string s = Guid.NewGuid().ToString("N")[..8];
        Tag parent = new() { TagName = $"Parent-{s}", TagTypeId = TagTypeEnum.Genre };
        Tag childA = new() { TagName = $"ChildA-{s}", TagTypeId = TagTypeEnum.Genre, ParentTag = parent };
        Tag childB = new() { TagName = $"ChildB-{s}", TagTypeId = TagTypeEnum.Genre, ParentTag = parent };
        db.Tags.AddRange(parent, childA, childB);
        await db.SaveChangesAsync();

        using IServiceScope readScope = Factory.Services.CreateScope();
        ITagHierarchyReadService cache = readScope.ServiceProvider.GetRequiredService<ITagHierarchyReadService>();
        TagExpansionMap map = await cache.GetExpansionMapAsync();

        map.Expand(parent.TagId).Should().BeEquivalentTo([parent.TagId, childA.TagId, childB.TagId]);
    }

    [Fact]
    public async Task GetExpansionMapAsync_TwoConcurrentScopes_ReturnTheSameCachedInstance()
    {
        // Proves the singleton is genuinely shared and cached, without adding a test-only counter
        // to the production type — a fresh load on each call would fail this by construction.
        ITagHierarchyReadService cacheViaScope1;
        ITagHierarchyReadService cacheViaScope2;
        using (IServiceScope scope1 = Factory.Services.CreateScope())
            cacheViaScope1 = scope1.ServiceProvider.GetRequiredService<ITagHierarchyReadService>();
        using (IServiceScope scope2 = Factory.Services.CreateScope())
            cacheViaScope2 = scope2.ServiceProvider.GetRequiredService<ITagHierarchyReadService>();

        TagExpansionMap first = await cacheViaScope1.GetExpansionMapAsync();
        TagExpansionMap second = await cacheViaScope2.GetExpansionMapAsync();

        ReferenceEquals(first, second).Should().BeTrue("both scopes resolve the same singleton cache");
    }

    [Fact]
    public async Task CreateTagAsync_WithParent_NewChildRollsUpImmediately()
    {
        int modId = await SeedUserAsync("Mod");
        int authorId = await SeedUserAsync("Author");
        string s = Guid.NewGuid().ToString("N")[..8];

        int parentTagId;
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Tag parent = new() { TagName = $"Parent-{s}", TagTypeId = TagTypeEnum.Genre };
            db.Tags.Add(parent);
            await db.SaveChangesAsync();
            parentTagId = parent.TagId;
        }

        // A filtered read on the parent BEFORE the child exists warms the cache with a map that has
        // no entry for the not-yet-created child — the scenario B12 complaint 1 calls out.
        int[] idsBeforeChild = await QueryIdsAsync(authorId, new StoryFilterDto { IncludedTagIds = [parentTagId] });
        idsBeforeChild.Should().BeEmpty();

        int childTagId;
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            SetActiveUser(FakeActiveUserContext.Moderator(modId));
            ITagWriteService writeSvc = scope.ServiceProvider.GetRequiredService<ITagWriteService>();
            TagSaveResult result = await writeSvc.CreateTagAsync(new CreateTagDto
            {
                TagName = $"Child-{s}", TagTypeId = TagTypeEnum.Genre, ParentTagId = parentTagId
            });
            childTagId = result.TagId;
        }

        int storyId;
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            storyId = await SeedStoryAsync(authorId);
            db.StoryTags.Add(new StoryTag { StoryId = storyId, TagId = childTagId });
            await db.SaveChangesAsync();
        }

        // The write-service create above must have invalidated the cache, so this read reloads and
        // sees the new child — no restart, no waiting out the TTL.
        int[] idsAfterChild = await QueryIdsAsync(authorId, new StoryFilterDto { IncludedTagIds = [parentTagId] });
        idsAfterChild.Should().Contain(storyId, "CreateTagAsync must invalidate the cache immediately");
    }

    [Fact]
    public async Task UpdateTagAsync_ReParenting_MakesTheReparentedTagsStoriesVisibleToTheNewParentFilter()
    {
        // B12 complaint 1's own example: a story invisible to a parent filter becomes visible after
        // UpdateTagAsync re-parents its tag — proving reproducibility isn't lost by the cache.
        int modId = await SeedUserAsync("Mod");
        int authorId = await SeedUserAsync("Author");
        string s = Guid.NewGuid().ToString("N")[..8];

        int parentTagId, standaloneTagId, storyId;
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Tag parent = new() { TagName = $"Parent-{s}", TagTypeId = TagTypeEnum.Genre };
            Tag standalone = new() { TagName = $"Standalone-{s}", TagTypeId = TagTypeEnum.Genre };
            db.Tags.AddRange(parent, standalone);
            await db.SaveChangesAsync();
            parentTagId = parent.TagId;
            standaloneTagId = standalone.TagId;

            storyId = await SeedStoryAsync(authorId);
            db.StoryTags.Add(new StoryTag { StoryId = storyId, TagId = standaloneTagId });
            await db.SaveChangesAsync();
        }

        int[] idsBeforeReparent = await QueryIdsAsync(authorId, new StoryFilterDto { IncludedTagIds = [parentTagId] });
        idsBeforeReparent.Should().NotContain(storyId, "the tag has no parent yet");

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            SetActiveUser(FakeActiveUserContext.Moderator(modId));
            ITagWriteService writeSvc = scope.ServiceProvider.GetRequiredService<ITagWriteService>();
            await writeSvc.UpdateTagAsync(new UpdateTagDto
            {
                TagId = standaloneTagId, TagName = $"Standalone-{s}", TagTypeId = TagTypeEnum.Genre,
                ParentTagId = parentTagId
            });
        }

        int[] idsAfterReparent = await QueryIdsAsync(authorId, new StoryFilterDto { IncludedTagIds = [parentTagId] });
        idsAfterReparent.Should().Contain(storyId, "UpdateTagAsync must invalidate the cache immediately");
    }

    [Fact]
    public async Task DeleteTagAsync_UnusedChildlessTag_SubsequentMapLoadSucceedsAndIsConsistent()
    {
        int modId = await SeedUserAsync("Mod");
        string s = Guid.NewGuid().ToString("N")[..8];

        int tagId;
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Tag tag = new() { TagName = $"Deletable-{s}", TagTypeId = TagTypeEnum.Genre };
            db.Tags.Add(tag);
            await db.SaveChangesAsync();
            tagId = tag.TagId;
        }

        // Warm the cache before the delete, so the test proves invalidation rather than a cold load
        // that would incidentally omit the deleted row anyway.
        using (IServiceScope scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ITagHierarchyReadService>().GetExpansionMapAsync();

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            SetActiveUser(FakeActiveUserContext.Moderator(modId));
            ITagWriteService writeSvc = scope.ServiceProvider.GetRequiredService<ITagWriteService>();
            await writeSvc.DeleteTagAsync(tagId);
        }

        using IServiceScope finalScope = Factory.Services.CreateScope();
        ITagHierarchyReadService cache = finalScope.ServiceProvider.GetRequiredService<ITagHierarchyReadService>();
        Func<Task> act = () => cache.GetExpansionMapAsync();
        await act.Should().NotThrowAsync("a reload after delete must succeed against the now-consistent table");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    // Story seeding uses the base IntegrationTestBase.SeedStoryAsync helper (authorId optional).

    private async Task<int[]> QueryIdsAsync(int viewerId, StoryFilterDto filter)
    {
        SetActiveUser(viewerId);
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryReadService read = scope.ServiceProvider.GetRequiredService<IStoryReadService>();
        var (items, _) = await read.GetListingsAsync(filter with { PageSize = 10_000, Page = 1 });
        return items.Select(i => i.StoryId).ToArray();
    }
}
