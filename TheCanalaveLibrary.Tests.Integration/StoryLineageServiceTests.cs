using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="IStoryLineageWriteService"/> / <see cref="IStoryLineageReadService"/>
/// (Feature 10, WU42 — formerly "Story Relationships," renamed to disambiguate from
/// <c>StoryCharacterPairing</c> and <c>UserStoryInteraction</c>). Covers: cross-author request/
/// approve/reject workflow, self-owned auto-approve (no self-notification), ownership gating on
/// both sides of the link, re-request-after-rejection row reuse, cascade delete from either story,
/// and the viewer-visible display rule for <see cref="StoryLineageDto"/> (mirrors Series' Feature 9
/// mature-drop rule). Also covers <see cref="IStoryReadService.SearchStoriesByTitleAsync"/> (WU42),
/// the reusable typeahead search backing the target picker.
///
/// <b>Per-test seeding:</b> every test seeds users and stories via <c>SeedUserAsync</c> /
/// <c>SeedStoryAsync</c>; Respawn resets the DB between every test — see testing.md.
///
/// Tier: Integration (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class StoryLineageServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;
    private int _otherUserId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId    = await SeedUserAsync("author");
        _otherUserId = await SeedUserAsync("other");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // RequestLineageAsync
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RequestLineage_CrossAuthor_CreatesPendingAndNotifiesTarget()
    {
        int sourceStoryId = await SeedStoryAsync(authorId: _authorId);
        int targetStoryId = await SeedStoryAsync(authorId: _otherUserId);

        SetActiveUser(_authorId);
        await RequestLineageAsync(sourceStoryId, targetStoryId, 3); // 3 = Sequel

        StoryLineage? row = await GetRawLineageAsync(sourceStoryId, targetStoryId, 3);
        row.Should().NotBeNull();
        row!.StatusId.Should().Be(StoryLineageStatus.Pending);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool notified = await db.Notifications.AnyAsync(n =>
            n.RecipientUserId == _otherUserId
            && n.NotificationTypeId == NotificationTypeEnum.StoryLineageRequested
            && n.SourceUserId == _authorId
            && n.RelatedEntityId == sourceStoryId);
        notified.Should().BeTrue("the target author should be notified of the pending request");
    }

    [Fact]
    public async Task RequestLineage_SelfOwnedTarget_AutoApprovedNoNotification()
    {
        int sourceStoryId = await SeedStoryAsync(authorId: _authorId);
        int targetStoryId = await SeedStoryAsync(authorId: _authorId); // same author

        SetActiveUser(_authorId);
        await RequestLineageAsync(sourceStoryId, targetStoryId, 1); // 1 = Inspired By

        StoryLineage? row = await GetRawLineageAsync(sourceStoryId, targetStoryId, 1);
        row!.StatusId.Should().Be(StoryLineageStatus.Approved, "self-owned links auto-approve");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool anyNotif = await db.Notifications.AnyAsync(n =>
            n.NotificationTypeId == NotificationTypeEnum.StoryLineageRequested
            || n.NotificationTypeId == NotificationTypeEnum.StoryLineageApproved);
        anyNotif.Should().BeFalse("matches the notification drop-self invariant — no self-notification");
    }

    [Fact]
    public async Task RequestLineage_SourceNotOwnedByCaller_ThrowsUnauthorizedAccess()
    {
        int sourceStoryId = await SeedStoryAsync(authorId: _otherUserId); // not the caller's
        int targetStoryId = await SeedStoryAsync(authorId: _authorId);

        SetActiveUser(_authorId);
        Func<Task> act = () => RequestLineageAsync(sourceStoryId, targetStoryId, 1);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RequestLineage_SelfReferential_ThrowsValidation()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);

        SetActiveUser(_authorId);
        Func<Task> act = () => RequestLineageAsync(storyId, storyId, 1);
        await act.Should().ThrowAsync<StoryLineageValidationException>();
    }

    [Fact]
    public async Task RequestLineage_UnknownTargetStory_ThrowsValidation()
    {
        int sourceStoryId = await SeedStoryAsync(authorId: _authorId);

        SetActiveUser(_authorId);
        Func<Task> act = () => RequestLineageAsync(sourceStoryId, targetStoryId: 999_999_999, 1);
        await act.Should().ThrowAsync<StoryLineageValidationException>();
    }

    [Fact]
    public async Task RequestLineage_UnknownType_ThrowsValidation()
    {
        int sourceStoryId = await SeedStoryAsync(authorId: _authorId);
        int targetStoryId = await SeedStoryAsync(authorId: _otherUserId);

        SetActiveUser(_authorId);
        Func<Task> act = () => RequestLineageAsync(sourceStoryId, targetStoryId, typeId: 999);
        await act.Should().ThrowAsync<StoryLineageValidationException>();
    }

    [Fact]
    public async Task RequestLineage_DuplicateActiveLink_ThrowsValidation()
    {
        int sourceStoryId = await SeedStoryAsync(authorId: _authorId);
        int targetStoryId = await SeedStoryAsync(authorId: _otherUserId);

        SetActiveUser(_authorId);
        await RequestLineageAsync(sourceStoryId, targetStoryId, 2);

        Func<Task> act = () => RequestLineageAsync(sourceStoryId, targetStoryId, 2);
        await act.Should().ThrowAsync<StoryLineageValidationException>();
    }

    [Fact]
    public async Task RequestLineage_ReRequestAfterRejection_ReusesRowAsPending()
    {
        int sourceStoryId = await SeedStoryAsync(authorId: _authorId);
        int targetStoryId = await SeedStoryAsync(authorId: _otherUserId);

        SetActiveUser(_authorId);
        await RequestLineageAsync(sourceStoryId, targetStoryId, 4);

        SetActiveUser(_otherUserId);
        await RejectLineageAsync(sourceStoryId, targetStoryId, 4);

        SetActiveUser(_authorId);
        await RequestLineageAsync(sourceStoryId, targetStoryId, 4); // re-request, same triple

        StoryLineage? row = await GetRawLineageAsync(sourceStoryId, targetStoryId, 4);
        row.Should().NotBeNull("composite PK reused, not duplicate-inserted");
        row!.StatusId.Should().Be(StoryLineageStatus.Pending);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        int rowCount = await db.StoryLineages.CountAsync(l =>
            l.SourceStoryId == sourceStoryId && l.TargetStoryId == targetStoryId && l.RelationshipTypeId == 4);
        rowCount.Should().Be(1);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ApproveLineageAsync / RejectLineageAsync
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApproveLineage_TargetOwner_FlipsToApprovedAndNotifiesSource()
    {
        int sourceStoryId = await SeedStoryAsync(authorId: _authorId);
        int targetStoryId = await SeedStoryAsync(authorId: _otherUserId);

        SetActiveUser(_authorId);
        await RequestLineageAsync(sourceStoryId, targetStoryId, 3);

        SetActiveUser(_otherUserId);
        await ApproveLineageAsync(sourceStoryId, targetStoryId, 3);

        StoryLineage? row = await GetRawLineageAsync(sourceStoryId, targetStoryId, 3);
        row!.StatusId.Should().Be(StoryLineageStatus.Approved);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool notified = await db.Notifications.AnyAsync(n =>
            n.RecipientUserId == _authorId
            && n.NotificationTypeId == NotificationTypeEnum.StoryLineageApproved
            && n.SourceUserId == _otherUserId
            && n.RelatedEntityId == targetStoryId);
        notified.Should().BeTrue("the source author should be notified of the approval");
    }

    [Fact]
    public async Task ApproveLineage_NonTargetOwner_ThrowsUnauthorizedAccess()
    {
        int sourceStoryId = await SeedStoryAsync(authorId: _authorId);
        int targetStoryId = await SeedStoryAsync(authorId: _otherUserId);

        SetActiveUser(_authorId);
        await RequestLineageAsync(sourceStoryId, targetStoryId, 3);

        // Still the source author — not the target owner.
        Func<Task> act = () => ApproveLineageAsync(sourceStoryId, targetStoryId, 3);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ApproveLineage_UnknownLink_ThrowsKeyNotFound()
    {
        int sourceStoryId = await SeedStoryAsync(authorId: _authorId);
        int targetStoryId = await SeedStoryAsync(authorId: _otherUserId);

        SetActiveUser(_otherUserId);
        Func<Task> act = () => ApproveLineageAsync(sourceStoryId, targetStoryId, 1);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task RejectLineage_TargetOwner_FlipsToRejectedNoNotification()
    {
        int sourceStoryId = await SeedStoryAsync(authorId: _authorId);
        int targetStoryId = await SeedStoryAsync(authorId: _otherUserId);

        SetActiveUser(_authorId);
        await RequestLineageAsync(sourceStoryId, targetStoryId, 2);

        SetActiveUser(_otherUserId);
        await RejectLineageAsync(sourceStoryId, targetStoryId, 2);

        StoryLineage? row = await GetRawLineageAsync(sourceStoryId, targetStoryId, 2);
        row!.StatusId.Should().Be(StoryLineageStatus.Rejected, "kept as a row, not deleted");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool anyApprovalNotif = await db.Notifications.AnyAsync(n =>
            n.NotificationTypeId == NotificationTypeEnum.StoryLineageApproved);
        anyApprovalNotif.Should().BeFalse("rejection is silent — no notification");
    }

    [Fact]
    public async Task RejectLineage_NonTargetOwner_ThrowsUnauthorizedAccess()
    {
        int sourceStoryId = await SeedStoryAsync(authorId: _authorId);
        int targetStoryId = await SeedStoryAsync(authorId: _otherUserId);

        SetActiveUser(_authorId);
        await RequestLineageAsync(sourceStoryId, targetStoryId, 2);

        Func<Task> act = () => RejectLineageAsync(sourceStoryId, targetStoryId, 2);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // DeleteLineageAsync
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteLineage_SourceOwner_RemovesRow()
    {
        int sourceStoryId = await SeedStoryAsync(authorId: _authorId);
        int targetStoryId = await SeedStoryAsync(authorId: _authorId);

        SetActiveUser(_authorId);
        await RequestLineageAsync(sourceStoryId, targetStoryId, 1); // self-owned, auto-approved
        await DeleteLineageAsync(sourceStoryId, targetStoryId, 1);

        (await GetRawLineageAsync(sourceStoryId, targetStoryId, 1)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteLineage_NonSourceOwner_ThrowsUnauthorizedAccess()
    {
        int sourceStoryId = await SeedStoryAsync(authorId: _authorId);
        int targetStoryId = await SeedStoryAsync(authorId: _otherUserId);

        SetActiveUser(_authorId);
        await RequestLineageAsync(sourceStoryId, targetStoryId, 2);

        SetActiveUser(_otherUserId); // owns the target, not the source
        Func<Task> act = () => DeleteLineageAsync(sourceStoryId, targetStoryId, 2);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task DeleteLineage_NonExistent_NoThrow()
    {
        int sourceStoryId = await SeedStoryAsync(authorId: _authorId);
        int targetStoryId = await SeedStoryAsync(authorId: _otherUserId);

        SetActiveUser(_authorId);
        Func<Task> act = () => DeleteLineageAsync(sourceStoryId, targetStoryId, 1);
        await act.Should().NotThrowAsync("idempotent — a no-op if the link doesn't exist");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Cascade delete
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeletingSourceStory_CascadesLineageRow()
    {
        int sourceStoryId = await SeedStoryAsync(authorId: _authorId);
        int targetStoryId = await SeedStoryAsync(authorId: _authorId);

        SetActiveUser(_authorId);
        await RequestLineageAsync(sourceStoryId, targetStoryId, 1);

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Story story = await db.Stories.SingleAsync(s => s.StoryId == sourceStoryId);
            db.Stories.Remove(story);
            await db.SaveChangesAsync();
        }

        (await GetRawLineageAsync(sourceStoryId, targetStoryId, 1)).Should().BeNull();
    }

    [Fact]
    public async Task DeletingTargetStory_CascadesLineageRow()
    {
        int sourceStoryId = await SeedStoryAsync(authorId: _authorId);
        int targetStoryId = await SeedStoryAsync(authorId: _authorId);

        SetActiveUser(_authorId);
        await RequestLineageAsync(sourceStoryId, targetStoryId, 1);

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Story story = await db.Stories.SingleAsync(s => s.StoryId == targetStoryId);
            db.Stories.Remove(story);
            await db.SaveChangesAsync();
        }

        (await GetRawLineageAsync(sourceStoryId, targetStoryId, 1)).Should().BeNull();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // GetLineageForStoryAsync — public display
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLineageForStory_ReturnsOnlyApprovedOutgoing()
    {
        int sourceStoryId = await SeedStoryAsync(authorId: _authorId);
        int approvedTargetId = await SeedStoryAsync(authorId: _authorId);
        int pendingTargetId = await SeedStoryAsync(authorId: _otherUserId);

        SetActiveUser(_authorId);
        await RequestLineageAsync(sourceStoryId, approvedTargetId, 1); // self-owned → auto-Approved
        await RequestLineageAsync(sourceStoryId, pendingTargetId, 2);  // cross-author → Pending

        IReadOnlyList<StoryLineageDto> links = await GetLineageForStoryAsync(sourceStoryId);

        links.Should().ContainSingle();
        links[0].TargetStoryId.Should().Be(approvedTargetId);
    }

    [Fact]
    public async Task GetLineageForStory_DoesNotReturnIncomingLinks()
    {
        // storyId is the TARGET of an approved link — must not appear as "its own" outgoing lineage.
        int sourceStoryId = await SeedStoryAsync(authorId: _authorId);
        int targetStoryId = await SeedStoryAsync(authorId: _authorId);

        SetActiveUser(_authorId);
        await RequestLineageAsync(sourceStoryId, targetStoryId, 1);

        IReadOnlyList<StoryLineageDto> links = await GetLineageForStoryAsync(targetStoryId);
        links.Should().BeEmpty("one-way display — absence of a reverse row means don't show on the target");
    }

    [Fact]
    public async Task GetLineageForStory_MatureTargetHiddenFromMatureDisabledViewer_ExcludedFromDisplay()
    {
        // Mirrors the Series Feature 9 mature-drop rule: a link is only ever returned when its
        // target survives the viewer's ContentRating filter (the join-not-bare-projection rule).
        int sourceStoryId = await SeedStoryAsync(authorId: _authorId);
        int matureTargetId = await SeedStoryAsync(authorId: _authorId, rating: Rating.M);

        SetActiveUser(_authorId);
        await RequestLineageAsync(sourceStoryId, matureTargetId, 3);

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_otherUserId, showMatureContent: false));
        IReadOnlyList<StoryLineageDto> links = await GetLineageForStoryAsync(sourceStoryId);
        links.Should().BeEmpty("the Mature target must not display for a viewer who has mature content off");
    }

    [Fact]
    public async Task GetLineageForStory_MatureTargetVisible_WhenViewerShowsMatureContent()
    {
        int sourceStoryId = await SeedStoryAsync(authorId: _authorId);
        int matureTargetId = await SeedStoryAsync(authorId: _authorId, rating: Rating.M);

        SetActiveUser(_authorId);
        await RequestLineageAsync(sourceStoryId, matureTargetId, 3);

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_otherUserId, showMatureContent: true));
        IReadOnlyList<StoryLineageDto> links = await GetLineageForStoryAsync(sourceStoryId);
        links.Should().ContainSingle(l => l.TargetStoryId == matureTargetId);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // GetManageDataForUserAsync — owner-wide management page
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetManageData_Outgoing_IncludesAllStatuses()
    {
        int sourceStoryId = await SeedStoryAsync(authorId: _authorId);
        int approvedTargetId = await SeedStoryAsync(authorId: _authorId);
        int pendingTargetId = await SeedStoryAsync(authorId: _otherUserId);
        int rejectedTargetId = await SeedStoryAsync(authorId: _otherUserId);

        SetActiveUser(_authorId);
        await RequestLineageAsync(sourceStoryId, approvedTargetId, 1);
        await RequestLineageAsync(sourceStoryId, pendingTargetId, 2);
        await RequestLineageAsync(sourceStoryId, rejectedTargetId, 3);
        SetActiveUser(_otherUserId);
        await RejectLineageAsync(sourceStoryId, rejectedTargetId, 3);

        SetActiveUser(_authorId);
        StoryLineageManageDto manage = await GetManageDataAsync();

        manage.Outgoing.Should().HaveCount(3);
        manage.Outgoing.Select(o => o.Status).Should().BeEquivalentTo(
            [StoryLineageStatus.Approved, StoryLineageStatus.Pending, StoryLineageStatus.Rejected]);
    }

    [Fact]
    public async Task GetManageData_Incoming_OnlyPendingTargetingCallersStories()
    {
        int sourceStoryId = await SeedStoryAsync(authorId: _otherUserId);
        int targetStoryId = await SeedStoryAsync(authorId: _authorId);
        int approvedTargetId = await SeedStoryAsync(authorId: _authorId);
        int approvedSourceId = await SeedStoryAsync(authorId: _otherUserId);

        SetActiveUser(_otherUserId);
        await RequestLineageAsync(sourceStoryId, targetStoryId, 1); // stays Pending
        await RequestLineageAsync(approvedSourceId, approvedTargetId, 2);

        SetActiveUser(_authorId);
        await ApproveLineageAsync(approvedSourceId, approvedTargetId, 2); // no longer Pending

        StoryLineageManageDto manage = await GetManageDataAsync();
        manage.IncomingRequests.Should().ContainSingle(r =>
            r.SourceStoryId == sourceStoryId && r.TargetStoryId == targetStoryId);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SearchStoriesByTitleAsync (WU42) — reusable typeahead search
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchStoriesByTitle_SubstringMatch_ReturnsMatchingStory()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        string title = await SetStoryTitleAsync(storyId, "The Ashen Skies of Kanto");

        IReadOnlyList<StoryTitleSearchDto> results = await SearchStoriesByTitleAsync("ashen");
        results.Should().ContainSingle(r => r.StoryId == storyId && r.Title == title);
    }

    [Fact]
    public async Task SearchStoriesByTitle_EmptyTerm_ReturnsEmpty()
    {
        await SeedStoryAsync(authorId: _authorId);
        (await SearchStoriesByTitleAsync("")).Should().BeEmpty();
    }

    [Fact]
    public async Task SearchStoriesByTitle_NoMatch_ReturnsEmpty()
    {
        await SeedStoryAsync(authorId: _authorId);
        (await SearchStoriesByTitleAsync("zzz-no-such-title-zzz")).Should().BeEmpty();
    }

    [Fact]
    public async Task SearchStoriesByTitle_RespectsContentRatingFilter()
    {
        int matureId = await SeedStoryAsync(authorId: _authorId, rating: Rating.M);
        string title = await SetStoryTitleAsync(matureId, "Mature Search Target Xyzzy");

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_otherUserId, showMatureContent: false));
        (await SearchStoriesByTitleAsync("Xyzzy")).Should().BeEmpty(
            "mature story must not surface for a viewer with mature content off");

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_otherUserId, showMatureContent: true));
        (await SearchStoriesByTitleAsync("Xyzzy")).Should().ContainSingle(r => r.StoryId == matureId && r.Title == title);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Private helpers — service calls
    // ─────────────────────────────────────────────────────────────────────────────

    private async Task RequestLineageAsync(int sourceStoryId, int targetStoryId, short typeId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryLineageWriteService svc = scope.ServiceProvider.GetRequiredService<IStoryLineageWriteService>();
        await svc.RequestLineageAsync(new CreateStoryLineageDto
        {
            SourceStoryId = sourceStoryId,
            TargetStoryId = targetStoryId,
            TypeId        = typeId
        });
    }

    private async Task ApproveLineageAsync(int sourceStoryId, int targetStoryId, short typeId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryLineageWriteService svc = scope.ServiceProvider.GetRequiredService<IStoryLineageWriteService>();
        await svc.ApproveLineageAsync(sourceStoryId, targetStoryId, typeId);
    }

    private async Task RejectLineageAsync(int sourceStoryId, int targetStoryId, short typeId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryLineageWriteService svc = scope.ServiceProvider.GetRequiredService<IStoryLineageWriteService>();
        await svc.RejectLineageAsync(sourceStoryId, targetStoryId, typeId);
    }

    private async Task DeleteLineageAsync(int sourceStoryId, int targetStoryId, short typeId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryLineageWriteService svc = scope.ServiceProvider.GetRequiredService<IStoryLineageWriteService>();
        await svc.DeleteLineageAsync(sourceStoryId, targetStoryId, typeId);
    }

    private async Task<IReadOnlyList<StoryLineageDto>> GetLineageForStoryAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryLineageReadService svc = scope.ServiceProvider.GetRequiredService<IStoryLineageReadService>();
        return await svc.GetLineageForStoryAsync(storyId);
    }

    private async Task<StoryLineageManageDto> GetManageDataAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryLineageReadService svc = scope.ServiceProvider.GetRequiredService<IStoryLineageReadService>();
        return await svc.GetManageDataForUserAsync();
    }

    private async Task<IReadOnlyList<StoryTitleSearchDto>> SearchStoriesByTitleAsync(string term)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryReadService svc = scope.ServiceProvider.GetRequiredService<IStoryReadService>();
        return await svc.SearchStoriesByTitleAsync(term);
    }

    /// <summary>Raw ground-truth read, bypassing the service (so tests can assert the exact row
    /// state EF Core sees, independent of the read service's own projections).</summary>
    private async Task<StoryLineage?> GetRawLineageAsync(int sourceStoryId, int targetStoryId, short typeId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.StoryLineages.AsNoTracking().FirstOrDefaultAsync(l =>
            l.SourceStoryId == sourceStoryId && l.TargetStoryId == targetStoryId && l.RelationshipTypeId == typeId);
    }

    /// <summary>Overwrites a seeded story's title (SeedStoryAsync's default titles are randomized
    /// GUID suffixes, unsuitable for substring-search assertions) and returns the new title.</summary>
    private async Task<string> SetStoryTitleAsync(int storyId, string title)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        StoryListing listing = await db.StoryListings.SingleAsync(sl => sl.StoryId == storyId);
        listing.StoryTitle = title;
        await db.SaveChangesAsync();
        return title;
    }
}
