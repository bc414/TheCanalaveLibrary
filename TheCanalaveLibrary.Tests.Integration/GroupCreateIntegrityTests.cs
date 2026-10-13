using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Service audit §2.12's <c>CreateGroupAsync</c> defects (WU-ParityAndRemaining, 2026-10-13;
/// layer2-services.md §"Group Rating Waterfall" → "Group names are unique, byte-wise"):
/// <list type="bullet">
/// <item>A taken name is a <see cref="GroupValidationException"/> (400), never the unique index's raw 500
/// — on create, on a rename onto another group's name, and when a concurrent create lands between the
/// pre-check and the insert (interleaved with <see cref="InterleavingCommandInterceptor"/>).</item>
/// <item>The group and its creator's Admin membership commit in one save, so a failed create leaves
/// neither row.</item>
/// </list>
/// FK parents: users via <see cref="IntegrationTestBase.SeedUserAsync"/>; groups through the service.
/// Tier: Integration.
/// </summary>
[Collection("Postgres")]
public class GroupCreateIntegrityTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _userId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _userId = await SeedUserAsync("creator");
        SetActiveUser(_userId);
    }

    [Fact]
    public async Task CreatingADuplicateName_IsAValidationRefusal_AndLeavesOneGroup()
    {
        await CreateAsync("Kanto Fans");

        Func<Task> act = () => CreateAsync("  Kanto Fans  ");

        (await act.Should().ThrowAsync<GroupValidationException>())
            .Which.Errors.Should().Equal(["A group named “Kanto Fans” already exists."]);
        (await GroupCountAsync()).Should().Be(1);
        (await MemberCountAsync()).Should().Be(1, "the refused create adds no membership either");
    }

    [Fact]
    public async Task ANameDifferingOnlyInCase_IsADifferentName()
    {
        // Byte-wise, like the index; case-insensitive names are D29 (pending).
        await CreateAsync("Kanto Fans");

        await FluentActions.Awaiting(() => CreateAsync("kanto fans")).Should().NotThrowAsync();
        (await GroupCountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task RenamingOntoAnotherGroupsName_IsRefused_AndChangesNothing()
    {
        await CreateAsync("Johto Fans");
        int groupId = await CreateAsync("Hoenn Fans");

        Func<Task> act = () => UpdateAsync(groupId, "Johto Fans");

        (await act.Should().ThrowAsync<GroupValidationException>())
            .Which.Errors.Should().Equal(["A group named “Johto Fans” already exists."]);
        (await NameOfAsync(groupId)).Should().Be("Hoenn Fans");
    }

    [Fact]
    public async Task KeepingItsOwnName_IsNotAClash()
    {
        int groupId = await CreateAsync("Sinnoh Fans");

        await FluentActions.Awaiting(() => UpdateAsync(groupId, "Sinnoh Fans")).Should().NotThrowAsync();
        (await NameOfAsync(groupId)).Should().Be("Sinnoh Fans");
    }

    [Fact]
    public async Task ADuplicateLandingBetweenTheCheckAndTheInsert_IsTheSameRefusal_AndCommitsNothing()
    {
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "INSERT INTO groups",
            interleavedSql:
                "INSERT INTO groups (group_name, creator_id, date_created, audience_rating, max_content_rating) " +
                $"VALUES ('Unova Fans', {_userId}, now(), 0, 2);",
            interceptReaders: true);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerGroupWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerGroupWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            (await FluentActions.Awaiting(() => svc.CreateGroupAsync(new CreateGroupDto { GroupName = "Unova Fans" }))
                    .Should().ThrowAsync<GroupValidationException>("the race is answered like the check — never a raw 500"))
                .Which.Errors.Should().Equal(["A group named “Unova Fans” already exists."]);
            writeDb.ChangeTracker.Entries().Should().BeEmpty(
                "the rejected group and membership are detached, so a circuit's next save cannot retry them");
        }

        interceptor.Fired.Should().BeTrue();
        (await GroupCountAsync()).Should().Be(1, "only the competing row");
        (await MemberCountAsync()).Should().Be(0, "the creator's membership went down with its group");
    }

    [Fact]
    public async Task TheGroupAndItsAdmin_CommitTogether()
    {
        int groupId = await CreateAsync("Kalos Fans");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.GroupMembers.SingleAsync(m => m.GroupId == groupId))
            .Should().Match<GroupMember>(m => m.UserId == _userId && m.Role == GroupRole.Admin);
    }

    [Fact]
    public async Task PostingADuplicateName_Answers400WithTheMessage_Not500()
    {
        await CreateAsync("Alola Fans");

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/groups",
            new CreateGroupDto { GroupName = "Alola Fans" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("already exists");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private async Task<int> CreateAsync(string name)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IGroupWriteService>()
            .CreateGroupAsync(new CreateGroupDto { GroupName = name, AudienceType = GroupAudienceType.Standard });
    }

    private async Task UpdateAsync(int groupId, string name)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IGroupWriteService>()
            .UpdateGroupAsync(new UpdateGroupDto
            {
                GroupId = groupId, GroupName = name, AudienceType = GroupAudienceType.Standard
            });
    }

    private async Task<int> GroupCountAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Groups.CountAsync();
    }

    private async Task<int> MemberCountAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().GroupMembers.CountAsync();
    }

    private async Task<string> NameOfAsync(int groupId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Groups
            .Where(g => g.GroupId == groupId).Select(g => g.GroupName).SingleAsync();
    }
}
