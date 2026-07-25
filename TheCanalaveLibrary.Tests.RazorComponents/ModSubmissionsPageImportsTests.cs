using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render + interaction tests for the <see cref="ModSubmissionsPage"/> Imports tab (Feature 53,
/// WU39): the two review queues (account tier, per-link tier), Approve/Reject callbacks firing
/// with the correct id, and reject requiring a reason — mirrors the existing Stories-tab idiom in
/// the same page. Role gating ([Authorize(Roles="Moderator,Admin")]) is the services' concern,
/// covered in Integration (ExternalVerificationEndpointsTests) — this tier fakes the services and
/// renders directly. Tier: RazorComponents (bUnit).
/// </summary>
public class ModSubmissionsPageImportsTests : BunitContext
{
    private readonly FakeExternalVerificationWriteService _verification = new();

    public ModSubmissionsPageImportsTests()
    {
        Services.AddSingleton<IModerationWriteService>(new FakeModerationWriteService());
        Services.AddSingleton<IExternalVerificationWriteService>(_verification);
    }

    private async Task<IRenderedComponent<ModSubmissionsPage>> RenderOnImportsTabAsync()
    {
        IRenderedComponent<ModSubmissionsPage> cut = Render<ModSubmissionsPage>();
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Imports").ClickAsync(new());
        return cut;
    }

    [Fact]
    public async Task ImportsTab_NoPendingItems_ShowsEmptyStates()
    {
        IRenderedComponent<ModSubmissionsPage> cut = await RenderOnImportsTabAsync();

        cut.Markup.Should().Contain("No pending account verifications.");
        cut.Markup.Should().Contain("No pending link verifications.");
    }

    [Fact]
    public async Task ImportsTab_RendersPendingAccount_WithCodeAndProfileLink()
    {
        _verification.PendingAccounts =
        [
            new PendingAccountVerificationDto(7, 3, "gengarlover", 1, "Archive of Our Own",
                "https://archiveofourown.org/users/gengarlover", "gengarlover", "TCL-Verify-ABC234", DateTime.UtcNow)
        ];

        IRenderedComponent<ModSubmissionsPage> cut = await RenderOnImportsTabAsync();

        cut.Markup.Should().Contain("gengarlover");
        cut.Markup.Should().Contain("TCL-Verify-ABC234");
        cut.Find("a[href='https://archiveofourown.org/users/gengarlover']").Should().NotBeNull();
    }

    [Fact]
    public async Task ImportsTab_ApproveAccount_CallsService_AndRemovesFromQueue()
    {
        _verification.PendingAccounts =
        [
            new PendingAccountVerificationDto(7, 3, "gengarlover", 1, "Archive of Our Own",
                "https://archiveofourown.org/users/gengarlover", "gengarlover", "TCL-Verify-ABC234", DateTime.UtcNow)
        ];

        IRenderedComponent<ModSubmissionsPage> cut = await RenderOnImportsTabAsync();
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Approve").ClickAsync(new());

        _verification.ApprovedAccountId.Should().Be(7);
        cut.Markup.Should().Contain("No pending account verifications.");
    }

    [Fact]
    public async Task ImportsTab_RejectAccount_EmptyReason_ShowsError_DoesNotCallService()
    {
        _verification.PendingAccounts =
        [
            new PendingAccountVerificationDto(7, 3, "gengarlover", 1, "Archive of Our Own",
                "https://archiveofourown.org/users/gengarlover", "gengarlover", "TCL-Verify-ABC234", DateTime.UtcNow)
        ];

        IRenderedComponent<ModSubmissionsPage> cut = await RenderOnImportsTabAsync();
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Reject").ClickAsync(new());
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Confirm reject").ClickAsync(new());

        cut.Markup.Should().Contain("A rejection reason is required.");
        _verification.RejectedAccount.Should().BeNull();
    }

    [Fact]
    public async Task ImportsTab_RejectAccount_WithReason_CallsService()
    {
        _verification.PendingAccounts =
        [
            new PendingAccountVerificationDto(7, 3, "gengarlover", 1, "Archive of Our Own",
                "https://archiveofourown.org/users/gengarlover", "gengarlover", "TCL-Verify-ABC234", DateTime.UtcNow)
        ];

        IRenderedComponent<ModSubmissionsPage> cut = await RenderOnImportsTabAsync();
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Reject").ClickAsync(new());
        await cut.Find("textarea").ChangeAsync(new() { Value = "Code not found on profile." });
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Confirm reject").ClickAsync(new());

        _verification.RejectedAccount.Should().Be((7, "Code not found on profile."));
    }

    [Fact]
    public async Task ImportsTab_RendersPendingLink_WithStoryAndAccountHandle()
    {
        _verification.PendingLinks =
        [
            new PendingLinkVerificationDto(42, 5, "Placed Story", "/story/5", 1, "Archive of Our Own",
                "https://archiveofourown.org/works/123", 3, "gengarlover", "gengarlover",
                "https://archiveofourown.org/users/gengarlover", DateTime.UtcNow)
        ];

        IRenderedComponent<ModSubmissionsPage> cut = await RenderOnImportsTabAsync();

        cut.Markup.Should().Contain("Placed Story");
        cut.Markup.Should().Contain("gengarlover");
        cut.Find("a[href='/story/5']").Should().NotBeNull();
        cut.Find("a[href='https://archiveofourown.org/works/123']").Should().NotBeNull();
    }

    [Fact]
    public async Task ImportsTab_ApproveLink_CallsService_AndRemovesFromQueue()
    {
        _verification.PendingLinks =
        [
            new PendingLinkVerificationDto(42, 5, "Placed Story", "/story/5", 1, "Archive of Our Own",
                "https://archiveofourown.org/works/123", 3, "gengarlover", "gengarlover",
                "https://archiveofourown.org/users/gengarlover", DateTime.UtcNow)
        ];

        IRenderedComponent<ModSubmissionsPage> cut = await RenderOnImportsTabAsync();
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Approve").ClickAsync(new());

        _verification.ApprovedLinkId.Should().Be(42);
        cut.Markup.Should().Contain("No pending link verifications.");
    }

    [Fact]
    public async Task ImportsTab_RejectLink_WithReason_CallsService()
    {
        _verification.PendingLinks =
        [
            new PendingLinkVerificationDto(42, 5, "Placed Story", "/story/5", 1, "Archive of Our Own",
                "https://archiveofourown.org/works/123", 3, "gengarlover", "gengarlover",
                "https://archiveofourown.org/users/gengarlover", DateTime.UtcNow)
        ];

        IRenderedComponent<ModSubmissionsPage> cut = await RenderOnImportsTabAsync();
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Reject").ClickAsync(new());
        await cut.Find("textarea").ChangeAsync(new() { Value = "Listed author doesn't match." });
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Confirm reject").ClickAsync(new());

        _verification.RejectedLink.Should().Be((42, "Listed author doesn't match."));
    }

    // ── Fake ──────────────────────────────────────────────────────────────────────

    private sealed class FakeExternalVerificationWriteService : IExternalVerificationWriteService
    {
        public List<PendingAccountVerificationDto> PendingAccounts { get; set; } = [];
        public List<PendingLinkVerificationDto> PendingLinks { get; set; } = [];

        public int? ApprovedAccountId { get; private set; }
        public (int Id, string Reason)? RejectedAccount { get; private set; }
        public int? ApprovedLinkId { get; private set; }
        public (int Id, string Reason)? RejectedLink { get; private set; }

        public Task<IReadOnlyList<VerificationPlatformDto>> GetVerificationPlatformsAsync() =>
            Task.FromResult((IReadOnlyList<VerificationPlatformDto>)Array.Empty<VerificationPlatformDto>());

        public Task<IReadOnlyList<ExternalAccountDto>> GetMyExternalAccountsAsync() =>
            Task.FromResult((IReadOnlyList<ExternalAccountDto>)Array.Empty<ExternalAccountDto>());

        public Task<IReadOnlyList<PendingAccountVerificationDto>> GetPendingAccountVerificationsAsync() =>
            Task.FromResult((IReadOnlyList<PendingAccountVerificationDto>)PendingAccounts);

        public Task<IReadOnlyList<PendingLinkVerificationDto>> GetPendingLinkVerificationsAsync() =>
            Task.FromResult((IReadOnlyList<PendingLinkVerificationDto>)PendingLinks);

        public Task<string> EnsureMyVerificationCodeAsync() =>
            throw new NotImplementedException("Not exercised by ModSubmissionsPage.");

        public Task SubmitAccountForVerificationAsync(AddExternalAccountRequest request) =>
            throw new NotImplementedException("Not exercised by ModSubmissionsPage.");

        public Task RequestLinkVerificationAsync(int storyExternalLinkId) =>
            throw new NotImplementedException("Not exercised by ModSubmissionsPage.");

        public Task ApproveAccountVerificationAsync(int userExternalIdentityId)
        {
            ApprovedAccountId = userExternalIdentityId;
            PendingAccounts = PendingAccounts.Where(a => a.UserExternalIdentityId != userExternalIdentityId).ToList();
            return Task.CompletedTask;
        }

        public Task RejectAccountVerificationAsync(int userExternalIdentityId, string reason)
        {
            RejectedAccount = (userExternalIdentityId, reason);
            PendingAccounts = PendingAccounts.Where(a => a.UserExternalIdentityId != userExternalIdentityId).ToList();
            return Task.CompletedTask;
        }

        public Task ApproveLinkVerificationAsync(int storyExternalLinkId)
        {
            ApprovedLinkId = storyExternalLinkId;
            PendingLinks = PendingLinks.Where(l => l.StoryExternalLinkId != storyExternalLinkId).ToList();
            return Task.CompletedTask;
        }

        public Task RejectLinkVerificationAsync(int storyExternalLinkId, string reason)
        {
            RejectedLink = (storyExternalLinkId, reason);
            PendingLinks = PendingLinks.Where(l => l.StoryExternalLinkId != storyExternalLinkId).ToList();
            return Task.CompletedTask;
        }
    }
}
