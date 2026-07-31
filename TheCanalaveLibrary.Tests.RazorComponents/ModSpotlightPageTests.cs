using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="ModSpotlightPage"/> (Feature 55, WU-Spotlight; retrofitted onto
/// <see cref="UserPicker"/> in WU-StatBadgeProducers, replacing the prior "exact username" text
/// input and its <c>IMessagingReadService</c> dependency): remaining capacity + settings values
/// render; the Grant button starts disabled with no recipient picked; revoke shown only for
/// Available slots.
///
/// <b>What is NOT tested here:</b> picking a user via <see cref="UserPicker"/> (keyboard input →
/// search → selection) requires JavaScript simulation that bUnit doesn't drive reliably — same
/// documented limitation as <c>StoryTitlePickerTests</c>. The full grant flow (pick → Grant slot →
/// allocator call) is covered by manual/live-browser verification; the search itself
/// (<c>IUserProfileReadService.SearchUsersByNameAsync</c>) and the allocator's
/// <c>GrantSlotAsync</c> are covered at the Integration tier.
/// Tier: RazorComponents (bUnit — allocator/settings/user-search faked; role gating is the
/// services' concern, covered in Integration).
/// </summary>
public class ModSpotlightPageTests : BunitContext
{
    private readonly FakeAllocator _allocator = new();
    private readonly FakeSettings _settings = new();
    private readonly FakeUserProfileReadService _userSearch = new();

    public ModSpotlightPageTests()
    {
        Services.AddSingleton<ISpotlightSlotAllocator>(_allocator);
        Services.AddSingleton<ISiteSettingsWriteService>(_settings);
        Services.AddSingleton<IUserProfileReadService>(_userSearch);
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
    public void Renders_UserPicker_ForGrantRecipient()
    {
        IRenderedComponent<ModSpotlightPage> cut = Render<ModSpotlightPage>();

        cut.Find("input[type=text]").GetAttribute("placeholder").Should().Be("Type a username...");
    }

    [Fact]
    public void GrantButton_StartsDisabled_WithNoRecipientPicked()
    {
        IRenderedComponent<ModSpotlightPage> cut = Render<ModSpotlightPage>();

        var grantButton = cut.FindAll("button").First(b => b.TextContent.Contains("Grant slot"));
        grantButton.HasAttribute("disabled").Should().BeTrue(
            "no recipient has been picked yet — the button must not allow a no-op grant call");
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

    // The page only calls SearchUsersByNameAsync (via UserPicker) — everything else unreachable here.
    private sealed class FakeUserProfileReadService : IUserProfileReadService
    {
        public Task<ProfileHeaderDto?> GetProfileHeaderAsync(int userId, bool includePrivate) =>
            throw new NotSupportedException();
        public Task<string?> GetProfileTextAsync(int userId) => throw new NotSupportedException();
        public Task<ProfileAccessState> GetProfileAccessStateAsync(int userId) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserCardDto>> SearchUsersByNameAsync(string term) =>
            Task.FromResult<IReadOnlyList<UserCardDto>>([]);
    }
}
