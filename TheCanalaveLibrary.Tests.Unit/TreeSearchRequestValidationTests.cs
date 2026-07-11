using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Request-shape rules of Automatic Tree Search (F59, `layer8-data-marts.md` §"The Automatic
/// Tree Search consumer") — exercised through the public <c>TraverseAsync</c> surface: the
/// service validates BEFORE touching any dependency, so invalid requests must throw
/// <see cref="ArgumentException"/> and valid ones must get past validation (proven by hitting
/// the throwing stub context factory — no host, no DB, per testing.md's Unit placement rule).
/// </summary>
public sealed class TreeSearchRequestValidationTests
{
    private static ServerTreeSearchReadService BuildSut() => new(
        new ThrowingContextFactory(),
        new StubActiveUserContext { UserId = 1, IsAuthenticated = true },
        new StubDiscoveryDefaults(),
        null!); // storyReadService — only SearchAsync (WU44) touches it; these tests exercise TraverseAsync

    private static TreeSearchRequest ValidRequest() => new()
    {
        RootStoryId = 1,
        MaxDegrees = 2,
        EdgeTypes = [TreeSearchEdgeType.Favorite],
    };

    [Fact]
    public async Task Traverse_WithBothRoots_Throws()
    {
        Func<Task> act = () => BuildSut().TraverseAsync(
            ValidRequest() with { RootStoryId = 1, RootUserId = 2 });
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Exactly one*");
    }

    [Fact]
    public async Task Traverse_WithNoRoot_Throws()
    {
        Func<Task> act = () => BuildSut().TraverseAsync(
            ValidRequest() with { RootStoryId = null, RootUserId = null });
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Exactly one*");
    }

    [Fact]
    public async Task Traverse_WithEmptyEdgeSet_Throws()
    {
        Func<Task> act = () => BuildSut().TraverseAsync(ValidRequest() with { EdgeTypes = [] });
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*edge type*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)] // beyond the ceiling — small-world reachability makes deeper walks pure cost
    public async Task Traverse_WithOutOfRangeMaxDegrees_Throws(int maxDegrees)
    {
        Func<Task> act = () => BuildSut().TraverseAsync(ValidRequest() with { MaxDegrees = maxDegrees });
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*MaxDegrees*");
    }

    [Fact]
    public async Task Traverse_WithNonPositiveResultCap_Throws()
    {
        Func<Task> act = () => BuildSut().TraverseAsync(ValidRequest() with { ResultCap = 0 });
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*ResultCap*");
    }

    [Theory] // paths are only meaningful on the truly-capped chain-of-trust edges (AD1/AD2)
    [InlineData(TreeSearchEdgeType.Favorite)]
    [InlineData(TreeSearchEdgeType.AuthoredBy)]
    [InlineData(TreeSearchEdgeType.Recommendation)]
    [InlineData(TreeSearchEdgeType.Vouch)] // ≤5 vouchees but unbounded stories each — NOT path-capable
    public async Task Traverse_PathsRequestedOnUnboundedEdge_Throws(TreeSearchEdgeType edge)
    {
        Func<Task> act = () => BuildSut().TraverseAsync(ValidRequest() with
        {
            IncludePaths = true,
            EdgeTypes = [TreeSearchEdgeType.HiddenGem, edge],
        });
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*chain-of-trust*");
    }

    [Fact]
    public async Task Traverse_PathsOnChainOfTrustEdges_PassesValidation()
    {
        // Getting past validation means reaching the (deliberately throwing) context factory.
        Func<Task> act = () => BuildSut().TraverseAsync(ValidRequest() with
        {
            IncludePaths = true,
            MaxDegrees = 6,
            EdgeTypes = [TreeSearchEdgeType.HiddenGem, TreeSearchEdgeType.AuthorSpotlight],
        });
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(StubMarker);
    }

    [Fact]
    public async Task Traverse_ValidWideRequest_PassesValidation()
    {
        Func<Task> act = () => BuildSut().TraverseAsync(ValidRequest());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(StubMarker);
    }

    private const string StubMarker = "stub context factory reached";

    private sealed class ThrowingContextFactory : IDbContextFactory<ReadOnlyApplicationDbContext>
    {
        public ReadOnlyApplicationDbContext CreateDbContext() => throw new InvalidOperationException(StubMarker);
    }

    private sealed class StubDiscoveryDefaults : IDiscoveryDefaultsReadService
    {
        public Task<IReadOnlyList<UserStoryInteractionTypeEnum>> GetDefaultExcludedInteractionsAsync(
            string searchModeKey) =>
            Task.FromResult<IReadOnlyList<UserStoryInteractionTypeEnum>>([]);
    }
}
