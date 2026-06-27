using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="ServerTagWriteService"/> (WU27.5). Covers mod-gate,
/// create/update happy paths, duplicate-name validation, parent assignment + hierarchy guard,
/// delete (unused succeeds, in-use blocked). Uses Testcontainers Postgres via
/// <see cref="IntegrationTestBase"/>. Tier: Integration.
/// </summary>
[Collection("Postgres")]
public class TagWriteServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    // ── Gate: non-mod is rejected ─────────────────────────────────────────────

    [Fact]
    public async Task CreateTagAsync_NonMod_ThrowsUnauthorized()
    {
        int userId = await SeedUserAsync("NonMod");
        SetActiveUser(userId); // IsModerator = false, IsAdmin = false

        using IServiceScope scope = Factory.Services.CreateScope();
        ITagWriteService svc = scope.ServiceProvider.GetRequiredService<ITagWriteService>();

        Func<Task> act = () => svc.CreateTagAsync(new CreateTagDto
        {
            TagName = "ShouldBeBlocked",
            TagTypeId = TagTypeEnum.Genre
        });

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task UpdateTagAsync_NonMod_ThrowsUnauthorized()
    {
        int userId = await SeedUserAsync("NonMod");
        SetActiveUser(userId);

        using IServiceScope scope = Factory.Services.CreateScope();
        ITagWriteService svc = scope.ServiceProvider.GetRequiredService<ITagWriteService>();

        Func<Task> act = () => svc.UpdateTagAsync(new UpdateTagDto
        {
            TagId = 999,
            TagName = "X",
            TagTypeId = TagTypeEnum.Genre
        });

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task DeleteTagAsync_NonMod_ThrowsUnauthorized()
    {
        int userId = await SeedUserAsync("NonMod");
        SetActiveUser(userId);

        using IServiceScope scope = Factory.Services.CreateScope();
        ITagWriteService svc = scope.ServiceProvider.GetRequiredService<ITagWriteService>();

        await svc.Invoking(s => s.DeleteTagAsync(999))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── Create happy path ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTagAsync_ValidDto_ReturnsNewTagId()
    {
        int modId = await SeedUserAsync("Mod");
        SetActiveUser(FakeActiveUserContext.Moderator(modId));

        using IServiceScope scope = Factory.Services.CreateScope();
        ITagWriteService svc = scope.ServiceProvider.GetRequiredService<ITagWriteService>();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        TagSaveResult result = await svc.CreateTagAsync(new CreateTagDto
        {
            TagName = $"Gengar-{suffix}",
            TagTypeId = TagTypeEnum.Character,
            IsFanon = false
        });
        int tagId = result.TagId;

        tagId.Should().BeGreaterThan(0);
        Tag? saved = await db.Tags.FindAsync(tagId);
        saved.Should().NotBeNull();
        saved!.TagName.Should().Be($"Gengar-{suffix}");
        saved.TagTypeId.Should().Be(TagTypeEnum.Character);
    }

    // ── Duplicate name within type is rejected ────────────────────────────────

    [Fact]
    public async Task CreateTagAsync_DuplicateNameInType_ThrowsValidation()
    {
        int modId = await SeedUserAsync("Mod");
        SetActiveUser(FakeActiveUserContext.Moderator(modId));

        using IServiceScope scope = Factory.Services.CreateScope();
        ITagWriteService svc = scope.ServiceProvider.GetRequiredService<ITagWriteService>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        await svc.CreateTagAsync(new CreateTagDto
        {
            TagName = $"Eevee-{suffix}", TagTypeId = TagTypeEnum.Character
        });

        // Second create with the same name in the same type must fail.
        Func<Task> act = () => svc.CreateTagAsync(new CreateTagDto
        {
            TagName = $"Eevee-{suffix}", TagTypeId = TagTypeEnum.Character
        });
        await act.Should().ThrowAsync<TagValidationException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateTagAsync_SameNameDifferentType_Succeeds()
    {
        // Composite uniqueness: "Paris" as Character AND Setting is allowed.
        int modId = await SeedUserAsync("Mod");
        SetActiveUser(FakeActiveUserContext.Moderator(modId));

        using IServiceScope scope = Factory.Services.CreateScope();
        ITagWriteService svc = scope.ServiceProvider.GetRequiredService<ITagWriteService>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        await svc.CreateTagAsync(new CreateTagDto
        {
            TagName = $"Paris-{suffix}", TagTypeId = TagTypeEnum.Character
        });

        // Same name, different type — must succeed.
        Func<Task> act = () => svc.CreateTagAsync(new CreateTagDto
        {
            TagName = $"Paris-{suffix}", TagTypeId = TagTypeEnum.Setting
        });
        await act.Should().NotThrowAsync();
    }

    // ── Parent assignment ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTagAsync_WithValidParent_PersistsParentTagId()
    {
        int modId = await SeedUserAsync("Mod");
        SetActiveUser(FakeActiveUserContext.Moderator(modId));

        using IServiceScope scope = Factory.Services.CreateScope();
        ITagWriteService svc = scope.ServiceProvider.GetRequiredService<ITagWriteService>();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        int parentId = (await svc.CreateTagAsync(new CreateTagDto
        {
            TagName = $"Parent-{suffix}", TagTypeId = TagTypeEnum.Genre
        })).TagId;

        int childId = (await svc.CreateTagAsync(new CreateTagDto
        {
            TagName = $"Child-{suffix}", TagTypeId = TagTypeEnum.Genre, ParentTagId = parentId
        })).TagId;

        Tag? child = await db.Tags.FindAsync(childId);
        child!.ParentTagId.Should().Be(parentId);
    }

    [Fact]
    public async Task CreateTagAsync_TwoLevelParent_ThrowsValidation()
    {
        // A tag whose chosen parent already has a parent violates one-level-deep rule.
        int modId = await SeedUserAsync("Mod");
        SetActiveUser(FakeActiveUserContext.Moderator(modId));

        using IServiceScope scope = Factory.Services.CreateScope();
        ITagWriteService svc = scope.ServiceProvider.GetRequiredService<ITagWriteService>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        int grandparentId = (await svc.CreateTagAsync(new CreateTagDto
        {
            TagName = $"Grandparent-{suffix}", TagTypeId = TagTypeEnum.Genre
        })).TagId;
        int parentId = (await svc.CreateTagAsync(new CreateTagDto
        {
            TagName = $"Parent-{suffix}", TagTypeId = TagTypeEnum.Genre, ParentTagId = grandparentId
        })).TagId;

        Func<Task> act = () => svc.CreateTagAsync(new CreateTagDto
        {
            TagName = $"Child-{suffix}", TagTypeId = TagTypeEnum.Genre, ParentTagId = parentId
        });
        await act.Should().ThrowAsync<TagValidationException>().WithMessage("*one level deep*");
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTagAsync_ValidDto_PersistsChanges()
    {
        int modId = await SeedUserAsync("Mod");
        SetActiveUser(FakeActiveUserContext.Moderator(modId));

        using IServiceScope scope = Factory.Services.CreateScope();
        ITagWriteService svc = scope.ServiceProvider.GetRequiredService<ITagWriteService>();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        int tagId = (await svc.CreateTagAsync(new CreateTagDto
        {
            TagName = $"Original-{suffix}", TagTypeId = TagTypeEnum.Genre
        })).TagId;

        await svc.UpdateTagAsync(new UpdateTagDto
        {
            TagId = tagId,
            TagName = $"Renamed-{suffix}",
            TagTypeId = TagTypeEnum.Genre,
            IsFanon = true
        });

        Tag? updated = await db.Tags.FindAsync(tagId);
        updated!.TagName.Should().Be($"Renamed-{suffix}");
        updated.IsFanon.Should().BeTrue();
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteTagAsync_UnusedTag_Succeeds()
    {
        int modId = await SeedUserAsync("Mod");
        SetActiveUser(FakeActiveUserContext.Moderator(modId));

        using IServiceScope scope = Factory.Services.CreateScope();
        ITagWriteService svc = scope.ServiceProvider.GetRequiredService<ITagWriteService>();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        int tagId = (await svc.CreateTagAsync(new CreateTagDto
        {
            TagName = $"Unused-{suffix}", TagTypeId = TagTypeEnum.Genre
        })).TagId;

        await svc.DeleteTagAsync(tagId);

        (await db.Tags.FindAsync(tagId)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteTagAsync_TagInUseByStory_ThrowsValidation()
    {
        int modId = await SeedUserAsync("Mod");
        SetActiveUser(FakeActiveUserContext.Moderator(modId));

        string suffix = Guid.NewGuid().ToString("N")[..8];

        // Seed the tag and a story with a StoryTag referencing it.
        using IServiceScope setupScope = Factory.Services.CreateScope();
        ApplicationDbContext db = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Tag tag = new() { TagName = $"InUse-{suffix}", TagTypeId = TagTypeEnum.Genre };
        db.Tags.Add(tag);
        await db.SaveChangesAsync();

        int storyId = await SeedStoryAsync();
        using IServiceScope storyScope = Factory.Services.CreateScope();
        ApplicationDbContext storyDb = storyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        storyDb.StoryTags.Add(new StoryTag
        {
            StoryId = storyId,
            TagId = tag.TagId,
            Priority = TagPriority.Primary
        });
        await storyDb.SaveChangesAsync();

        // Now try to delete.
        using IServiceScope deleteScope = Factory.Services.CreateScope();
        ITagWriteService svc = deleteScope.ServiceProvider.GetRequiredService<ITagWriteService>();

        Func<Task> act = () => svc.DeleteTagAsync(tag.TagId);
        await act.Should().ThrowAsync<TagValidationException>().WithMessage("*referenced*");
    }

    [Fact]
    public async Task DeleteTagAsync_TagWithChildren_ThrowsValidation()
    {
        int modId = await SeedUserAsync("Mod");
        SetActiveUser(FakeActiveUserContext.Moderator(modId));

        using IServiceScope scope = Factory.Services.CreateScope();
        ITagWriteService svc = scope.ServiceProvider.GetRequiredService<ITagWriteService>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        int parentId = (await svc.CreateTagAsync(new CreateTagDto
        {
            TagName = $"ParentToDelete-{suffix}", TagTypeId = TagTypeEnum.Genre
        })).TagId;
        await svc.CreateTagAsync(new CreateTagDto
        {
            TagName = $"ChildTag-{suffix}", TagTypeId = TagTypeEnum.Genre, ParentTagId = parentId
        });

        Func<Task> act = () => svc.DeleteTagAsync(parentId);
        await act.Should().ThrowAsync<TagValidationException>().WithMessage("*child tag*");
    }

    [Fact]
    public async Task DeleteTagAsync_TagNotFound_ThrowsKeyNotFound()
    {
        int modId = await SeedUserAsync("Mod");
        SetActiveUser(FakeActiveUserContext.Moderator(modId));

        using IServiceScope scope = Factory.Services.CreateScope();
        ITagWriteService svc = scope.ServiceProvider.GetRequiredService<ITagWriteService>();

        await svc.Invoking(s => s.DeleteTagAsync(int.MaxValue))
            .Should().ThrowAsync<KeyNotFoundException>();
    }
}
