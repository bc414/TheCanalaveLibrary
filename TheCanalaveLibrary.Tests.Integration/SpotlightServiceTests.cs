using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for the Community Spotlight cluster (Feature 55, WU-Spotlight):
/// grant seam (<see cref="ISpotlightSlotAllocator"/>), redemption validation + booking
/// (<see cref="ISpotlightWriteService"/>), homepage/active reads, block availability,
/// go-live sweep (<see cref="SpotlightGoLiveSweeper"/>), and FK behaviors.
/// Tier: Integration (real Testcontainers Postgres).
///
/// <para><b>site_settings note:</b> the table is deliberately NOT in Respawn's ignore list, so
/// every test starts with it empty and services run on the <see cref="SiteSettingKeys"/> Core
/// defaults (also exercising the fallback path). Tests needing a custom knob insert the row
/// directly via <see cref="SetSettingAsync"/>.</para>
///
/// <para><b>FK parents per test:</b> users via <c>SeedUserAsync</c>; stories via
/// <c>SeedStoryAsync</c>; recommendations via <see cref="SeedRecAsync"/> (needs story parent);
/// slots via the allocator under a moderator context (needs user parents); placements via
/// redemption (needs slot + story parents). Notification types are HasData-seeded and survive
/// Respawn.</para>
/// </summary>
[Collection("Postgres")]
public class SpotlightServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _modId;
    private int _sponsorId;
    private int _authorId;
    private int _storyId;

    private static readonly int DefaultDuration = SiteSettingKeys.SpotlightBlockDurationDaysDefault;

    /// <summary>Start of the block containing "now" — bookable immediately (partial window).</summary>
    private static DateTime CurrentBlockStart => SpotlightBlocks.FloorToBlockStart(DateTime.UtcNow, DefaultDuration);

    /// <summary>Start of the next block — a future booking (not yet live).</summary>
    private static DateTime NextBlockStart => CurrentBlockStart.AddDays(DefaultDuration);

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _modId = await SeedUserAsync("Mod");
        _sponsorId = await SeedUserAsync("Sponsor");
        _authorId = await SeedUserAsync("Author");
        _storyId = await SeedStoryAsync(_authorId);
    }

    // ── Grant seam ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GrantSlot_AsModerator_CreatesAvailableSlotAndNotifiesAwardee()
    {
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        int slotId = await CallAllocatorAsync(a => a.GrantSlotAsync(_sponsorId, SpotlightSlotSource.ModAward));

        SetActiveUser(_sponsorId);
        IReadOnlyList<SpotlightSlotDto> slots = await CallReadAsync(r => r.GetMyAvailableSlotsAsync());
        slots.Should().ContainSingle(s => s.SlotId == slotId);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Notifications.AnyAsync(n =>
                n.RecipientUserId == _sponsorId
                && n.NotificationTypeId == NotificationTypeEnum.SpotlightSlotGranted))
            .Should().BeTrue("the awardee is notified inline at grant time");
    }

    [Fact]
    public async Task GrantSlot_NonModerator_Throws()
    {
        SetActiveUser(_sponsorId);
        Func<Task> act = () => CallAllocatorAsync(a => a.GrantSlotAsync(_sponsorId, SpotlightSlotSource.ModAward));
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GrantSlot_DonationSource_ThrowsNotSupported()
    {
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        Func<Task> act = () => CallAllocatorAsync(a => a.GrantSlotAsync(_sponsorId, SpotlightSlotSource.Donation));
        await act.Should().ThrowAsync<NotSupportedException>("the donation pipeline is the deferred seam");
    }

    [Fact]
    public async Task GrantSlot_AtMonthlyCap_Rejects_AndRevokeFreesCapacity()
    {
        await SetSettingAsync(SiteSettingKeys.SpotlightMonthlyGrantCap, "2");
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));

        // The reset guarantees a clean count — call the service the natural number of times.
        int firstSlot = await CallAllocatorAsync(a => a.GrantSlotAsync(_sponsorId, SpotlightSlotSource.ModAward));
        await CallAllocatorAsync(a => a.GrantSlotAsync(_authorId, SpotlightSlotSource.ModAward));

        (await CallAllocatorAsync(a => a.GetRemainingMonthlyGrantCapacityAsync())).Should().Be(0);
        Func<Task> overCap = () => CallAllocatorAsync(a => a.GrantSlotAsync(_modId, SpotlightSlotSource.ModAward));
        await overCap.Should().ThrowAsync<SpotlightValidationException>().WithMessage("*cap*");

        // Revoking an unredeemed grant gives its capacity back.
        await CallAllocatorAsync(a => a.RevokeSlotAsync(firstSlot));
        (await CallAllocatorAsync(a => a.GetRemainingMonthlyGrantCapacityAsync())).Should().Be(1);
    }

    // ── Redemption: happy path + active read ─────────────────────────────────────

    [Fact]
    public async Task Redeem_CurrentBlock_BooksPlacement_AndAppearsOnHomepage()
    {
        int recId = await SeedRecAsync(_sponsorId, _storyId);
        int slotId = await GrantToSponsorAsync();

        SetActiveUser(_sponsorId);
        await CallWriteAsync(w => w.RedeemSlotAsync(new RedeemSpotlightSlotDto(slotId, _storyId, recId, CurrentBlockStart)));

        // Slot consumed.
        (await CallReadAsync(r => r.GetMyAvailableSlotsAsync())).Should().BeEmpty();

        // Booking listed for the sponsor.
        IReadOnlyList<SpotlightBookingDto> bookings = await CallReadAsync(r => r.GetMyBookingsAsync());
        bookings.Should().ContainSingle(b => b.StoryId == _storyId && b.HasRecommendation);

        // Current block started in the past → live right now, composed with story card + rec.
        SetActiveUser(FakeActiveUserContext.Anonymous());
        IReadOnlyList<SpotlightDisplayDto> active = await CallReadAsync(r => r.GetActiveSpotlightsAsync());
        SpotlightDisplayDto spotlight = active.Should().ContainSingle(s => s.Story.StoryId == _storyId).Subject;
        spotlight.Recommendation.Should().NotBeNull("the attached recommendation displays beside the story");
        spotlight.Recommendation!.RecommendationId.Should().Be(recId);
    }

    [Fact]
    public async Task Redeem_FutureBlock_NotActiveYet()
    {
        int slotId = await GrantToSponsorAsync();

        SetActiveUser(_sponsorId);
        await CallWriteAsync(w => w.RedeemSlotAsync(new RedeemSpotlightSlotDto(slotId, _storyId, null, NextBlockStart)));

        IReadOnlyList<SpotlightDisplayDto> active = await CallReadAsync(r => r.GetActiveSpotlightsAsync());
        active.Should().BeEmpty("the booked block hasn't opened yet");
    }

    // ── Redemption: eligibility rejections ────────────────────────────────────────

    [Fact]
    public async Task Redeem_OwnStory_Rejects()
    {
        int ownStoryId = await SeedStoryAsync(_sponsorId);
        int slotId = await GrantToSponsorAsync();

        SetActiveUser(_sponsorId);
        Func<Task> act = () => CallWriteAsync(w =>
            w.RedeemSlotAsync(new RedeemSpotlightSlotDto(slotId, ownStoryId, null, NextBlockStart)));
        await act.Should().ThrowAsync<SpotlightValidationException>().WithMessage("*own story*");
    }

    [Fact]
    public async Task Redeem_NonPublicStory_Rejects()
    {
        int draftStoryId = await SeedStoryAsync(_authorId);
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Stories.Where(s => s.StoryId == draftStoryId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.StoryStatusId, StoryStatusEnum.Draft));
        }

        int slotId = await GrantToSponsorAsync();
        SetActiveUser(_sponsorId);
        Func<Task> act = () => CallWriteAsync(w =>
            w.RedeemSlotAsync(new RedeemSpotlightSlotDto(slotId, draftStoryId, null, NextBlockStart)));
        await act.Should().ThrowAsync<SpotlightValidationException>().WithMessage("*publicly visible*");
    }

    [Fact]
    public async Task Redeem_RecommendationOfDifferentStory_Rejects()
    {
        int otherStoryId = await SeedStoryAsync(_authorId);
        int recOnOtherStory = await SeedRecAsync(_sponsorId, otherStoryId);
        int slotId = await GrantToSponsorAsync();

        SetActiveUser(_sponsorId);
        Func<Task> act = () => CallWriteAsync(w =>
            w.RedeemSlotAsync(new RedeemSpotlightSlotDto(slotId, _storyId, recOnOtherStory, NextBlockStart)));
        await act.Should().ThrowAsync<SpotlightValidationException>().WithMessage("*doesn't belong*");
    }

    [Fact]
    public async Task Redeem_SomeoneElsesRecommendation_IsAllowed()
    {
        // Self-STORY is banned; someone else's recommendation is explicitly fine (and so is the
        // sponsor's own — the rule settled 2026-07-11).
        int othersRecId = await SeedRecAsync(_authorId, _storyId);
        int slotId = await GrantToSponsorAsync();

        SetActiveUser(_sponsorId);
        await CallWriteAsync(w => w.RedeemSlotAsync(new RedeemSpotlightSlotDto(slotId, _storyId, othersRecId, NextBlockStart)));

        (await CallReadAsync(r => r.GetMyBookingsAsync()))
            .Should().ContainSingle(b => b.StoryId == _storyId && b.HasRecommendation);
    }

    [Fact]
    public async Task Redeem_OffGridStart_Rejects()
    {
        int slotId = await GrantToSponsorAsync();
        SetActiveUser(_sponsorId);
        Func<Task> act = () => CallWriteAsync(w =>
            w.RedeemSlotAsync(new RedeemSpotlightSlotDto(slotId, _storyId, null, NextBlockStart.AddHours(3))));
        await act.Should().ThrowAsync<SpotlightValidationException>().WithMessage("*grid*");
    }

    [Fact]
    public async Task Redeem_SomeoneElsesSlot_Rejects()
    {
        int slotId = await GrantToSponsorAsync();
        SetActiveUser(_authorId); // not the slot holder
        Func<Task> act = () => CallWriteAsync(w =>
            w.RedeemSlotAsync(new RedeemSpotlightSlotDto(slotId, _storyId, null, NextBlockStart)));
        await act.Should().ThrowAsync<SpotlightValidationException>().WithMessage("*not yours*");
    }

    [Fact]
    public async Task Redeem_StoryCooldown_RejectsAdjacentRebooking()
    {
        int slot1 = await GrantToSponsorAsync();
        int slot2 = await GrantToSponsorAsync();

        SetActiveUser(_sponsorId);
        await CallWriteAsync(w => w.RedeemSlotAsync(new RedeemSpotlightSlotDto(slot1, _storyId, null, CurrentBlockStart)));

        // Same story into the next block — well inside the 90-day default cooldown.
        Func<Task> act = () => CallWriteAsync(w =>
            w.RedeemSlotAsync(new RedeemSpotlightSlotDto(slot2, _storyId, null, NextBlockStart)));
        await act.Should().ThrowAsync<SpotlightValidationException>().WithMessage("*rest*");
    }

    // ── Redemption: capacity + race ───────────────────────────────────────────────

    [Fact]
    public async Task Redeem_FullBlock_Rejects()
    {
        await SetSettingAsync(SiteSettingKeys.SpotlightPositionCount, "1");
        int otherStoryId = await SeedStoryAsync(_authorId);
        int slot1 = await GrantToSponsorAsync();
        int slot2 = await GrantToSponsorAsync();

        SetActiveUser(_sponsorId);
        await CallWriteAsync(w => w.RedeemSlotAsync(new RedeemSpotlightSlotDto(slot1, _storyId, null, NextBlockStart)));

        Func<Task> act = () => CallWriteAsync(w =>
            w.RedeemSlotAsync(new RedeemSpotlightSlotDto(slot2, otherStoryId, null, NextBlockStart)));
        await act.Should().ThrowAsync<SpotlightValidationException>().WithMessage("*filled*");

        // The availability calendar agrees.
        IReadOnlyList<SpotlightBlockDto> blocks = await CallReadAsync(r => r.GetBlockAvailabilityAsync());
        blocks.Should().Contain(b => b.StartUtc == NextBlockStart && !b.HasOpening);
    }

    [Fact]
    public async Task Redeem_TwoRacersOneOpening_ExactlyOneWins()
    {
        await SetSettingAsync(SiteSettingKeys.SpotlightPositionCount, "1");
        int otherStoryId = await SeedStoryAsync(_authorId);
        int slot1 = await GrantToSponsorAsync();
        int slot2 = await GrantToSponsorAsync();

        // Same sponsor identity for both racers (the shared FakeActiveUserContext is
        // factory-global); distinct stories so only block capacity is contended.
        SetActiveUser(_sponsorId);
        Task t1 = CallWriteAsync(w => w.RedeemSlotAsync(new RedeemSpotlightSlotDto(slot1, _storyId, null, NextBlockStart)));
        Task t2 = CallWriteAsync(w => w.RedeemSlotAsync(new RedeemSpotlightSlotDto(slot2, otherStoryId, null, NextBlockStart)));

        Func<Task> both = () => Task.WhenAll(t1, t2);
        // Exactly one must fail with the block-full rejection — the advisory lock serializes
        // the count-then-insert.
        await both.Should().ThrowAsync<SpotlightValidationException>();
        new[] { t1.IsCompletedSuccessfully, t2.IsCompletedSuccessfully }
            .Count(ok => ok).Should().Be(1, "the advisory lock must let exactly one racer book the last opening");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.CommunitySpotlights.CountAsync(cs => cs.StartDate == NextBlockStart))
            .Should().Be(1);
    }

    // ── Go-live sweep ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sweep_LivePlacement_NotifiesAuthorAndRecommender_Once()
    {
        int recommenderId = await SeedUserAsync("Recommender");
        int recId = await SeedRecAsync(recommenderId, _storyId);
        int slotId = await GrantToSponsorAsync();

        SetActiveUser(_sponsorId);
        await CallWriteAsync(w => w.RedeemSlotAsync(new RedeemSpotlightSlotDto(slotId, _storyId, recId, CurrentBlockStart)));

        int stampedFirst = await RunSweepAsync();
        stampedFirst.Should().Be(1);

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Notifications.AnyAsync(n =>
                    n.RecipientUserId == _authorId && n.NotificationTypeId == NotificationTypeEnum.StorySpotlighted))
                .Should().BeTrue("the story author is notified at go-live");
            (await db.Notifications.AnyAsync(n =>
                    n.RecipientUserId == recommenderId && n.NotificationTypeId == NotificationTypeEnum.RecommendationSpotlighted))
                .Should().BeTrue("the attached recommendation's recommender is notified at go-live");
        }

        // Idempotent: the stamp keeps a second sweep from re-notifying.
        (await RunSweepAsync()).Should().Be(0, "GoLiveNotifiedUtc is the fires-once stamp");
    }

    [Fact]
    public async Task Sweep_FuturePlacement_NotTouched()
    {
        int slotId = await GrantToSponsorAsync();
        SetActiveUser(_sponsorId);
        await CallWriteAsync(w => w.RedeemSlotAsync(new RedeemSpotlightSlotDto(slotId, _storyId, null, NextBlockStart)));

        (await RunSweepAsync()).Should().Be(0, "the window hasn't opened yet");
    }

    // ── FK behaviors ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeletedRecommendation_BlanksPlacementRecHalf()
    {
        int recId = await SeedRecAsync(_authorId, _storyId);
        int slotId = await GrantToSponsorAsync();
        SetActiveUser(_sponsorId);
        await CallWriteAsync(w => w.RedeemSlotAsync(new RedeemSpotlightSlotDto(slotId, _storyId, recId, CurrentBlockStart)));

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Recommendations.Where(r => r.RecommendationId == recId).ExecuteDeleteAsync();
        }

        SetActiveUser(FakeActiveUserContext.Anonymous());
        IReadOnlyList<SpotlightDisplayDto> active = await CallReadAsync(r => r.GetActiveSpotlightsAsync());
        active.Should().ContainSingle(s => s.Story.StoryId == _storyId)
            .Which.Recommendation.Should().BeNull("RecommendationId is SetNull — the placement survives, rec half blank");
    }

    [Fact]
    public async Task DeletedStory_CascadesPlacement()
    {
        int slotId = await GrantToSponsorAsync();
        SetActiveUser(_sponsorId);
        await CallWriteAsync(w => w.RedeemSlotAsync(new RedeemSpotlightSlotDto(slotId, _storyId, null, CurrentBlockStart)));

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Stories.Where(s => s.StoryId == _storyId).ExecuteDeleteAsync();
            (await db.CommunitySpotlights.AnyAsync()).Should().BeFalse("StoryId is Cascade");
            // The consumed slot survives (Restrict is on the placement→slot side only).
            (await db.SpotlightSlots.AnyAsync(s => s.SlotId == slotId)).Should().BeTrue();
        }
    }

    // ── Site settings round trip ──────────────────────────────────────────────────

    [Fact]
    public async Task SiteSettings_SetInt_RoundTrips_AndNonModCannotWrite()
    {
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ISiteSettingsWriteService settings = scope.ServiceProvider.GetRequiredService<ISiteSettingsWriteService>();
            await settings.SetIntAsync(SiteSettingKeys.SpotlightPositionCount, 5);
            (await settings.GetIntAsync(SiteSettingKeys.SpotlightPositionCount, 3)).Should().Be(5);
            await settings.SetIntAsync(SiteSettingKeys.SpotlightPositionCount, 4); // upsert path
            (await settings.GetIntAsync(SiteSettingKeys.SpotlightPositionCount, 3)).Should().Be(4);
        }

        SetActiveUser(_sponsorId);
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ISiteSettingsWriteService settings = scope.ServiceProvider.GetRequiredService<ISiteSettingsWriteService>();
            Func<Task> act = () => settings.SetIntAsync(SiteSettingKeys.SpotlightPositionCount, 99);
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private async Task<int> GrantToSponsorAsync()
    {
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        return await CallAllocatorAsync(a => a.GrantSlotAsync(_sponsorId, SpotlightSlotSource.ModAward));
    }

    private async Task<T> CallAllocatorAsync<T>(Func<ISpotlightSlotAllocator, Task<T>> call)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await call(scope.ServiceProvider.GetRequiredService<ISpotlightSlotAllocator>());
    }

    private async Task CallAllocatorAsync(Func<ISpotlightSlotAllocator, Task> call)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await call(scope.ServiceProvider.GetRequiredService<ISpotlightSlotAllocator>());
    }

    private async Task<T> CallReadAsync<T>(Func<ISpotlightReadService, Task<T>> call)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await call(scope.ServiceProvider.GetRequiredService<ISpotlightReadService>());
    }

    private async Task CallWriteAsync(Func<ISpotlightWriteService, Task> call)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await call(scope.ServiceProvider.GetRequiredService<ISpotlightWriteService>());
    }

    private async Task<int> RunSweepAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SpotlightGoLiveSweeper>().SweepAsync();
    }

    private async Task<int> SeedRecAsync(int? recommenderId, int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Recommendation rec = new()
        {
            StoryId = storyId,
            RecommenderId = recommenderId,
            StatusId = (short)RecommendationStatusEnum.Approved,
            DatePosted = DateTime.UtcNow,
            RecommendationDetail = new RecommendationDetail { Text = new string('x', 500) }
        };
        db.Recommendations.Add(rec);
        await db.SaveChangesAsync();
        return rec.RecommendationId;
    }

    private async Task SetSettingAsync(string key, string value)
    {
        // Direct row insert — site_settings starts empty every test (not Respawn-ignored), so the
        // Core defaults apply unless a test plants a row.
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.SiteSettings.Add(new SiteSetting { SettingKey = key, Value = value });
        await db.SaveChangesAsync();
    }
}
