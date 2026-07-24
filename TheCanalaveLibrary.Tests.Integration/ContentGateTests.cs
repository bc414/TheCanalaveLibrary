using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;
using Xunit;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Feature 66 (Viewer Access Gating, WU-AccessGate) — the three-plane access model's service and
/// HTTP behavior: gated-existence reads (Visible | GatedMature-metadata | NotFound), durable
/// per-item reveals (DB rows for accounts, cookie stand-in for anonymous), Personal-plane
/// hydration, the ProfileVisibility Class-A guard, and the Feature-64 crawlability surface
/// (robots/sitemap/canonical-301). Model: content-safety.md §"The Three-Plane Access Model";
/// design docs under .claude/design/. Tier: Integration.
/// </summary>
[Collection("Postgres")]
public class ContentGateTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    // ── Gated-existence reads ─────────────────────────────────────────────────────

    [Fact]
    public async Task StoryGate_MatureStory_ReturnsMetadataForMatureOffViewer()
    {
        int authorId = await SeedUserAsync("author");
        int storyId = await SeedStoryAsync(authorId, Rating.M);
        SetActiveUser(FakeActiveUserContext.Anonymous());

        GatedMetadataDto? gate = await CallStoryReadAsync(s => s.GetStoryGateAsync(storyId));

        gate.Should().NotBeNull("an M story exists — the interstitial acknowledges it");
        gate!.RevealTarget.Should().Be(RevealedEntityType.Story);
        gate.RevealTargetId.Should().Be(storyId);
        gate.Rating.Should().Be(Rating.M);
        gate.Title.Should().NotBeNullOrEmpty("the interstitial shows title/author/rating");
    }

    [Fact]
    public async Task StoryGate_TeenStory_ReturnsNull()
    {
        int storyId = await SeedStoryAsync(rating: Rating.T);
        SetActiveUser(FakeActiveUserContext.Anonymous());

        (await CallStoryReadAsync(s => s.GetStoryGateAsync(storyId)))
            .Should().BeNull("non-M stories never gate — a null detail read means truly absent");
    }

    [Fact]
    public async Task StoryGate_TakenDownMatureStory_ReturnsNull()
    {
        int storyId = await SeedStoryAsync(rating: Rating.M);
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Story story = (await db.Stories.FindAsync(storyId))!;
            story.IsTakenDown = true;
            await db.SaveChangesAsync();
        }
        SetActiveUser(FakeActiveUserContext.Anonymous());

        (await CallStoryReadAsync(s => s.GetStoryGateAsync(storyId)))
            .Should().BeNull("takedown is Class-A enforcement, not consent — a taken-down M story stays a true 404");
    }

    // ── Reveals (Direct-navigation plane) ─────────────────────────────────────────

    [Fact]
    public async Task StoryDetail_MatureOffViewer_NullWithoutReveal_VisibleWithDbReveal()
    {
        int authorId = await SeedUserAsync("author");
        int storyId = await SeedStoryAsync(authorId, Rating.M);
        int readerId = await SeedUserAsync("reader", showMature: false);
        SetActiveUser(readerId);

        (await CallStoryReadAsync(s => s.GetStoryByIdAsync(storyId)))
            .Should().BeNull("no consent yet — the detail read stays gated");

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.UserContentReveals.Add(new UserContentReveal
            {
                UserId = readerId,
                EntityType = RevealedEntityType.Story,
                EntityId = storyId,
                DateRevealed = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        StoryDetailsDTO? story = await CallStoryReadAsync(s => s.GetStoryByIdAsync(storyId));
        story.Should().NotBeNull("a durable per-story reveal grants exactly this story");
        story!.StoryId.Should().Be(storyId);

        int otherMStoryId = await SeedStoryAsync(authorId, Rating.M);
        (await CallStoryReadAsync(s => s.GetStoryByIdAsync(otherMStoryId)))
            .Should().BeNull("a reveal is per-story consent, never a global unlock");
    }

    [Fact]
    public async Task StoryDetail_AnonymousWithCookieReveal_ReturnsStory()
    {
        int storyId = await SeedStoryAsync(rating: Rating.M);
        FakeActiveUserContext anon = FakeActiveUserContext.Anonymous();
        anon.AnonReveals.Add((RevealedEntityType.Story, storyId));
        SetActiveUser(anon);

        (await CallStoryReadAsync(s => s.GetStoryByIdAsync(storyId)))
            .Should().NotBeNull("anonymous reveals ride the prefs cookie (HasAnonRevealed)");
    }

    [Fact]
    public async Task StoryDetail_VerifiedBot_ReturnsStory()
    {
        int storyId = await SeedStoryAsync(rating: Rating.M);
        SetActiveUser(new FakeActiveUserContext { IsVerifiedBot = true });

        (await CallStoryReadAsync(s => s.GetStoryByIdAsync(storyId)))
            .Should().NotBeNull("Pattern B: verified crawlers are served full content on gated pages");
    }

    // ── Disclosure + Personal plane ───────────────────────────────────────────────

    [Fact]
    public async Task GatedCards_ReturnsHiddenMItemsOnly()
    {
        int mStoryId = await SeedStoryAsync(rating: Rating.M);
        int tStoryId = await SeedStoryAsync(rating: Rating.T);
        SetActiveUser(FakeActiveUserContext.Anonymous());

        IReadOnlyList<GatedMetadataDto> cards =
            await CallStoryReadAsync(s => s.GetGatedCardsAsync([mStoryId, tStoryId]));

        cards.Should().ContainSingle("only the M item is hidden from a T-ceiling viewer")
            .Which.RevealTargetId.Should().Be(mStoryId);
    }

    [Fact]
    public async Task PersonalScope_HydratesMStoriesFromOwnCandidateSet()
    {
        int mStoryId = await SeedStoryAsync(rating: Rating.M);
        int readerId = await SeedUserAsync("reader", showMature: false);
        SetActiveUser(readerId);

        (StoryListingDto[] items, int totalCount) = await CallStoryReadAsync(s =>
            s.GetListingsAsync(new StoryFilterDto { PageSize = 10 }, [mStoryId], personalScope: true));

        items.Should().ContainSingle(
                "Personal plane: a viewer's own interaction-backed candidate set is never rating-filtered " +
                "(this is what un-ghosts a mature-off reader's M favorites)")
            .Which.StoryId.Should().Be(mStoryId);
        totalCount.Should().Be(1);

        (StoryListingDto[] discoveryItems, _) = await CallStoryReadAsync(s =>
            s.GetListingsAsync(new StoryFilterDto { PageSize = 10 }, [mStoryId]));
        discoveryItems.Should().BeEmpty("without personalScope the Discovery-plane filter still applies");
    }

    // ── ProfileVisibility Class-A guard ───────────────────────────────────────────

    [Fact]
    public async Task PrivateProfile_ProfileTabReads_ReturnEmptyForOtherViewers()
    {
        int ownerId = await SeedUserAsync("private-owner");
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            User owner = (await db.Users.FindAsync(ownerId))!;
            owner.PrivacySettings.ProfileVisibility = ProfileVisibility.Private;
            await db.SaveChangesAsync();
        }

        SetActiveUser(FakeActiveUserContext.Anonymous());
        using IServiceScope readScope = Factory.Services.CreateScope();

        (await readScope.ServiceProvider.GetRequiredService<IUserStoryInteractionReadService>()
                .GetFavoriteStoryIdsAsync(ownerId, includePrivate: false))
            .Should().BeEmpty("a Private profile's favorites are Class-A gated at the service");

        (await readScope.ServiceProvider.GetRequiredService<IFollowingReadService>()
                .GetFollowedUsersAsync(ownerId))
            .Should().BeEmpty("a Private profile's following list is Class-A gated at the service");

        (await readScope.ServiceProvider.GetRequiredService<IUserProfileReadService>()
                .GetProfileAccessStateAsync(ownerId))
            .Should().Be(ProfileAccessState.Private, "the page renders the honest 'This profile is private.' state");
    }

    // ── Blog-post gate ────────────────────────────────────────────────────────────

    [Fact]
    public async Task BlogPostGate_MatureProfilePost_ReturnsMetadata()
    {
        int authorId = await SeedUserAsync("blogger");
        int postId;
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            ProfileBlogPost post = new()
            {
                AuthorId = authorId,
                Title = "Mature post",
                Content = "<p>content</p>",
                Rating = Rating.M,
                IsPublished = true,
                DateCreated = DateTime.UtcNow,
                LastUpdatedDate = DateTime.UtcNow,
            };
            db.ProfileBlogPosts.Add(post);
            await db.SaveChangesAsync();
            postId = post.BlogPostId;
        }

        SetActiveUser(FakeActiveUserContext.Anonymous());
        using IServiceScope readScope = Factory.Services.CreateScope();
        IBlogPostReadService blogPosts = readScope.ServiceProvider.GetRequiredService<IBlogPostReadService>();

        (await blogPosts.GetByIdAsync(postId)).Should().BeNull("no consent — the detail stays gated");

        GatedMetadataDto? gate = await blogPosts.GetBlogPostGateAsync(postId);
        gate.Should().NotBeNull();
        gate!.RevealTarget.Should().Be(RevealedEntityType.BlogPost,
            "a profile post's reveal target is the post itself (group posts target their group)");
        gate.RevealTargetId.Should().Be(postId);
    }

    // ── Feature 64 crawlability surface (HTTP) ────────────────────────────────────

    [Fact]
    public async Task Robots_AllowsSearch_BlocksAiTrainers_PointsAtSitemap()
    {
        HttpClient client = Factory.CreateClient();
        string body = await client.GetStringAsync("/robots.txt");

        body.Should().Contain("User-agent: *").And.Contain("Allow: /");
        body.Should().Contain("GPTBot").And.Contain("CCBot",
            "AI-training crawlers are disallowed (class norm, settled 2026-07-19)");
        body.Should().Contain("Sitemap: ").And.Contain("/sitemap.xml");
    }

    [Fact]
    public async Task Sitemap_IncludesMatureStory()
    {
        int mStoryId = await SeedStoryAsync(rating: Rating.M);

        HttpClient client = Factory.CreateClient();
        string body = await client.GetStringAsync("/sitemap.xml");

        body.Should().Contain($"/story/{mStoryId}",
            "index-all (decision row 11): M stories are gated, never de-listed — the interstitial is the indexable page");
    }

    [Fact]
    public async Task StaleSlug_Redirects301ToCanonical()
    {
        int storyId = await SeedStoryAsync(rating: Rating.E);
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            StoryDetail detail = await db.StoryDetails.SingleAsync(d => d.StoryId == storyId);
            detail.Slug = "canonical-slug";
            await db.SaveChangesAsync();
        }

        HttpClient client = Factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        HttpResponseMessage response = await client.GetAsync($"/story/{storyId}/stale-slug");

        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.ToString().Should().Be($"/story/{storyId}/canonical-slug");
    }

    [Fact]
    public async Task StoryGateEndpoint_AnonymousGetsMetadataJson_DetailStaysNull()
    {
        int storyId = await SeedStoryAsync(rating: Rating.M);
        SetActiveUser(FakeActiveUserContext.Anonymous());

        HttpClient client = Factory.CreateClient();

        GatedMetadataDto? gate =
            await client.GetFromJsonAsync<GatedMetadataDto?>($"/api/stories/{storyId}/gate");
        gate.Should().NotBeNull("the gate endpoint backs the WASM interstitial pass");
        gate!.RevealTargetId.Should().Be(storyId);

        // The detail endpoint withholds the body — an empty/null JSON response, never content.
        string detailBody = await client.GetStringAsync($"/api/stories/{storyId}");
        detailBody.Should().BeOneOf("", "null");
    }

    // ── Helper ────────────────────────────────────────────────────────────────────

    private async Task<T> CallStoryReadAsync<T>(Func<IStoryReadService, Task<T>> call)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await call(scope.ServiceProvider.GetRequiredService<IStoryReadService>());
    }
}
