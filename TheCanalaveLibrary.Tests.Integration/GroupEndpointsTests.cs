using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="GroupEndpoints"/> (F38/F39/F40, WU-GroupsL5, 2026-07-24) — the
/// Layer-5 HTTP surface over the Groups services, exercised through <c>Factory.CreateClient()</c> so
/// routing, model binding, and the exception→status translation all run for real. Service-level
/// behavior (the content-rating waterfall, admin/member gate semantics) is already covered by
/// <see cref="GroupServiceTests"/>; these tests pin the HTTP boundary the L5 grid-mark reconciliation
/// (2026-07-24) claims — status codes, the <see cref="PagedResult{T}"/> envelope, and folder-write
/// routes specifically, since <see cref="GroupFolderManagementPage"/> is their only UI consumer and
/// this tier is what verifies them over the wire without a browser.
/// Tier: Integration.
/// </summary>
[Collection("Postgres")]
public class GroupEndpointsTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private async Task<int> SeedGroupWithAdminAsync(int adminUserId)
    {
        SetActiveUser(adminUserId);
        using IServiceScope scope = Factory.Services.CreateScope();
        IGroupWriteService svc = scope.ServiceProvider.GetRequiredService<IGroupWriteService>();
        return await svc.CreateGroupAsync(new CreateGroupDto
        {
            GroupName    = $"Test Group {Guid.NewGuid():N}"[..24],
            AudienceType = GroupAudienceType.Standard
        });
    }

    private async Task SeedGroupMemberAsync(int groupId, int userId, GroupRole role)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.GroupMembers.Add(new GroupMember
        {
            GroupId    = groupId,
            UserId     = userId,
            Role       = role,
            DateJoined = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    // ── Reads: PagedResult<T> boundary ──────────────────────────────────────────

    [Fact]
    public async Task GetListings_ReturnsPagedResultEnvelope()
    {
        int adminId = await SeedUserAsync("Admin");
        await SeedGroupWithAdminAsync(adminId);
        SetActiveUser(FakeActiveUserContext.Anonymous());

        HttpClient client = Factory.CreateClient();
        PagedResult<GroupCardDto>? page =
            await client.GetFromJsonAsync<PagedResult<GroupCardDto>>("/api/groups/?page=1&pageSize=20");

        page.Should().NotBeNull();
        page!.Items.Should().NotBeEmpty();
        page.TotalCount.Should().BeGreaterThanOrEqualTo(page.Items.Length);
    }

    [Fact]
    public async Task GetMembers_ReturnsPagedResultEnvelope()
    {
        int adminId = await SeedUserAsync("Admin");
        int groupId = await SeedGroupWithAdminAsync(adminId);
        SetActiveUser(FakeActiveUserContext.Anonymous());

        HttpClient client = Factory.CreateClient();
        PagedResult<GroupMemberDto>? page = await client.GetFromJsonAsync<PagedResult<GroupMemberDto>>(
            $"/api/groups/{groupId}/members?page=1&pageSize=20");

        page.Should().NotBeNull();
        page!.Items.Should().ContainSingle(m => m.UserId == adminId, "the creator is the sole Admin member");
    }

    // ── Write auth floor ─────────────────────────────────────────────────────────

    [Fact]
    public async Task PostFolders_Anonymous_Returns401()
    {
        int adminId = await SeedUserAsync("Admin");
        int groupId = await SeedGroupWithAdminAsync(adminId);
        SetActiveUser(FakeActiveUserContext.Anonymous());

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/groups/folders", new CreateFolderDto
        {
            GroupId = groupId,
            Name    = "Anon Folder"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "RequireAuthorization() must reject an unauthenticated caller before the handler runs");
    }

    [Fact]
    public async Task PostFolders_AuthenticatedNonAdminMember_Returns403()
    {
        int adminId  = await SeedUserAsync("Admin");
        int memberId = await SeedUserAsync("Member");
        int groupId  = await SeedGroupWithAdminAsync(adminId);
        await SeedGroupMemberAsync(groupId, memberId, GroupRole.Member);
        SetActiveUser(memberId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/groups/folders", new CreateFolderDto
        {
            GroupId = groupId,
            Name    = "Member Folder"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "folder creation is admin-only — a plain member must be rejected, mirroring MA-702's " +
            "edge-gate pattern for the service's own RequireAdminAsync throw");
    }

    // ── Folder CRUD over the wire (the page's actual write surface) ─────────────

    [Fact]
    public async Task PostFolders_Admin_Returns200AndCreatesFolder()
    {
        int adminId = await SeedUserAsync("Admin");
        int groupId = await SeedGroupWithAdminAsync(adminId);
        SetActiveUser(adminId);
        string name = $"Folder-{Guid.NewGuid():N}"[..20];

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/groups/folders", new CreateFolderDto
        {
            GroupId   = groupId,
            Name      = name,
            MaxRating = Rating.T
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        int folderId = await response.Content.ReadFromJsonAsync<int>();
        folderId.Should().BePositive();

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        GroupFolder? folder = await db.GroupFolders.FindAsync(folderId);
        folder.Should().NotBeNull();
        folder!.Name.Should().Be(name);
        folder.MaxRating.Should().Be(Rating.T);
    }

    [Fact]
    public async Task PutFolderName_Admin_Returns204AndRenames()
    {
        int adminId = await SeedUserAsync("Admin");
        int groupId = await SeedGroupWithAdminAsync(adminId);
        SetActiveUser(adminId);

        using IServiceScope scope = Factory.Services.CreateScope();
        IGroupWriteService svc = scope.ServiceProvider.GetRequiredService<IGroupWriteService>();
        int folderId = await svc.CreateFolderAsync(new CreateFolderDto { GroupId = groupId, Name = "Old" });

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response =
            await client.PutAsJsonAsync($"/api/groups/folders/{folderId}/name", "Renamed");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.GroupFolders.FindAsync(folderId))!.Name.Should().Be("Renamed");
    }

    [Fact]
    public async Task PutFolderName_UnknownFolder_Returns404()
    {
        int adminId = await SeedUserAsync("Admin");
        SetActiveUser(adminId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response =
            await client.PutAsJsonAsync("/api/groups/folders/999999/name", "Ghost");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteFolder_NonAdminMember_Returns403()
    {
        int adminId  = await SeedUserAsync("Admin");
        int memberId = await SeedUserAsync("Member");
        int groupId  = await SeedGroupWithAdminAsync(adminId);
        await SeedGroupMemberAsync(groupId, memberId, GroupRole.Member);

        using IServiceScope scope = Factory.Services.CreateScope();
        IGroupWriteService svc = scope.ServiceProvider.GetRequiredService<IGroupWriteService>();
        int folderId = await svc.CreateFolderAsync(new CreateFolderDto { GroupId = groupId, Name = "Doomed" });

        SetActiveUser(memberId);
        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.DeleteAsync($"/api/groups/folders/{folderId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.GroupFolders.FindAsync(folderId)).Should().NotBeNull("the rejected delete must not touch the row");
    }

    [Fact]
    public async Task DeleteFolder_Admin_Returns204AndRemovesRow()
    {
        int adminId = await SeedUserAsync("Admin");
        int groupId = await SeedGroupWithAdminAsync(adminId);

        using IServiceScope scope = Factory.Services.CreateScope();
        IGroupWriteService svc = scope.ServiceProvider.GetRequiredService<IGroupWriteService>();
        int folderId = await svc.CreateFolderAsync(new CreateFolderDto { GroupId = groupId, Name = "Doomed" });

        SetActiveUser(adminId);
        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.DeleteAsync($"/api/groups/folders/{folderId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.GroupFolders.FindAsync(folderId)).Should().BeNull();
    }

    [Fact]
    public async Task PutFolderSortOrder_Admin_Returns204AndUpdatesRow()
    {
        int adminId = await SeedUserAsync("Admin");
        int groupId = await SeedGroupWithAdminAsync(adminId);

        using IServiceScope scope = Factory.Services.CreateScope();
        IGroupWriteService svc = scope.ServiceProvider.GetRequiredService<IGroupWriteService>();
        int folderId = await svc.CreateFolderAsync(new CreateFolderDto { GroupId = groupId, Name = "Movable" });

        SetActiveUser(adminId);
        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response =
            await client.PutAsync($"/api/groups/folders/{folderId}/sort-order?newSortOrder=7", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.GroupFolders.FindAsync(folderId))!.SortOrder.Should().Be(7);
    }
}
