using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// <see cref="BlogPostEditorPage"/>'s poll panel (the WU-ErrorHandling2 follow-up, built by WU-TrackerFixes
/// 2026-10-13; this test added by its review fixes): a refused poll save hands <see cref="PollEditorForm"/>
/// the messages and the session flag together, as <see cref="PollsPageTests"/> checks for the site-poll
/// page. Tier: RazorComponents (bUnit).
/// </summary>
public class BlogPostEditorPagePollTests : BunitContext
{
    private const int AuthorId = 3;
    private const int BlogPostId = 9;

    private Exception? _createPollException;

    public BlogPostEditorPagePollTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();

        IBlogPostWriteService posts = ScriptedProxy.Create<IBlogPostWriteService>(out ScriptedProxy postScript);
        postScript.On(nameof(IBlogPostWriteService.GetForEditAsync), _ => Task.FromResult<BlogPostEditDto?>(
            new BlogPostEditDto(BlogPostId, AuthorId, "A post", "<p>Body.</p>", Rating.E, HasSpoilers: false,
                StoryId: null, IsPublished: true)));
        Services.AddSingleton(posts);

        IPollWriteService polls = ScriptedProxy.Create<IPollWriteService>(out ScriptedProxy pollScript);
        pollScript
            .On(nameof(IPollWriteService.GetPollsForBlogPostAsync), _ => Task.FromResult(Array.Empty<PollDto>()))
            .On(nameof(IPollWriteService.CreateBlogPostPollAsync), _ =>
                _createPollException is null ? Task.FromResult(1) : Task.FromException<int>(_createPollException));
        Services.AddSingleton(polls);

        Services.AddSingleton<IStoryReadService>(new FakeStoryReadService()); // the story-link picker: none
        Services.AddScoped<DraftStore>();
        Services.AddSingleton<IToastService>(new ToastService());

        this.AddAuthorization().SetAuthorized("author").SetClaims(new Claim(ClaimTypes.NameIdentifier, AuthorId.ToString()));
    }

    private static int SignInLinks(IRenderedComponent<BlogPostEditorPage> cut) =>
        cut.FindAll("a").Count(a => a.TextContent.Trim() == "Sign in");

    private static Task ClickAsync(IRenderedComponent<BlogPostEditorPage> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == text).ClickAsync(new MouseEventArgs());

    [Fact]
    public async Task PollSave_AnExpiredSession_OffersSignIn_AndALaterRefusalDropsIt()
    {
        IRenderedComponent<BlogPostEditorPage> cut = Render<BlogPostEditorPage>(p => p.Add(c => c.BlogPostId, BlogPostId));
        await ClickAsync(cut, "Add Poll");

        _createPollException = new SessionExpiredException();
        await ClickAsync(cut, "Create Poll");

        cut.Markup.Should().Contain("session has expired");
        SignInLinks(cut).Should().Be(1, "the panel passes ErrorsIsSessionExpired with its error list");

        _createPollException = new PollValidationException(["A poll needs a question."]);
        await ClickAsync(cut, "Create Poll");

        cut.Markup.Should().Contain("A poll needs a question.");
        cut.Markup.Should().NotContain("session has expired");
        SignInLinks(cut).Should().Be(0, "the flag travels with the message");
    }
}
