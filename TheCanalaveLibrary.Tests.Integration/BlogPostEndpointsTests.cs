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
/// via <c>EndpointHelpers.ExecuteAsync</c>, same wire shape as the Chapter/Story /edit routes).
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

    // ── Group-post lifecycle routes (owner ruling D10, WU-TptHardDelete) ─────────

    /// <summary>Inline group + group post (FK parents: the author user; the group row).</summary>
    private async Task<int> SeedGroupPostAsync(int authorId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Group group = new()
        {
            GroupName = $"Group {Guid.NewGuid():N}"[..20], CreatorId = authorId,
            AudienceRating = Rating.E, MaxContentRating = Rating.T
        };
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        GroupBlogPost post = new()
        {
            AuthorId = authorId, GroupId = group.GroupId, Title = "Group post", Content = "<p>hi</p>",
            Rating = Rating.E, IsPublished = true, DateCreated = DateTime.UtcNow, LastUpdatedDate = DateTime.UtcNow
        };
        db.GroupBlogPosts.Add(post);
        await db.SaveChangesAsync();
        return post.BlogPostId;
    }

    [Fact]
    public async Task PutGroupPost_RouteBodyMismatch_Returns400()
    {
        int postId = await SeedGroupPostAsync(_authorId);
        SetActiveUser(_authorId);

        HttpResponseMessage response = await Factory.CreateClient().PutAsJsonAsync(
            $"/api/blog-posts/group/{postId}",
            new UpdateGroupBlogPostDto { BlogPostId = postId + 1, Title = "t", Content = "<p>c</p>" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutGroupPost_NonAuthor_Returns403_Author_Returns204()
    {
        int postId = await SeedGroupPostAsync(_authorId);
        int otherId = await SeedUserAsync("other");
        UpdateGroupBlogPostDto dto = new() { BlogPostId = postId, Title = "Edited", Content = "<p>c</p>", Rating = Rating.E };

        SetActiveUser(otherId);
        (await Factory.CreateClient().PutAsJsonAsync($"/api/blog-posts/group/{postId}", dto))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        SetActiveUser(_authorId);
        (await Factory.CreateClient().PutAsJsonAsync($"/api/blog-posts/group/{postId}", dto))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteGroupPost_NonAuthor_Returns403_Author_Returns204()
    {
        int postId = await SeedGroupPostAsync(_authorId);
        int otherId = await SeedUserAsync("other");

        SetActiveUser(otherId);
        (await Factory.CreateClient().DeleteAsync($"/api/blog-posts/group/{postId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        SetActiveUser(_authorId);
        (await Factory.CreateClient().DeleteAsync($"/api/blog-posts/group/{postId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetById_CarriesEachSubtypesKind_OverTheWire()
    {
        // BlogPostPage offers Edit for Kind == Profile only; on WASM the kind arrives through this JSON.
        int groupPostId = await SeedGroupPostAsync(_authorId);
        int sitePostId = await SeedSitePostAsync(_authorId);
        SetActiveUser(_authorId);
        HttpClient client = Factory.CreateClient();

        BlogPostDto? profile = await ReadNullableAsync<BlogPostDto>(await client.GetAsync($"/api/blog-posts/{_publishedPostId}"));
        BlogPostDto? group   = await ReadNullableAsync<BlogPostDto>(await client.GetAsync($"/api/blog-posts/{groupPostId}"));
        BlogPostDto? site    = await ReadNullableAsync<BlogPostDto>(await client.GetAsync($"/api/blog-posts/{sitePostId}"));

        profile!.Kind.Should().Be(BlogPostKind.Profile);
        group!.Kind.Should().Be(BlogPostKind.Group);
        site!.Kind.Should().Be(BlogPostKind.Site, "a site announcement's author is a moderator, who must get no Edit link (U9)");
    }

    /// <summary>Inline published site announcement (FK parent: the author user).</summary>
    private async Task<int> SeedSitePostAsync(int authorId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        SiteBlogPost post = new()
        {
            AuthorId = authorId, Title = "Site news", Content = "<p>news</p>", Rating = Rating.E,
            IsPublished = true, DateCreated = DateTime.UtcNow, LastUpdatedDate = DateTime.UtcNow
        };
        db.SiteBlogPosts.Add(post);
        await db.SaveChangesAsync();
        return post.BlogPostId;
    }

    [Fact]
    public async Task DeleteProfileRoute_WithAGroupPostId_Returns404_NotA500()
    {
        // Regression: the profile route's stub delete affected 0 rows on a group id and surfaced as a
        // 500 (DbUpdateConcurrencyException). Each subtype now answers only for its own ids.
        int postId = await SeedGroupPostAsync(_authorId);
        SetActiveUser(_authorId);

        (await Factory.CreateClient().DeleteAsync($"/api/blog-posts/{postId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
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
