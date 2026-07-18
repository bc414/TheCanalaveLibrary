using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// HTTP-surface tests for <see cref="ChapterEndpoints"/> — exercised through
/// <c>Factory.CreateClient()</c> so the endpoint's exception→status translation runs for real, not
/// just the service. Regression coverage for the MA-301 edit-read gate: the browser pass found that
/// <c>GET /api/chapters/edit/{id}</c> returned 500 (unhandled <see cref="UnauthorizedAccessException"/>)
/// instead of 403 for a non-author, because the read route was the one endpoint not wrapped in
/// <c>EndpointHelpers.ExecuteWriteAsync</c> — a wire-level gap the service-level
/// <c>ChapterWriteServiceTests</c> could not see (they call the service directly and assert the raw
/// exception). Tier: Integration.
/// </summary>
[Collection("Postgres")]
public class ChapterEndpointsTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;
    private int _otherUserId;
    private int _storyId;
    private long _contentId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId    = await SeedUserAsync("author");
        _otherUserId = await SeedUserAsync("other");
        _storyId     = await SeedStoryAsync(_authorId, rating: Rating.E);

        // Create a chapter as the author (write path gates on story authorship, MA-301).
        SetActiveUser(_authorId);
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IChapterWriteService write = scope.ServiceProvider.GetRequiredService<IChapterWriteService>();
        int chapterId = await write.CreateChapterAsync(new CreateChapterDto
        {
            StoryId = _storyId, Title = "Ch 1", ChapterText = "<p>author-only draft</p>", Rating = Rating.E
        });

        using IServiceScope dbScope = Factory.Services.CreateScope();
        ApplicationDbContext db = dbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _contentId = (await db.Chapters.SingleAsync(c => c.ChapterId == chapterId)).PrimaryContentId!.Value;
    }

    [Fact]
    public async Task GetEdit_NonAuthor_Returns403_NotUnhandled500()
    {
        SetActiveUser(_otherUserId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync($"/api/chapters/edit/{_contentId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a non-author must get a translated 403 over HTTP, not an unhandled 500 — so " +
            "ClientChapterReadService's 403→UnauthorizedAccessException mapping fires under WASM (MA-301)");
    }

    [Fact]
    public async Task GetEdit_Author_Returns200()
    {
        SetActiveUser(_authorId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync($"/api/chapters/edit/{_contentId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the author may load their own chapter for editing");
        (await response.Content.ReadAsStringAsync()).Should().Contain("author-only draft");
    }
}
