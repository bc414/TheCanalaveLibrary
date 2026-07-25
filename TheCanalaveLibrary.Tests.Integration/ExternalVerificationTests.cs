using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for Feature 53's two-tier verification workflow (WU39, settled 2026-07-24,
/// audit/Moderation.md F53): the account tier (<see cref="UserExternalIdentity"/>, confirmed once
/// per user × platform) and the per-link tier (existing <see cref="StoryExternalLink.VerificationStatus"/>,
/// gated on the account tier already being Verified). Tier: Integration (real Testcontainers
/// Postgres — the unique-index upsert semantics and the queue filters must be real).
/// </summary>
[Collection("Postgres")]
public class ExternalVerificationTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;
    private int _modId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId = await SeedUserAsync("Author");
        _modId = await SeedUserAsync("Mod");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<int> SeedExternalLinkAsync(
        int storyId, short platformId = 1,
        string url = "https://archiveofourown.org/works/999", DateTime? requestedAt = null)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var link = new StoryExternalLink
        {
            StoryId = storyId,
            ExternalPlatformId = platformId,
            Url = url,
            VerificationStatus = VerificationStatusEnum.Unverified,
            DateVerificationRequested = requestedAt
        };
        db.StoryExternalLinks.Add(link);
        await db.SaveChangesAsync();
        return link.StoryExternalLinkId;
    }

    private async Task SubmitAccountAsync(int userId, short platformId, string handle)
    {
        SetActiveUser(userId);
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IExternalVerificationWriteService svc = scope.ServiceProvider.GetRequiredService<IExternalVerificationWriteService>();
        await svc.SubmitAccountForVerificationAsync(new AddExternalAccountRequest(
            platformId, $"https://example-platform-{platformId}.test/users/{handle}", handle));
    }

    private async Task<int> GetIdentityIdAsync(int userId, short platformId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return db.UserExternalIdentities
            .Single(i => i.UserId == userId && i.ExternalPlatformId == platformId)
            .UserExternalIdentityId;
    }

    /// <summary>Submits and mod-approves an account in one call — the setup most link-tier tests need.</summary>
    private async Task VerifyAccountAsync(int userId, short platformId, string handle)
    {
        await SubmitAccountAsync(userId, platformId, handle);
        int identityId = await GetIdentityIdAsync(userId, platformId);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IExternalVerificationWriteService svc = scope.ServiceProvider.GetRequiredService<IExternalVerificationWriteService>();
        await svc.ApproveAccountVerificationAsync(identityId);
    }

    // ── Account tier ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task EnsureMyVerificationCodeAsync_CreatesOnce_IdempotentOnSecondCall()
    {
        SetActiveUser(_authorId);
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IExternalVerificationWriteService svc = scope.ServiceProvider.GetRequiredService<IExternalVerificationWriteService>();

        string first = await svc.EnsureMyVerificationCodeAsync();
        string second = await svc.EnsureMyVerificationCodeAsync();

        first.Should().StartWith("TCL-Verify-");
        second.Should().Be(first, "the code is lazily created once and reused — never regenerated");
    }

    [Fact]
    public async Task SubmitAccountForVerificationAsync_CreatesUnverified()
    {
        await SubmitAccountAsync(_authorId, 1, "gengarlover");

        SetActiveUser(_authorId);
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IExternalVerificationReadService svc = scope.ServiceProvider.GetRequiredService<IExternalVerificationReadService>();
        IReadOnlyList<ExternalAccountDto> accounts = await svc.GetMyExternalAccountsAsync();

        accounts.Should().ContainSingle(a =>
            a.ExternalPlatformId == 1 && a.Status == VerificationStatusEnum.Unverified && a.Handle == "gengarlover");
    }

    [Fact]
    public async Task SubmitAccountForVerificationAsync_UnsupportedPlatform_Throws()
    {
        // ExternalPlatformId 7 = "Other" — SupportsVerification = false (seeded, no placement surface).
        SetActiveUser(_authorId);
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IExternalVerificationWriteService svc = scope.ServiceProvider.GetRequiredService<IExternalVerificationWriteService>();

        Func<Task> act = () => svc.SubmitAccountForVerificationAsync(
            new AddExternalAccountRequest(7, "https://example.test/u/x", "x"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SubmitAccountForVerificationAsync_ResubmitAfterReject_ResetsToUnverifiedAndClearsReason()
    {
        await SubmitAccountAsync(_authorId, 1, "gengarlover");
        int identityId = await GetIdentityIdAsync(_authorId, 1);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await using AsyncServiceScope modScope = Factory.Services.CreateAsyncScope();
        IExternalVerificationWriteService modSvc = modScope.ServiceProvider.GetRequiredService<IExternalVerificationWriteService>();
        await modSvc.RejectAccountVerificationAsync(identityId, "Code not found on profile.");

        // Upsert — same (user, platform) pair, not a second row (unique index would reject a duplicate insert).
        await SubmitAccountAsync(_authorId, 1, "gengarlover");

        SetActiveUser(_authorId);
        await using AsyncServiceScope readScope = Factory.Services.CreateAsyncScope();
        IExternalVerificationReadService readSvc = readScope.ServiceProvider.GetRequiredService<IExternalVerificationReadService>();
        ExternalAccountDto account = (await readSvc.GetMyExternalAccountsAsync()).Single(a => a.ExternalPlatformId == 1);

        account.Status.Should().Be(VerificationStatusEnum.Unverified);
        account.RejectionReason.Should().BeNull("re-submitting clears the prior rejection reason");
    }

    [Fact]
    public async Task ApproveAccountVerificationAsync_FlipsStatus_StampsModAndDate_WritesNotification()
    {
        await SubmitAccountAsync(_authorId, 1, "gengarlover");
        int identityId = await GetIdentityIdAsync(_authorId, 1);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IExternalVerificationWriteService svc = scope.ServiceProvider.GetRequiredService<IExternalVerificationWriteService>();
        await svc.ApproveAccountVerificationAsync(identityId);

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserExternalIdentity identity = db.UserExternalIdentities.Single(i => i.UserExternalIdentityId == identityId);

        identity.VerificationStatus.Should().Be(VerificationStatusEnum.Verified);
        identity.ReviewedByModeratorUserId.Should().Be(_modId);
        identity.DateReviewed.Should().NotBeNull();

        db.Notifications.Should().ContainSingle(n =>
            n.RecipientUserId == _authorId && n.NotificationTypeId == NotificationTypeEnum.ExternalAccountVerified);
    }

    [Fact]
    public async Task RejectAccountVerificationAsync_FlipsStatus_RecordsReason_WritesNotification()
    {
        await SubmitAccountAsync(_authorId, 1, "gengarlover");
        int identityId = await GetIdentityIdAsync(_authorId, 1);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IExternalVerificationWriteService svc = scope.ServiceProvider.GetRequiredService<IExternalVerificationWriteService>();
        await svc.RejectAccountVerificationAsync(identityId, "Code not found on profile.");

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserExternalIdentity identity = db.UserExternalIdentities.Single(i => i.UserExternalIdentityId == identityId);

        identity.VerificationStatus.Should().Be(VerificationStatusEnum.Rejected);
        identity.RejectionReason.Should().Be("Code not found on profile.");

        db.Notifications.Should().ContainSingle(n =>
            n.RecipientUserId == _authorId && n.NotificationTypeId == NotificationTypeEnum.ExternalAccountRejected);
    }

    [Fact]
    public async Task AccountWrites_NonModerator_Throws()
    {
        await SubmitAccountAsync(_authorId, 1, "gengarlover");
        int identityId = await GetIdentityIdAsync(_authorId, 1);

        SetActiveUser(_authorId); // authenticated, not a moderator
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IExternalVerificationWriteService svc = scope.ServiceProvider.GetRequiredService<IExternalVerificationWriteService>();

        Func<Task> act = () => svc.ApproveAccountVerificationAsync(identityId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── Per-link tier ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RequestLinkVerificationAsync_WithoutVerifiedAccount_Throws()
    {
        int storyId = await SeedStoryAsync(_authorId);
        int linkId = await SeedExternalLinkAsync(storyId);

        SetActiveUser(_authorId);
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IExternalVerificationWriteService svc = scope.ServiceProvider.GetRequiredService<IExternalVerificationWriteService>();

        Func<Task> act = () => svc.RequestLinkVerificationAsync(linkId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*account first*", "the account tier must be Verified before a per-link request is meaningful");
    }

    [Fact]
    public async Task RequestLinkVerificationAsync_NonOwner_Throws()
    {
        int storyId = await SeedStoryAsync(_authorId);
        int linkId = await SeedExternalLinkAsync(storyId);
        int otherUserId = await SeedUserAsync("NotTheAuthor");

        SetActiveUser(otherUserId);
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IExternalVerificationWriteService svc = scope.ServiceProvider.GetRequiredService<IExternalVerificationWriteService>();

        Func<Task> act = () => svc.RequestLinkVerificationAsync(linkId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RequestLinkVerificationAsync_WithVerifiedAccount_SetsDateVerificationRequested()
    {
        int storyId = await SeedStoryAsync(_authorId);
        int linkId = await SeedExternalLinkAsync(storyId);
        await VerifyAccountAsync(_authorId, 1, "gengarlover");

        SetActiveUser(_authorId);
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IExternalVerificationWriteService svc = scope.ServiceProvider.GetRequiredService<IExternalVerificationWriteService>();
        await svc.RequestLinkVerificationAsync(linkId);

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.StoryExternalLinks.Single(l => l.StoryExternalLinkId == linkId)
            .DateVerificationRequested.Should().NotBeNull();
    }

    [Fact]
    public async Task ApproveLinkVerificationAsync_FlipsStatus_WritesNotification()
    {
        int storyId = await SeedStoryAsync(_authorId);
        int linkId = await SeedExternalLinkAsync(storyId, requestedAt: DateTime.UtcNow);
        await VerifyAccountAsync(_authorId, 1, "gengarlover");

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IExternalVerificationWriteService svc = scope.ServiceProvider.GetRequiredService<IExternalVerificationWriteService>();
        await svc.ApproveLinkVerificationAsync(linkId);

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.StoryExternalLinks.Single(l => l.StoryExternalLinkId == linkId)
            .VerificationStatus.Should().Be(VerificationStatusEnum.Verified);

        db.Notifications.Should().ContainSingle(n =>
            n.RecipientUserId == _authorId && n.NotificationTypeId == NotificationTypeEnum.ExternalLinkVerified
            && n.RelatedEntityId == storyId);
    }

    [Fact]
    public async Task RejectLinkVerificationAsync_FlipsStatus_RecordsReason_WritesNotification_LinkNotHidden()
    {
        int storyId = await SeedStoryAsync(_authorId);
        int linkId = await SeedExternalLinkAsync(storyId, requestedAt: DateTime.UtcNow);
        await VerifyAccountAsync(_authorId, 1, "gengarlover");

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IExternalVerificationWriteService svc = scope.ServiceProvider.GetRequiredService<IExternalVerificationWriteService>();
        await svc.RejectLinkVerificationAsync(linkId, "Listed author doesn't match.");

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        StoryExternalLink link = db.StoryExternalLinks.Single(l => l.StoryExternalLinkId == linkId);

        link.VerificationStatus.Should().Be(VerificationStatusEnum.Rejected);
        link.RejectionReason.Should().Be("Listed author doesn't match.");
        // Settled 2026-07-24: a rejected link is NOT deleted/hidden — it stays a plain public link.

        db.Notifications.Should().ContainSingle(n =>
            n.RecipientUserId == _authorId && n.NotificationTypeId == NotificationTypeEnum.ExternalLinkRejected);
    }

    // ── Moderator queues ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPendingAccountVerificationsAsync_OnlyReturnsUnverified()
    {
        await SubmitAccountAsync(_authorId, 1, "gengarlover"); // stays Unverified
        int otherUser = await SeedUserAsync("Other");
        await VerifyAccountAsync(otherUser, 2, "someone"); // submitted then approved — must not appear

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IExternalVerificationReadService svc = scope.ServiceProvider.GetRequiredService<IExternalVerificationReadService>();
        IReadOnlyList<PendingAccountVerificationDto> pending = await svc.GetPendingAccountVerificationsAsync();

        pending.Should().ContainSingle(p => p.UserId == _authorId);
    }

    [Fact]
    public async Task GetPendingLinkVerificationsAsync_ExcludesRequestedLinkOnUnverifiedAccount()
    {
        int storyId = await SeedStoryAsync(_authorId);
        await SeedExternalLinkAsync(storyId, requestedAt: DateTime.UtcNow);
        await SubmitAccountAsync(_authorId, 1, "gengarlover"); // requested but NOT yet Verified

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IExternalVerificationReadService svc = scope.ServiceProvider.GetRequiredService<IExternalVerificationReadService>();
        IReadOnlyList<PendingLinkVerificationDto> pending = await svc.GetPendingLinkVerificationsAsync();

        pending.Should().BeEmpty("no per-link queue item exists until the account tier is Verified for that platform");
    }

    [Fact]
    public async Task GetPendingLinkVerificationsAsync_IncludesLinkOnceAccountVerified()
    {
        int storyId = await SeedStoryAsync(_authorId);
        int linkId = await SeedExternalLinkAsync(storyId, requestedAt: DateTime.UtcNow);
        await VerifyAccountAsync(_authorId, 1, "gengarlover");

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IExternalVerificationReadService svc = scope.ServiceProvider.GetRequiredService<IExternalVerificationReadService>();
        IReadOnlyList<PendingLinkVerificationDto> pending = await svc.GetPendingLinkVerificationsAsync();

        pending.Should().ContainSingle(p => p.StoryExternalLinkId == linkId && p.AccountHandle == "gengarlover");
    }

    [Fact]
    public async Task GetPendingLinkVerificationsAsync_ExcludesNotYetRequestedLink()
    {
        int storyId = await SeedStoryAsync(_authorId);
        await SeedExternalLinkAsync(storyId); // never requested (DateVerificationRequested null)
        await VerifyAccountAsync(_authorId, 1, "gengarlover");

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IExternalVerificationReadService svc = scope.ServiceProvider.GetRequiredService<IExternalVerificationReadService>();
        IReadOnlyList<PendingLinkVerificationDto> pending = await svc.GetPendingLinkVerificationsAsync();

        pending.Should().BeEmpty();
    }

    // ── Story-page projection ────────────────────────────────────────────────────

    [Fact]
    public async Task StoryPageProjection_VerifiedLinkAndVerifiedAccount_SurfacesHandleAndProfileUrl()
    {
        int storyId = await SeedStoryAsync(_authorId);
        int linkId = await SeedExternalLinkAsync(storyId, requestedAt: DateTime.UtcNow);
        await VerifyAccountAsync(_authorId, 1, "gengarlover");

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        await using AsyncServiceScope modScope = Factory.Services.CreateAsyncScope();
        IExternalVerificationWriteService modSvc = modScope.ServiceProvider.GetRequiredService<IExternalVerificationWriteService>();
        await modSvc.ApproveLinkVerificationAsync(linkId);

        await using AsyncServiceScope readScope = Factory.Services.CreateAsyncScope();
        IStoryReadService storyRead = readScope.ServiceProvider.GetRequiredService<IStoryReadService>();
        StoryDetailsDTO? details = await storyRead.GetStoryByIdAsync(storyId);

        StoryExternalLinkDto dto = details!.ExternalLinks.Single();
        dto.IsReviewed.Should().BeTrue();
        dto.AuthorAccountHandle.Should().Be("gengarlover");
        dto.AuthorAccountProfileUrl.Should().NotBeNull();
    }

    [Fact]
    public async Task StoryPageProjection_UnreviewedLink_HasNullHandleAndProfileUrl()
    {
        int storyId = await SeedStoryAsync(_authorId);
        await SeedExternalLinkAsync(storyId);
        await VerifyAccountAsync(_authorId, 1, "gengarlover"); // account verified, but link never reviewed

        await using AsyncServiceScope readScope = Factory.Services.CreateAsyncScope();
        IStoryReadService storyRead = readScope.ServiceProvider.GetRequiredService<IStoryReadService>();
        StoryDetailsDTO? details = await storyRead.GetStoryByIdAsync(storyId);

        StoryExternalLinkDto dto = details!.ExternalLinks.Single();
        dto.IsReviewed.Should().BeFalse();
        dto.AuthorAccountHandle.Should().BeNull();
        dto.AuthorAccountProfileUrl.Should().BeNull();
    }
}
