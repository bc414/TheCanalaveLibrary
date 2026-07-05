using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="TagEndpoints"/> (WU-L5Pilot) — the Layer-5 HTTP surface over
/// the tag services, exercised through <c>Factory.CreateClient()</c> so routing, model binding,
/// and the exception→status translation all run for real. Service-level behavior (validation
/// rules, mod gate semantics) is covered by <see cref="TagWriteServiceTests"/>; these tests
/// pin the boundary: status codes, ProblemDetails bodies, query binding, and payload round trips.
/// Tier: Integration.
/// </summary>
[Collection("Postgres")]
public class TagEndpointsTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private async Task<int> SeedTagAsync(string name, TagTypeEnum type, int? parentId = null)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Tag tag = new() { TagName = name, TagTypeId = type, ParentTagId = parentId };
        db.Tags.Add(tag);
        await db.SaveChangesAsync();
        return tag.TagId;
    }

    // ── Reads ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDirectory_ReturnsSeededTagInItsTypeGroup()
    {
        string name = $"DirTag-{Guid.NewGuid():N}"[..20];
        await SeedTagAsync(name, TagTypeEnum.Genre);

        HttpClient client = Factory.CreateClient();
        List<TagDirectoryGroupDto>? groups =
            await client.GetFromJsonAsync<List<TagDirectoryGroupDto>>("/api/tags/directory");

        groups.Should().NotBeNull();
        TagDirectoryGroupDto genreGroup = groups!.Single(g => g.TagType == TagTypeEnum.Genre);
        genreGroup.Nodes.Should().Contain(n => n.Tag.TagName == name);
    }

    [Fact]
    public async Task GetByType_BindsEnumFromQuery()
    {
        string name = $"TypeTag-{Guid.NewGuid():N}"[..20];
        await SeedTagAsync(name, TagTypeEnum.Setting);

        HttpClient client = Factory.CreateClient();
        List<TagDropDownDTO>? tags = await client.GetFromJsonAsync<List<TagDropDownDTO>>(
            $"/api/tags?type={(short)TagTypeEnum.Setting}");

        tags.Should().NotBeNull();
        tags!.Should().Contain(t => t.TagName == name);
    }

    [Fact]
    public async Task GetChipsByIds_BindsRepeatedKeysAndPreservesOrder()
    {
        int idA = await SeedTagAsync($"A-{Guid.NewGuid():N}"[..16], TagTypeEnum.Genre);
        int idB = await SeedTagAsync($"B-{Guid.NewGuid():N}"[..16], TagTypeEnum.Genre);

        HttpClient client = Factory.CreateClient();
        List<TagChipDto>? chips = await client.GetFromJsonAsync<List<TagChipDto>>(
            $"/api/tags/chips/by-ids?ids={idB}&ids={idA}");

        chips.Should().NotBeNull();
        chips!.Select(c => c.TagId).Should().Equal(idB, idA); // reorder-to-input contract
    }

    // ── Write status mapping ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_NonMod_Returns403()
    {
        int userId = await SeedUserAsync("NonMod");
        SetActiveUser(userId); // authenticated, not a mod

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/tags", new CreateTagDto
        {
            TagName = "Blocked",
            TagTypeId = TagTypeEnum.Genre
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Mod_Returns200WithTagSaveResult()
    {
        int modId = await SeedUserAsync("Mod");
        SetActiveUser(FakeActiveUserContext.Moderator(modId));
        string name = $"NewTag-{Guid.NewGuid():N}"[..20];

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/tags", new CreateTagDto
        {
            TagName = name,
            TagTypeId = TagTypeEnum.Genre
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        TagSaveResult? result = await response.Content.ReadFromJsonAsync<TagSaveResult>();
        result.Should().NotBeNull();
        result!.TagId.Should().BePositive();
        result.SpriteWarning.Should().BeNull(); // no sprite identifier submitted

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Tags.Single(t => t.TagId == result.TagId).TagName.Should().Be(name);
    }

    [Fact]
    public async Task Post_DuplicateName_Returns400WithDetailMessage()
    {
        int modId = await SeedUserAsync("Mod");
        SetActiveUser(FakeActiveUserContext.Moderator(modId));
        string name = $"DupTag-{Guid.NewGuid():N}"[..20];
        await SeedTagAsync(name, TagTypeEnum.Genre);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/tags", new CreateTagDto
        {
            TagName = name,
            TagTypeId = TagTypeEnum.Genre
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // The ProblemDetails.Detail carries the service's user-facing message verbatim —
        // ClientTagWriteService rethrows it as TagValidationException(detail).
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(name).And.Contain("already exists");
    }

    [Fact]
    public async Task Put_RouteBodyIdMismatch_Returns400()
    {
        int modId = await SeedUserAsync("Mod");
        SetActiveUser(FakeActiveUserContext.Moderator(modId));
        int tagId = await SeedTagAsync($"Mm-{Guid.NewGuid():N}"[..16], TagTypeEnum.Genre);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/tags/{tagId + 1}", new UpdateTagDto
        {
            TagId = tagId,
            TagName = "Renamed",
            TagTypeId = TagTypeEnum.Genre
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_UnknownTag_Returns404()
    {
        int modId = await SeedUserAsync("Mod");
        SetActiveUser(FakeActiveUserContext.Moderator(modId));

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PutAsJsonAsync("/api/tags/999999", new UpdateTagDto
        {
            TagId = 999999,
            TagName = "Ghost",
            TagTypeId = TagTypeEnum.Genre
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Mod_Returns204AndRemovesRow()
    {
        int modId = await SeedUserAsync("Mod");
        SetActiveUser(FakeActiveUserContext.Moderator(modId));
        int tagId = await SeedTagAsync($"Del-{Guid.NewGuid():N}"[..16], TagTypeEnum.Genre);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.DeleteAsync($"/api/tags/{tagId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Tags.Any(t => t.TagId == tagId).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_InUse_Returns400WithDetailMessage()
    {
        int modId = await SeedUserAsync("Mod");
        SetActiveUser(FakeActiveUserContext.Moderator(modId));
        // A child tag is the cheapest "referenced" state — no story/selection parents needed.
        int parentId = await SeedTagAsync($"Par-{Guid.NewGuid():N}"[..16], TagTypeEnum.Genre);
        await SeedTagAsync($"Chi-{Guid.NewGuid():N}"[..16], TagTypeEnum.Genre, parentId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.DeleteAsync($"/api/tags/{parentId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("child tag");
    }
}
