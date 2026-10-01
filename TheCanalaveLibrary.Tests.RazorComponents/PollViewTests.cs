using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// <see cref="PollView"/>'s handling of a vote result (WU-AccessGateSweep2 review fixes). Under owner
/// ruling D6 a pure vote withdrawal on a poll whose post was hidden after the page loaded still
/// lands, and <see cref="IPollWriteService.VoteAsync"/> returns <c>null</c> rather than the hidden
/// poll's content. The component must hand that null to its parent — which drops the poll, exactly
/// as after a delete — instead of dereferencing it. L3 <c>@code</c> EventCallback logic, so this tier
/// (<c>testing.md</c>). The service behavior itself is Integration
/// (<c>ParentVisibilityContractTests</c>).
/// Tier: RazorComponents (bUnit).
/// </summary>
public class PollViewTests : BunitContext
{
    private readonly FakePollWriteService _polls = new();

    public PollViewTests()
    {
        Services.AddScoped<IPollWriteService>(_ => _polls);
    }

    private static PollDto VotedPoll(int votedOptionId) => new(
        PollId: 7, PollName: "Favorite region", Description: null,
        DateOpened: DateTime.UtcNow.AddDays(-1), DateClosed: null,
        AllowMultiple: false, ResultsVisibility: PollResultsVisibility.Always,
        AnonymityMode: PollAnonymityMode.Public, OwnerId: 1, OwnerUserName: "author",
        IsArchived: false, BlogPostId: 3, Status: PollStatus.Open, ResultsVisibleToViewer: true,
        ConfigLocked: true, ViewerVotedOptionIds: [votedOptionId], ViewerVotedAnonymously: false,
        TotalVoterCount: 1,
        Options:
        [
            new PollOptionResultDto(70, "Sinnoh", 0, 1, []),
            new PollOptionResultDto(71, "Johto", 1, 0, []),
        ]);

    private (IRenderedComponent<PollView> Cut, List<PollDto?> Raised) RenderVoted()
    {
        List<PollDto?> raised = [];
        IRenderedComponent<PollView> cut = Render<PollView>(p => p
            .Add(c => c.Poll, VotedPoll(70))
            .Add(c => c.CurrentUserId, 5)
            .Add(c => c.OnPollChanged, (PollDto? dto) => raised.Add(dto)));
        return (cut, raised);
    }

    [Fact]
    public void Retract_NullResult_RaisesNullSoTheParentDropsThePoll()
    {
        _polls.NextVoteResult = null; // the post was hidden after the page loaded
        (IRenderedComponent<PollView> cut, List<PollDto?> raised) = RenderVoted();

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Retract vote").Click();

        _polls.LastVote.Should().NotBeNull();
        _polls.LastVote!.Value.PollId.Should().Be(7);
        _polls.LastVote.Value.OptionIds.Should().BeEmpty("Retract sends the empty set");
        raised.Should().ContainSingle().Which.Should().BeNull(
            "a withdrawal from a poll the viewer can no longer see leaves nothing to render");
        cut.FindAll("[role=alert]").Should().BeEmpty("a landed withdrawal is not an error");
    }

    [Fact]
    public void Retract_RefreshedResult_RaisesTheRefreshedPoll()
    {
        PollDto refreshed = VotedPoll(70) with { ViewerVotedOptionIds = [], TotalVoterCount = 0 };
        _polls.NextVoteResult = refreshed;
        (IRenderedComponent<PollView> cut, List<PollDto?> raised) = RenderVoted();

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Retract vote").Click();

        raised.Should().ContainSingle().Which.Should().BeSameAs(refreshed);
    }

    [Fact]
    public void PublicVoterName_LinksToTheVotersProfileRoute()
    {
        // The voter link pointed at /profile/{id}, a route that does not exist (404) — found in the
        // WU-AccessGateSweep2 browser pass. Profiles live at /user/{id}.
        PollDto poll = VotedPoll(70) with
        {
            Options =
            [
                new PollOptionResultDto(70, "Sinnoh", 0, 1, [new PollVoterDto(12, "voter")]),
                new PollOptionResultDto(71, "Johto", 1, 0, []),
            ],
        };
        IRenderedComponent<PollView> cut = Render<PollView>(p => p
            .Add(c => c.Poll, poll)
            .Add(c => c.CurrentUserId, 5));

        cut.FindAll("a").Single(a => a.TextContent.Trim() == "voter")
            .GetAttribute("href").Should().Be("/user/12");
    }

    /// <summary>Records the last vote and returns a scripted result; every other member is unused.</summary>
    private sealed class FakePollWriteService : IPollWriteService
    {
        public PollDto? NextVoteResult { get; set; }
        public (int PollId, int[] OptionIds)? LastVote { get; private set; }

        public Task<PollDto?> VoteAsync(int pollId, int[] optionIds, bool voteAnonymously)
        {
            LastVote = (pollId, optionIds);
            return Task.FromResult(NextVoteResult);
        }

        public Task<int> CreateSitePollAsync(PollEditDto dto) => throw new NotSupportedException();
        public Task<int> CreateBlogPostPollAsync(int blogPostId, PollEditDto dto) => throw new NotSupportedException();
        public Task UpdatePollAsync(int pollId, PollEditDto dto) => throw new NotSupportedException();
        public Task ClosePollAsync(int pollId) => throw new NotSupportedException();
        public Task SetSitePollArchivedAsync(int pollId, bool archived) => throw new NotSupportedException();
        public Task DeletePollAsync(int pollId) => throw new NotSupportedException();
        public Task<PollDto[]> GetSitePollsAsync(bool includeArchived) => throw new NotSupportedException();
        public Task<PollDto[]> GetPollsForBlogPostAsync(int blogPostId) => throw new NotSupportedException();
        public Task<PollDto?> GetPollAsync(int pollId) => throw new NotSupportedException();
    }
}
