using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The mods-now implementation of the grant seam (<see cref="ISpotlightSlotAllocator"/> —
/// Feature 55, WU-Spotlight). When the donation/payment pipeline lands (deferred past beta,
/// <c>audit/Spotlight.md</c>), it becomes a second caller passing
/// <see cref="SpotlightSlotSource.Donation"/> + a <c>PaymentId</c>; nothing on the
/// redemption/display side changes.
/// </summary>
public class ServerSpotlightSlotAllocator(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    INotificationWriteService notifications,
    ISiteSettingsReadService siteSettings,
    ILogger<ServerSpotlightSlotAllocator> logger) : ISpotlightSlotAllocator
{
    public async Task<int> GrantSlotAsync(int toUserId, SpotlightSlotSource source, Rating maxStoryRating = Rating.E)
    {
        // Slot rating class is binary (dedicated M / non-M pools, WU-AccessGate): normalize any
        // non-M value to E so T never becomes a third pool by accident.
        if (maxStoryRating != Rating.M) maxStoryRating = Rating.E;

        if (source == SpotlightSlotSource.Donation)
            throw new NotSupportedException(
                "Donation-sourced slots are the deferred payment-pipeline seam — no grant path produces them yet.");

        int modId = RequireModerator();

        bool userExists = await writeDb.Users.AnyAsync(u => u.Id == toUserId);
        if (!userExists)
            throw new SpotlightValidationException("That user does not exist.");

        // Monthly cap — models "donations are capped for the month by actual site costs, so
        // slots stay meaningful and proportional." Revoked grants give their capacity back.
        int cap = await siteSettings.GetIntAsync(
            SiteSettingKeys.SpotlightMonthlyGrantCap, SiteSettingKeys.SpotlightMonthlyGrantCapDefault);
        int grantedThisMonth = await CountGrantsThisMonthAsync(writeDb);
        if (grantedThisMonth >= cap)
            throw new SpotlightValidationException(
                $"The monthly grant cap ({cap}) has been reached — no more slots this month.");

        var slot = new SpotlightSlot
        {
            GrantedToUserId = toUserId,
            GrantedByUserId = modId,
            Source = source,
            Status = SpotlightSlotStatus.Available,
            MaxStoryRating = maxStoryRating,
            GrantedUtc = DateTime.UtcNow
        };
        writeDb.SpotlightSlots.Add(slot);
        await writeDb.SaveChangesAsync();

        // Best-effort post-commit (standard notification pattern).
        try
        {
            await notifications.NotifySpotlightSlotGrantedAsync(toUserId, modId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SpotlightSlotGranted notification failed for user {UserId}, slot {SlotId}",
                toUserId, slot.SlotId);
        }

        return slot.SlotId;
    }

    public async Task RevokeSlotAsync(int slotId)
    {
        RequireModerator();

        SpotlightSlot? slot = await writeDb.SpotlightSlots.FirstOrDefaultAsync(s => s.SlotId == slotId);
        if (slot is null)
            throw new SpotlightValidationException("That slot does not exist.");
        if (slot.Status != SpotlightSlotStatus.Available)
            throw new SpotlightValidationException("Only unredeemed slots can be revoked.");

        slot.Status = SpotlightSlotStatus.Revoked;
        await writeDb.SaveChangesAsync();
    }

    public async Task<int> GetRemainingMonthlyGrantCapacityAsync()
    {
        int cap = await siteSettings.GetIntAsync(
            SiteSettingKeys.SpotlightMonthlyGrantCap, SiteSettingKeys.SpotlightMonthlyGrantCapDefault);

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        int granted = await CountGrantsThisMonthAsync(readDb);
        return Math.Max(0, cap - granted);
    }

    public async Task<IReadOnlyList<SpotlightSlotAdminDto>> GetRecentGrantsAsync(int take = 50)
    {
        RequireModerator();

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        return await readDb.SpotlightSlots
            .OrderByDescending(s => s.GrantedUtc)
            .Take(take)
            .Select(s => new SpotlightSlotAdminDto(
                s.SlotId,
                s.GrantedToUserId,
                s.GrantedToUser != null ? s.GrantedToUser.UserName : null,
                s.Source,
                s.Status,
                s.GrantedUtc,
                s.MaxStoryRating))
            .ToListAsync();
    }

    /// <summary>Non-revoked grants in the current UTC calendar month, on either context.</summary>
    private static Task<int> CountGrantsThisMonthAsync(DbContext context)
    {
        DateTime now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return context.Set<SpotlightSlot>()
            .CountAsync(s => s.GrantedUtc >= monthStart && s.Status != SpotlightSlotStatus.Revoked);
    }

    private int RequireModerator()
    {
        // IsInRole is literal — Admin does NOT inherit Moderator; accept both (IActiveUserContext doc).
        if (!activeUser.IsModerator && !activeUser.IsAdmin)
            throw new UnauthorizedAccessException("This operation requires a moderator.");
        return activeUser.UserId
               ?? throw new InvalidOperationException("Moderator context has no user id.");
    }
}
