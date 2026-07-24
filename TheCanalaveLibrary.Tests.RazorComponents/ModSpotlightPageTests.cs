using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="ModSpotlightPage"/> (Feature 55, WU-Spotlight): remaining
/// capacity + settings values render; granting by exact username resolves and calls the
/// allocator; unknown username surfaces an inline error; revoke shown only for Available slots.
/// Tier: RazorComponents (bUnit — allocator/settings/lookup faked; role gating is the services'
/// concern, covered in Integration).
/// </summary>
public class ModSpotlightPageTests : BunitContext
{
    private readonly FakeAllocator _allocator = new();
    private readonly FakeSettings _settings = new();
    private readonly FakeUserLookup _lookup = new();

    public ModSpotlightPageTests()
    {
        Services.AddSingleton<ISpotlightSlotAllocator>(_allocator);
        Services.AddSingleton<ISiteSettingsWriteService>(_settings);
        Services.AddSingleton<IMessagingReadService>(_lookup);
        Services.AddSingleton<IToastService, ToastService>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Renders_CapacityAndSettings()
    {
        _allocator.Remaining = 7;
        _settings.Values[SiteSettingKeys.SpotlightPositionCount] = 4;

        IRenderedComponent<ModSpotlightPage> cut = Render<ModSpotlightPage>();

        cut.Markup.Should().Contain("7");
        cut.Find("input[type=number]").Should().NotBeNull();
        cut.Markup.Should().Contain("Homepage positions");
    }

    [Fact]
    public async Task Grant_KnownUsername_CallsAllocator()
    {
        _lookup.User = new MessagingParticipantDto(42, "Sponsor", "/avatar.png");

        IRenderedComponent<ModSpotlightPage> cut = Render<ModSpotlightPage>();
        await cut.Find("input[type=text]").ChangeAsync(new() { Value = "Sponsor" });
        await cut.FindAll("button").First(b => b.TextContent.Contains("Grant slot")).ClickAsync(new());

        _allocator.LastGrantedTo.Should().Be(42);
    }

    [Fact]
    public async Task Grant_UnknownUsername_ShowsInlineError_AndDoesNotGrant()
    {
        _lookup.User = null;

        IRenderedComponent<ModSpotlightPage> cut = Render<ModSpotlightPage>();
        await cut.Find("input[type=text]").ChangeAsync(new() { Value = "Nobody" });
        await cut.FindAll("button").First(b => b.TextContent.Contains("Grant slot")).ClickAsync(new());

        cut.Markup.Should().Contain("was found");
        _allocator.LastGrantedTo.Should().BeNull();
    }

    [Fact]
    public void RecentGrants_RevokeOnlyForAvailable()
    {
        _allocator.Recent =
        [
            new SpotlightSlotAdminDto(1, 42, "HolderA", SpotlightSlotSource.ModAward, SpotlightSlotStatus.Available, new DateTime(2026, 7, 1)),
            new SpotlightSlotAdminDto(2, 43, "HolderB", SpotlightSlotSource.ModAward, SpotlightSlotStatus.Redeemed, new DateTime(2026, 7, 2)),
        ];

        IRenderedComponent<ModSpotlightPage> cut = Render<ModSpotlightPage>();

        cut.Markup.Should().Contain("HolderA").And.Contain("HolderB");
        cut.FindAll("button").Count(b => b.TextContent.Contains("Revoke"))
            .Should().Be(1, "only unredeemed (Available) grants can be revoked");
    }

    // ── Fakes ─────────────────────────────────────────────────────────────────────

    private sealed class FakeAllocator : ISpotlightSlotAllocator
    {
        public int Remaining { get; set; } = 12;
        public IReadOnlyList<SpotlightSlotAdminDto> Recent { get; set; } = [];
        public int? LastGrantedTo { get; private set; }

        public Task<int> GrantSlotAsync(int toUserId, SpotlightSlotSource source, Rating maxStoryRating = Rating.E)
        {
            LastGrantedTo = toUserId;
            return Task.FromResult(1);
        }

        public Task RevokeSlotAsync(int slotId) => Task.CompletedTask;
        public Task<int> GetRemainingMonthlyGrantCapacityAsync() => Task.FromResult(Remaining);
        public Task<IReadOnlyList<SpotlightSlotAdminDto>> GetRecentGrantsAsync(int take = 50) => Task.FromResult(Recent);
    }

    private sealed class FakeSettings : ISiteSettingsWriteService
    {
        public Dictionary<string, int> Values { get; } = [];

        public Task<int> GetIntAsync(string settingKey, int fallback) =>
            Task.FromResult(Values.TryGetValue(settingKey, out int v) ? v : fallback);

        public Task SetIntAsync(string settingKey, int value)
        {
            Values[settingKey] = value;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserLookup : IMessagingReadService
    {
        public MessagingParticipantDto? User { get; set; }

        public Task<MessagingParticipantDto?> FindUserByUsernameAsync(string username) => Task.FromResult(User);

        // The mod page uses only the username lookup — everything else is unreachable here.
        public Task<IReadOnlyList<ConversationSummaryDto>> GetConversationsAsync(bool includeArchived = false) =>
            throw new NotSupportedException();
        public Task<ConversationThreadDto> GetConversationThreadAsync(int conversationId, int page, int pageSize) =>
            throw new NotSupportedException();
        public Task<int> GetUnreadConversationCountAsync() => Task.FromResult(0);
    }
}
