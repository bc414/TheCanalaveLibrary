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
/// <see cref="PollsPage"/>'s inline editor refusal (the WU-ErrorHandling2 follow-up, built by WU-TrackerFixes
/// 2026-10-13; this test added by its review fixes): a refused save hands <see cref="PollEditorForm"/> the
/// messages and the session flag together, so Sign in shows for an expired session and never beside a
/// later, different refusal. <see cref="PollEditorFormTests"/> covers the form's rendering of the flag;
/// this covers the page setting it. Tier: RazorComponents (bUnit).
/// </summary>
public class PollsPageTests : BunitContext
{
    private readonly ScriptedPollWriteService _polls = new();

    public PollsPageTests()
    {
        Services.AddScoped<IPollWriteService>(_ => _polls);
        this.AddAuthorization()
            .SetAuthorized("mod")
            .SetRoles("Moderator")
            .SetClaims(new Claim(ClaimTypes.NameIdentifier, "3"));
    }

    private static int SignInLinks(IRenderedComponent<PollsPage> cut) =>
        cut.FindAll("a").Count(a => a.TextContent.Trim() == "Sign in");

    private static Task ClickAsync(IRenderedComponent<PollsPage> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == text).ClickAsync(new MouseEventArgs());

    [Fact]
    public async Task Save_AnExpiredSession_OffersSignIn_AndALaterRefusalDropsIt()
    {
        IRenderedComponent<PollsPage> cut = Render<PollsPage>();
        await ClickAsync(cut, "New Site Poll");

        _polls.CreateException = new SessionExpiredException();
        await ClickAsync(cut, "Create Poll");

        cut.Find("[role='alert']").TextContent.Should().Contain("session has expired");
        SignInLinks(cut).Should().Be(1, "the page passes ErrorsIsSessionExpired with its error list");

        _polls.CreateException = new PollValidationException(["A poll needs a question."]);
        await ClickAsync(cut, "Create Poll");

        cut.Find("[role='alert']").TextContent.Should().Contain("A poll needs a question.");
        SignInLinks(cut).Should().Be(0, "the flag travels with the message");
    }

    /// <summary>Lists no polls and throws <see cref="CreateException"/> from a create; nothing else is used.</summary>
    private sealed class ScriptedPollWriteService : IPollWriteService
    {
        public Exception? CreateException { get; set; }

        public Task<PollDto[]> GetSitePollsAsync(bool includeArchived) => Task.FromResult(Array.Empty<PollDto>());
        public Task<int> CreateSitePollAsync(PollEditDto dto) =>
            CreateException is null ? Task.FromResult(1) : throw CreateException;

        public Task<PollDto?> VoteAsync(int pollId, int[] optionIds, bool voteAnonymously) => throw new NotSupportedException();
        public Task<int> CreateBlogPostPollAsync(int blogPostId, PollEditDto dto) => throw new NotSupportedException();
        public Task UpdatePollAsync(int pollId, PollEditDto dto) => throw new NotSupportedException();
        public Task ClosePollAsync(int pollId) => throw new NotSupportedException();
        public Task SetSitePollArchivedAsync(int pollId, bool archived) => throw new NotSupportedException();
        public Task DeletePollAsync(int pollId) => throw new NotSupportedException();
        public Task<PollDto[]> GetPollsForBlogPostAsync(int blogPostId) => throw new NotSupportedException();
        public Task<PollDto?> GetPollAsync(int pollId) => throw new NotSupportedException();
    }
}
