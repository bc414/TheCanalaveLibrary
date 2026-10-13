using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Regression tests for a runtime bug found by the WU-CounterSymmetry browser pass (2026-09-30):
/// <see cref="MyStoryLineagesPage"/>'s Approve, refused because another tab had already approved the
/// request (it is no longer Pending), showed the refusal but left the stale row listed. That row still
/// offered Approve and Reject, and Reject acts on an Approved row by design, so it would revoke the
/// approval the other tab made. A request the requester removed left its row behind the same way.
/// The handlers now keep the message and reload the list (error-handling.md §"Layered error
/// boundaries", the reload-after-refusal paragraph).
///
/// <para>Tier: RazorComponents (bUnit, no host or DB). The server-side refusal itself (Pending-only
/// approve, the 400) is Integration-covered by <c>CounterSymmetryTests</c>.</para>
/// </summary>
public class MyStoryLineagesPageTests : BunitContext
{
    private const int TestUserId = 4;
    private const string NoLongerPending = "This lineage request is no longer pending.";

    private readonly FakeStoryLineageWriteService _lineage = new();

    public MyStoryLineagesPageTests()
    {
        _lineage.Incoming.Add(Request(6, 1, "With OC Details", "Five Chapters"));
        _lineage.Incoming.Add(Request(12, 3, "Filler", "Completed One-Shot"));

        Services.AddScoped<IStoryLineageWriteService>(_ => _lineage);
        Services.AddScoped<IStoryReadService>(_ => new FakeStoryReadService());
        Services.AddScoped<IToastService>(_ => new FakeToastService());

        // AuthorizeView needs bUnit's authorization double (SeriesCreateEditPageTests' pattern); the
        // page resolves its own user id from ClaimTypes.NameIdentifier.
        this.AddAuthorization().SetAuthorized("AuthorAlpha").SetClaims(
            new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()));
    }

    private static StoryLineageIncomingRequestDto Request(int source, int target, string sourceTitle, string targetTitle) =>
        new(source, sourceTitle, RequesterUserId: 5, RequesterUserName: "AuthorBeta",
            target, targetTitle, TypeId: 1, TypeName: "Inspired By");

    private static IElement IncomingRow(IRenderedComponent<MyStoryLineagesPage> cut, string sourceTitle) =>
        cut.FindAll("li").First(li => li.TextContent.Contains(sourceTitle));

    private static IElement RowButton(IElement row, string text) =>
        row.QuerySelectorAll("button").First(b => b.TextContent.Trim() == text);

    [Fact]
    public void ApproveRefusedBecauseNoLongerPending_ShowsTheMessage_AndDropsTheStaleRow()
    {
        IRenderedComponent<MyStoryLineagesPage> cut = Render<MyStoryLineagesPage>();
        cut.WaitForAssertion(() => cut.FindAll("li").Should().HaveCount(2));

        // Another tab approved 6→1 after this page loaded: the server no longer lists it as Pending
        // and refuses this page's click (the 400 both phases surface as this exception).
        _lineage.MoveOutOfPending(6, 1);
        _lineage.RefuseWith = new StoryLineageValidationException([NoLongerPending]);

        RowButton(IncomingRow(cut, "With OC Details"), "Approve").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain(NoLongerPending, "the refusal still explains itself inline");
            cut.FindAll("li").Where(li => li.TextContent.Contains("With OC Details")).Should().BeEmpty(
                "the list reloads, so the stale row no longer offers a Reject that would revoke it");
            cut.FindAll("li").Should().ContainSingle(li => li.TextContent.Contains("Filler"),
                "the other pending request is untouched");
        });
    }

    // The requester removed the request after this page loaded: approve and reject both 404
    // (KeyNotFoundException on both phases). Reject never refuses an Approved row — revoking one is by
    // design — so a removed row is its only "moved under the page" refusal.
    [Theory]
    [InlineData("Approve")]
    [InlineData("Reject")]
    public void RefusedBecauseTheRequesterRemovedIt_DropsTheStaleRow(string action)
    {
        IRenderedComponent<MyStoryLineagesPage> cut = Render<MyStoryLineagesPage>();
        cut.WaitForAssertion(() => cut.FindAll("li").Should().HaveCount(2));

        _lineage.MoveOutOfPending(6, 1);
        _lineage.RefuseWith = new KeyNotFoundException("Lineage link not found.");

        RowButton(IncomingRow(cut, "With OC Details"), action).Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("li").Where(li => li.TextContent.Contains("With OC Details")).Should().BeEmpty();
            cut.FindAll("li").Should().ContainSingle(li => li.TextContent.Contains("Filler"));
        });
    }

    [Fact]
    public void ARefusalWhoseReloadAlsoFails_KeepsTheRefusalAndAppendsTheReloadError()
    {
        IRenderedComponent<MyStoryLineagesPage> cut = Render<MyStoryLineagesPage>();
        cut.WaitForAssertion(() => cut.FindAll("li").Should().HaveCount(2));

        _lineage.RefuseWith = new StoryLineageValidationException([NoLongerPending]);
        _lineage.FailNextReloadWith = new StoryLineageValidationException(["Reload refused."]);

        RowButton(IncomingRow(cut, "With OC Details"), "Approve").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain(NoLongerPending, "a failed reload never replaces the refusal");
            cut.Markup.Should().Contain("Reload refused.");
        });
    }
}

/// <summary>
/// In-memory <see cref="IStoryLineageWriteService"/> for <see cref="MyStoryLineagesPageTests"/>:
/// <see cref="Incoming"/> is the server's Pending list, <see cref="RefuseWith"/> makes the next
/// approve/reject throw, and <see cref="FailNextReloadWith"/> makes the next manage-data read throw.
/// </summary>
internal sealed class FakeStoryLineageWriteService : IStoryLineageWriteService
{
    public List<StoryLineageIncomingRequestDto> Incoming { get; } = [];
    public Exception? RefuseWith { get; set; }
    public Exception? FailNextReloadWith { get; set; }

    public void MoveOutOfPending(int source, int target) =>
        Incoming.RemoveAll(r => r.SourceStoryId == source && r.TargetStoryId == target);

    public Task<IReadOnlyList<StoryLineageDto>> GetLineageForStoryAsync(int storyId) =>
        Task.FromResult<IReadOnlyList<StoryLineageDto>>([]);

    public Task<StoryLineageManageDto> GetManageDataForUserAsync()
    {
        if (FailNextReloadWith is { } failure)
        {
            FailNextReloadWith = null;
            throw failure;
        }
        return Task.FromResult(new StoryLineageManageDto([], [.. Incoming]));
    }

    public Task<IReadOnlyList<StoryLineageTypeDto>> GetLineageTypesAsync() =>
        Task.FromResult<IReadOnlyList<StoryLineageTypeDto>>([new StoryLineageTypeDto(1, "Inspired By")]);

    public Task RequestLineageAsync(CreateStoryLineageDto dto) => Task.CompletedTask;

    public Task ApproveLineageAsync(int sourceStoryId, int targetStoryId, short typeId) => Act(sourceStoryId, targetStoryId);

    public Task RejectLineageAsync(int sourceStoryId, int targetStoryId, short typeId) => Act(sourceStoryId, targetStoryId);

    public Task DeleteLineageAsync(int sourceStoryId, int targetStoryId, short typeId) => Task.CompletedTask;

    private Task Act(int source, int target)
    {
        if (RefuseWith is { } refusal)
        {
            RefuseWith = null;
            throw refusal;
        }
        MoveOutOfPending(source, target);
        return Task.CompletedTask;
    }
}
