using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="ReadItLaterRecommendationCard"/> — the write composite Explore, Deep
/// Dive and Community Spotlight render beside a story card (Feature 30, owner ruling D3,
/// WU-InertFeatures; these tests from its review fixes). Covers: an anonymous click is a login nudge
/// and calls nothing; a signed-in click calls the card producer and raises <c>OnSaved</c> with the story
/// id; a refused save shows inline (session expiry with a sign-in link) and raises nothing; the saved
/// state is the host's parameter, never latched — a save the host has not confirmed shows nothing, and
/// a host that later clears the bit re-enables the button.
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class ReadItLaterRecommendationCardTests : BunitContext
{
    private readonly FakeUserStoryInteractionWriteService _interactions = new();

    public ReadItLaterRecommendationCardTests()
    {
        Services.AddScoped<IUserStoryInteractionWriteService>(_ => _interactions);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static RecommendationDto Rec() => new(
        RecommendationId: 8, StoryId: 5, Recommender: new UserCardDto(42, "Writer", null, null, []),
        BodyHtml: "<p>Read this.</p>", LikeCount: 0, IsHiddenGem: false, IsHighlightedByAuthor: false,
        SuccessfulRecCount: 0, DatePosted: new DateTime(2026, 1, 1), IsLikedByCurrentUser: false,
        IsOwnRecommendation: false);

    private IRenderedComponent<ReadItLaterRecommendationCard> RenderCard(
        int? currentUserId, Action<int>? onSaved = null, bool saved = false) =>
        Render<ReadItLaterRecommendationCard>(p => p
            .Add(c => c.Rec, Rec())
            .Add(c => c.CurrentUserId, currentUserId)
            .Add(c => c.IsReadItLaterSaved, saved)
            .Add(c => c.OnSaved, EventCallback.Factory.Create<int>(this, id => onSaved?.Invoke(id))));

    private const string SaveButton = "[aria-label='Save this story to Read It Later']";

    [Fact]
    public async Task Anonymous_Click_NavigatesToLogin_WithoutCallingTheService()
    {
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/discover");
        int? raised = null;
        IRenderedComponent<ReadItLaterRecommendationCard> cut = RenderCard(currentUserId: null, id => raised = id);

        await cut.Find(SaveButton).ClickAsync(new());

        _interactions.ReadItLaterFromRecommendationCalls.Should().BeEmpty();
        raised.Should().BeNull();
        nav.Uri.Should().Contain("/Account/Login?ReturnUrl=%2Fdiscover", "D3: an anonymous card click is a login nudge");
    }

    [Fact]
    public async Task SignedIn_Click_CallsTheCardProducer_AndRaisesOnSavedWithTheStoryId()
    {
        int? raised = null;
        IRenderedComponent<ReadItLaterRecommendationCard> cut = RenderCard(currentUserId: 3, id => raised = id);

        await cut.Find(SaveButton).ClickAsync(new());

        _interactions.ReadItLaterFromRecommendationCalls.Should().Equal([8]);
        raised.Should().Be(5, "the host re-reads that story's state for the StoryCard panel beside the card");
        cut.FindAll("[role=alert]").Should().BeEmpty();
    }

    [Fact]
    public async Task RefusedSave_ShowsTheRefusalInline_AndRaisesNothing()
    {
        _interactions.ReadItLaterFromRecommendationThrows = new KeyNotFoundException();
        int? raised = null;
        IRenderedComponent<ReadItLaterRecommendationCard> cut = RenderCard(currentUserId: 3, id => raised = id);

        await cut.Find(SaveButton).ClickAsync(new());

        raised.Should().BeNull();
        cut.Find("[role=alert]").TextContent.Should().Contain(ExceptionPresenter.NotFoundMessage);
        cut.Find(SaveButton).HasAttribute("disabled").Should().BeFalse("nothing was saved");
    }

    [Fact]
    public async Task ExpiredSession_ShowsASignInLink()
    {
        _interactions.ReadItLaterFromRecommendationThrows = new SessionExpiredException();
        IRenderedComponent<ReadItLaterRecommendationCard> cut = RenderCard(currentUserId: 3);

        await cut.Find(SaveButton).ClickAsync(new());

        cut.Find("a[href^='/Account/Login?ReturnUrl=']").TextContent.Should().Contain("Sign in");
    }

    [Fact]
    public async Task SavedState_IsTheHosts_NeverLatched()
    {
        IRenderedComponent<ReadItLaterRecommendationCard> cut = RenderCard(currentUserId: 3);
        await cut.Find(SaveButton).ClickAsync(new());

        // Until the host's re-read hands the bit down, the card claims nothing.
        cut.Find(SaveButton).HasAttribute("disabled").Should().BeFalse();

        cut.Render(p => p.Add(c => c.IsReadItLaterSaved, true));
        cut.Find("[aria-label='Saved to Read It Later']").HasAttribute("disabled").Should().BeTrue();

        // The story card's panel beside it cleared Read It Later: the button must come back, or the
        // reader could never start a new attribution here (D3: "a re-RIL after a clear starts a new one").
        cut.Render(p => p.Add(c => c.IsReadItLaterSaved, false));
        cut.Find(SaveButton).HasAttribute("disabled").Should().BeFalse();
    }
}
