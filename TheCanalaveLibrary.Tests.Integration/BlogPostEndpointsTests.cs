using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="BlogPostEndpoints"/> — the Layer-5 HTTP surface over
/// <see cref="IBlogPostReadService"/>, exercised through <c>Factory.CreateClient()</c>. Regression
/// coverage for the endpoint-authz sweep (2026-07-18): <c>GET /api/blog-posts/by-author/{authorId}</c>
/// rides a public route, so a forged <c>includeUnpublished=true</c> from a non-owner must degrade to
/// the published-only public view instead of leaking drafts (owner check lives in
/// <c>ServerBlogPostReadService.GetByAuthorAsync</c>); and <c>GET /api/blog-posts/{id}/edit</c>
/// enforces authorship in <c>GetForEditAsync</c> (<see cref="UnauthorizedAccessException"/> → 403
/// via <c>EndpointHelpers.ExecuteWriteAsync</c>, same wire shape as the Chapter/Story /edit routes).
/// Tier: Integration.
/// </summary>
[Collection("Postgres")]
public class BlogPostEndpointsTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;
    private int _publishedPostId;
    private int _draftPostId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId        = await SeedUserAsync("author");
        _publishedPostId = await SeedProfileBlogPostAsync(_authorId, isPublished: true);
        _draftPostId     = await SeedProfileBlogPostAsync(_authorId, isPublished: false);
    }

    /// <summary>
    /// Inserts a <see cref="ProfileBlogPost"/> row directly via <see cref="ApplicationDbContext"/>
    /// (FK parent: the author user row seeded via <c>SeedUserAsync</c>).
    /// </summary>
    private async Task<int> SeedProfileBlogPostAsync(int authorId, bool isPublished)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        ProfileBlogPost post = new()
        {
            AuthorId        = authorId,
            Title           = isPublished ? "Published post" : "Secret draft",
            Content         = isPublished ? "<p>published body</p>" : "<p>draft body</p>",
            Rating          = Rating.E,
            IsPublished     = isPublished,
            DateCreated     = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow
        };
        db.ProfileBlogPosts.Add(post);
        await db.SaveChangesAsync();
        return post.BlogPostId;
    }

    // Mirrors UserProfileEndpointsTests.ReadNullableAsync — a null Results.Json(...) writes an
    // empty 200 body that ReadFromJsonAsync chokes on.
    private static async Task<T?> ReadNullableAsync<T>(HttpResponseMessage response)
    {
        string raw = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(raw) || raw == "null"
            ? default
            : JsonSerializer.Deserialize<T>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private const string ByAuthorWithDraftsQuery = "?page=1&pageSize=10&includeUnpublished=true";

    // ── GET /api/blog-posts/by-author/{authorId} — forged includeUnpublished degrades ──

    [Fact]
    public async Task GetByAuthor_AnonymousWithIncludeUnpublishedTrue_ReturnsPublishedOnly()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response =
            await client.GetAsync($"/api/blog-posts/by-author/{_authorId}{ByAuthorWithDraftsQuery}");

        response.EnsureSuccessStatusCode();
        PagedResult<BlogPostListingDto>? result =
            await response.Content.ReadFromJsonAsync<PagedResult<BlogPostListingDto>>();
        result.Should().NotBeNull();
        result!.Items.Select(i => i.BlogPostId).Should().Contain(_publishedPostId);
        result.Items.Select(i => i.BlogPostId).Should().NotContain(_draftPostId,
            "a forged includeUnpublished=true on the public route must degrade to the published-only " +
            "view — drafts never leak to anonymous callers (endpoint-authz sweep 2026-07-18)");
    }

    [Fact]
    public async Task GetByAuthor_OtherUserWithIncludeUnpublishedTrue_ReturnsPublishedOnly()
    {
        int otherId = await SeedUserAsync("other");
        SetActiveUser(otherId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response =
            await client.GetAsync($"/api/blog-posts/by-author/{_authorId}{ByAuthorWithDraftsQuery}");

        response.EnsureSuccessStatusCode();
        PagedResult<BlogPostListingDto>? result =
            await response.Content.ReadFromJsonAsync<PagedResult<BlogPostListingDto>>();
        result.Should().NotBeNull();
        result!.Items.Select(i => i.BlogPostId).Should().Contain(_publishedPostId);
        result.Items.Select(i => i.BlogPostId).Should().NotContain(_draftPostId,
            "the unpublished view is owner-only — another authenticated user's forged flag must not unlock drafts");
    }

    [Fact]
    public async Task GetByAuthor_Author_IncludesUnpublishedDraft()
    {
        SetActiveUser(_authorId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response =
            await client.GetAsync($"/api/blog-posts/by-author/{_authorId}{ByAuthorWithDraftsQuery}");

        response.EnsureSuccessStatusCode();
        PagedResult<BlogPostListingDto>? result =
            await response.Content.ReadFromJsonAsync<PagedResult<BlogPostListingDto>>();
        result.Should().NotBeNull();
        result!.Items.Select(i => i.BlogPostId).Should().Contain([_publishedPostId, _draftPostId],
            "the verified owner sees their own drafts alongside published posts");
    }

    // ── GET /api/blog-posts/{id}/edit — author-only editor read ──────────────────

    [Fact]
    public async Task GetEdit_NonAuthor_Returns403()
    {
        int otherId = await SeedUserAsync("other");
        SetActiveUser(otherId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync($"/api/blog-posts/{_draftPostId}/edit");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "without the GetForEditAsync author gate any authenticated user could read any draft's full " +
            "content over the /edit route (endpoint-authz sweep 2026-07-18)");
    }

    [Fact]
    public async Task GetEdit_Author_Returns200WithDto()
    {
        SetActiveUser(_authorId);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync($"/api/blog-posts/{_draftPostId}/edit");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the author may load their own draft for editing");
        BlogPostEditDto? dto = await ReadNullableAsync<BlogPostEditDto>(response);
        dto.Should().NotBeNull();
        dto!.BlogPostId.Should().Be(_draftPostId);
        dto.Content.Should().Be("<p>draft body</p>");
    }

    [Fact]
    public async Task GetEdit_Anonymous_Returns401()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync($"/api/blog-posts/{_draftPostId}/edit");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the /edit route carries RequireAuthorization() — anonymous callers stop at the auth floor");
    }
}
