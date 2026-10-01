using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// <see cref="StoryLifecycle"/> — the author transition table (owner ruling D1, WU-StoryLifecycle;
/// rule of record <c>layer2-services.md</c> §"Story Lifecycle"). Exhaustive over every
/// current × target × trusted combination (plus an undefined target), checked against a literal
/// table of the legal moves written out independently below, then the routing/no-op/message cases
/// by name. The server applies this function verbatim (Integration: <c>StoryLifecycleTests</c>).
/// Tier: Unit.
/// </summary>
public class StoryLifecycleTests
{
    private static readonly StoryStatusEnum[] Published =
    [
        StoryStatusEnum.InProgress, StoryStatusEnum.Completed, StoryStatusEnum.OnHiatus,
        StoryStatusEnum.Cancelled, StoryStatusEnum.Rewriting, StoryStatusEnum.OpenBeta,
    ];

    /// <summary>The legal author moves, written as data (not derived from the code under test):
    /// withdraw, revise, every published→published move and every unpublish. Submit is handled
    /// separately because its result depends on trust and PostApprovalStatus.</summary>
    private static readonly HashSet<(StoryStatusEnum From, StoryStatusEnum To)> LegalPlainMoves = BuildLegalMoves();

    private static HashSet<(StoryStatusEnum, StoryStatusEnum)> BuildLegalMoves()
    {
        HashSet<(StoryStatusEnum, StoryStatusEnum)> moves =
        [
            (StoryStatusEnum.PendingApproval, StoryStatusEnum.Draft), // withdraw
            (StoryStatusEnum.Rejected, StoryStatusEnum.Draft),        // revise
        ];
        foreach (StoryStatusEnum from in Published)
        {
            moves.Add((from, StoryStatusEnum.Draft)); // unpublish
            foreach (StoryStatusEnum to in Published)
                if (to != from) moves.Add((from, to));
        }
        return moves;
    }

    public static IEnumerable<object[]> EveryCombination()
    {
        StoryStatusEnum[] targets = [.. Enum.GetValues<StoryStatusEnum>(), (StoryStatusEnum)99];
        foreach (StoryStatusEnum current in Enum.GetValues<StoryStatusEnum>())
        foreach (StoryStatusEnum target in targets)
        foreach (bool trusted in new[] { false, true })
            yield return [current, target, trusted];
    }

    [Theory]
    [MemberData(nameof(EveryCombination))]
    public void ResolveAuthorTransition_MatchesTheLegalMoveTable(
        StoryStatusEnum current, StoryStatusEnum target, bool trusted)
    {
        // A valid entry status, so the submit row's only variable here is trust.
        const StoryStatusEnum postApproval = StoryStatusEnum.Completed;

        StoryTransitionResult result =
            StoryLifecycle.ResolveAuthorTransition(current, target, postApproval, trusted);

        if (!Enum.IsDefined(target))
        {
            result.IsAllowed.Should().BeFalse("an undefined enum value is never a target");
        }
        else if (target == current)
        {
            result.IsAllowed.Should().BeTrue("a same-status request is a no-op");
            result.ResultingStatus.Should().Be(current);
        }
        else if (current == StoryStatusEnum.Draft && target == StoryStatusEnum.PendingApproval)
        {
            result.IsAllowed.Should().BeTrue();
            result.ResultingStatus.Should().Be(trusted ? postApproval : StoryStatusEnum.PendingApproval,
                "the trust waiver routes a trusted author's submit straight to PostApprovalStatus");
        }
        else if (LegalPlainMoves.Contains((current, target)))
        {
            result.IsAllowed.Should().BeTrue();
            result.ResultingStatus.Should().Be(target);
        }
        else
        {
            result.IsAllowed.Should().BeFalse(
                $"{current} → {target} is not an author move (moderator-only or simply illegal)");
            result.Errors.Should().ContainSingle();
        }
    }

    // ── Submit (Draft → PendingApproval) ────────────────────────────────────────────

    [Theory]
    [InlineData(StoryStatusEnum.Draft)]      // not chosen yet
    [InlineData(StoryStatusEnum.OnHiatus)]   // defined, not an entry status
    [InlineData(StoryStatusEnum.Rejected)]
    [InlineData((StoryStatusEnum)42)]        // undefined
    public void Submit_WithoutAnEntryPostApprovalStatus_IsRefused_EvenWhenTrusted(StoryStatusEnum postApproval)
    {
        foreach (bool trusted in new[] { false, true })
        {
            StoryTransitionResult result = StoryLifecycle.ResolveAuthorTransition(
                StoryStatusEnum.Draft, StoryStatusEnum.PendingApproval, postApproval, trusted);

            result.IsAllowed.Should().BeFalse();
            result.Errors.Should().Equal(StoryLifecycle.PostApprovalStatusRequiredMessage);
        }
    }

    [Theory]
    [InlineData(StoryStatusEnum.InProgress)]
    [InlineData(StoryStatusEnum.Completed)]
    [InlineData(StoryStatusEnum.OpenBeta)]
    public void Submit_Trusted_LandsAtPostApprovalStatus_Untrusted_EntersTheQueue(StoryStatusEnum postApproval)
    {
        StoryLifecycle.ResolveAuthorTransition(StoryStatusEnum.Draft, StoryStatusEnum.PendingApproval, postApproval, trusted: true)
            .ResultingStatus.Should().Be(postApproval);
        StoryLifecycle.ResolveAuthorTransition(StoryStatusEnum.Draft, StoryStatusEnum.PendingApproval, postApproval, trusted: false)
            .ResultingStatus.Should().Be(StoryStatusEnum.PendingApproval);
    }

    // ── Refusal messages (user-facing) ──────────────────────────────────────────────

    [Theory]
    [InlineData(StoryStatusEnum.PendingApproval, StoryStatusEnum.Rejected, "Only a moderator can reject a story.")]
    [InlineData(StoryStatusEnum.InProgress, StoryStatusEnum.Rejected, "Only a moderator can reject a story.")]
    [InlineData(StoryStatusEnum.PendingApproval, StoryStatusEnum.InProgress, "A story awaiting review is published by a moderator's approval.")]
    [InlineData(StoryStatusEnum.Draft, StoryStatusEnum.Completed, "Submit the story for publication instead.")]
    [InlineData(StoryStatusEnum.Rejected, StoryStatusEnum.PendingApproval, "Return a rejected story to draft before submitting it again.")]
    [InlineData(StoryStatusEnum.Rejected, StoryStatusEnum.InProgress, "Return a rejected story to draft before submitting it again.")]
    [InlineData(StoryStatusEnum.OnHiatus, StoryStatusEnum.PendingApproval, "A story can't move from OnHiatus to PendingApproval.")]
    public void IllegalMoves_ExplainThemselves(StoryStatusEnum current, StoryStatusEnum target, string expected)
    {
        StoryLifecycle.ResolveAuthorTransition(current, target, StoryStatusEnum.InProgress, trusted: true)
            .Errors.Should().Equal(expected);
    }

    [Fact]
    public void UndefinedTarget_IsRefused()
    {
        StoryLifecycle.ResolveAuthorTransition(StoryStatusEnum.Draft, (StoryStatusEnum)99, StoryStatusEnum.InProgress, trusted: true)
            .Errors.Should().Equal("That isn't a valid story status.");
    }

    // ── Set predicates ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(StoryStatusEnum.Draft, false, false)]
    [InlineData(StoryStatusEnum.PendingApproval, false, false)]
    [InlineData(StoryStatusEnum.InProgress, true, true)]
    [InlineData(StoryStatusEnum.Completed, true, true)]
    [InlineData(StoryStatusEnum.OnHiatus, true, false)]
    [InlineData(StoryStatusEnum.Cancelled, true, false)]
    [InlineData(StoryStatusEnum.Rewriting, true, false)]
    [InlineData(StoryStatusEnum.OpenBeta, true, true)]
    [InlineData(StoryStatusEnum.Rejected, false, false)]
    public void IsPublished_And_IsEntryStatus(StoryStatusEnum status, bool published, bool entry)
    {
        StoryLifecycle.IsPublished(status).Should().Be(published);
        StoryLifecycle.IsEntryStatus(status).Should().Be(entry);
    }
}
