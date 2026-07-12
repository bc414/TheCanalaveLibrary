using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="PollRules"/> (Feature 37) — the dependency-free lifecycle and
/// results-visibility rules shared by <c>ServerPollReadService</c>/<c>ServerPollWriteService</c>.
/// Tier: Unit (directly-constructed, no host/DB).
/// </summary>
public class PollRulesTests
{
    private static readonly DateTime Now = new(2026, 7, 12, 12, 0, 0, DateTimeKind.Utc);

    // ── StatusFor ────────────────────────────────────────────────────────────────

    [Fact]
    public void StatusFor_FutureOpenDate_IsPending() =>
        PollRules.StatusFor(Now.AddHours(1), null, Now).Should().Be(PollStatus.Pending);

    [Fact]
    public void StatusFor_PastOpenNoClose_IsOpen() =>
        PollRules.StatusFor(Now.AddHours(-1), null, Now).Should().Be(PollStatus.Open);

    [Fact]
    public void StatusFor_PastOpenFutureClose_IsOpen() =>
        PollRules.StatusFor(Now.AddHours(-1), Now.AddHours(1), Now).Should().Be(PollStatus.Open);

    [Fact]
    public void StatusFor_PastClose_IsClosed() =>
        PollRules.StatusFor(Now.AddHours(-2), Now.AddHours(-1), Now).Should().Be(PollStatus.Closed);

    [Fact]
    public void StatusFor_CloseAtExactlyNow_IsClosed() =>
        PollRules.StatusFor(Now.AddHours(-1), Now, Now).Should().Be(PollStatus.Closed);

    [Fact]
    public void StatusFor_ClosedWinsOverPending()
    {
        // A manually-closed scheduled poll (close stamped before its open date arrived)
        // must read Closed, not Pending — closed always wins.
        PollRules.StatusFor(Now.AddHours(1), Now.AddMinutes(-5), Now).Should().Be(PollStatus.Closed);
    }

    // ── ResultsVisible ───────────────────────────────────────────────────────────

    [Fact]
    public void ResultsVisible_OwnerOrModerator_AlwaysTrue()
    {
        foreach (PollResultsVisibility visibility in Enum.GetValues<PollResultsVisibility>())
            PollRules.ResultsVisible(visibility, PollStatus.Open,
                    viewerHasCurrentVote: false, viewerIsOwnerOrModerator: true)
                .Should().BeTrue($"owner/mod always sees results ({visibility})");
    }

    [Fact]
    public void ResultsVisible_Always_TrueForNonVoter() =>
        PollRules.ResultsVisible(PollResultsVisibility.Always, PollStatus.Open, false, false)
            .Should().BeTrue();

    [Fact]
    public void ResultsVisible_AfterClose_FalseWhileOpen_TrueWhenClosed()
    {
        PollRules.ResultsVisible(PollResultsVisibility.AfterClose, PollStatus.Open, true, false)
            .Should().BeFalse("even a voter waits for close");
        PollRules.ResultsVisible(PollResultsVisibility.AfterClose, PollStatus.Closed, false, false)
            .Should().BeTrue();
    }

    [Fact]
    public void ResultsVisible_AfterVote_TracksCurrentVoteState()
    {
        PollRules.ResultsVisible(PollResultsVisibility.AfterVote, PollStatus.Open, true, false)
            .Should().BeTrue();
        // Settled 2026-07-12: retract → results hide again (pure function of CURRENT state).
        PollRules.ResultsVisible(PollResultsVisibility.AfterVote, PollStatus.Open, false, false)
            .Should().BeFalse();
    }
}
