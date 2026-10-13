using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Service audit §2.12: <c>CreateChapterAsync</c>'s two saves (insert the chapter and its first version,
/// then point <c>PrimaryContentId</c> at the version) used to commit separately, so a failure between
/// them left a chapter with a NULL primary pointer for good. Since WU-ParityAndRemaining (2026-10-13)
/// both sit in one execution-strategy transaction. The failure is injected on the second save's
/// <c>UPDATE</c> with <see cref="InterleavingCommandInterceptor"/>'s <c>failWith</c> (testing.md
/// "interleave, don't race"). FK parents: the author via <see cref="IntegrationTestBase.SeedUserAsync"/>,
/// the story via <see cref="IntegrationTestBase.SeedStoryAsync"/>. Tier: Integration.
/// </summary>
[Collection("Postgres")]
public class ChapterCreateAtomicityTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task AFailureOnThePrimaryPointerUpdate_RollsBackTheWholeCreate()
    {
        int authorId = await SeedUserAsync();
        int storyId = await SeedStoryAsync(authorId);
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(authorId, showMatureContent: true));

        InterleavingCommandInterceptor interceptor = new(ConnectionString, "UPDATE chapters SET primary_content_id",
            failWith: new InvalidOperationException("injected: the second save fails"),
            interceptReaders: true);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerChapterWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerChapterWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            await FluentActions.Awaiting(() => svc.CreateChapterAsync(new CreateChapterDto
                {
                    StoryId = storyId, Title = "Doomed", ChapterText = "<p>Words.</p>", Rating = Rating.E
                }))
                .Should().ThrowAsync<Exception>();
            writeDb.ChangeTracker.Entries().Should().BeEmpty(
                "the rolled-back rows are dropped, so the circuit's next save cannot act on them");
        }

        interceptor.Fired.Should().BeTrue("the marker must match the pointer update, or this test proves nothing");
        using IServiceScope check = Factory.Services.CreateScope();
        ApplicationDbContext db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Chapters.CountAsync(c => c.StoryId == storyId))
            .Should().Be(0, "a chapter with a NULL PrimaryContentId must never be committed");
        (await db.ChapterContents.CountAsync(cc => cc.Chapter.StoryId == storyId)).Should().Be(0);
    }
}
