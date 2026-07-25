using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server read implementation of <see cref="IExternalVerificationReadService"/> (Feature 53,
/// WU39, settled 2026-07-24, audit/Moderation.md F53). The two moderator-queue reads are
/// elevated work-surface reads — like <c>ServerModerationReadService</c>, they are
/// M-content-agnostic (a mod sees every pending item regardless of their own content-rating
/// setting); the actual gate is endpoint/page authorization, not a role check in the read itself
/// (mirrors the existing moderation read surface).
/// </summary>
public class ServerExternalVerificationReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser) : IExternalVerificationReadService
{
    /// <summary>Exposed as a protected property so the derived write service can access the user context without double-capturing the constructor parameter (CS9107).</summary>
    protected IActiveUserContext ActiveUser { get; } = activeUser;

    public async Task<IReadOnlyList<VerificationPlatformDto>> GetVerificationPlatformsAsync()
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        return await readDb.ExternalPlatforms
            .Where(p => p.SupportsVerification)
            .OrderBy(p => p.ExternalPlatformId)
            .Select(p => new VerificationPlatformDto(p.ExternalPlatformId, p.Name, p.PlacementInstructions))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ExternalAccountDto>> GetMyExternalAccountsAsync()
    {
        if (ActiveUser.UserId is not int userId) return [];

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        return await readDb.UserExternalIdentities
            .Where(i => i.UserId == userId)
            .OrderBy(i => i.ExternalPlatformId)
            .Select(i => new ExternalAccountDto(
                i.ExternalPlatformId,
                i.ExternalPlatform.Name,
                i.ProfileUrl,
                i.Handle,
                i.VerificationStatus,
                i.RejectionReason))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<PendingAccountVerificationDto>> GetPendingAccountVerificationsAsync()
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        // elevated read: moderator work surface (M-content-agnostic) — endpoint/page
        // authorization is the actual gate, per content-safety.md "Moderator review surfaces
        // are work surfaces".
        return await readDb.UserExternalIdentities
            .Where(i => i.VerificationStatus == VerificationStatusEnum.Unverified)
            .OrderBy(i => i.DateRequested)
            .Select(i => new PendingAccountVerificationDto(
                i.UserExternalIdentityId,
                i.UserId,
                i.User.UserName ?? string.Empty,
                i.ExternalPlatformId,
                i.ExternalPlatform.Name,
                i.ProfileUrl,
                i.Handle,
                i.User.VerificationCode ?? string.Empty,
                i.DateRequested))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<PendingLinkVerificationDto>> GetPendingLinkVerificationsAsync()
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

        // elevated read: moderator work surface — no per-link item exists here until the story
        // author holds a Verified account-tier identity for that link's platform (settled
        // 2026-07-24, F53: the account tier is the binding proof; this is only the cheap
        // per-work identity comparison on top of it).
        return await readDb.StoryExternalLinks
            .Where(el => el.VerificationStatus == VerificationStatusEnum.Unverified
                && el.DateVerificationRequested != null
                && el.Story.Author != null
                && el.Story.Author.UserExternalIdentities.Any(i =>
                    i.ExternalPlatformId == el.ExternalPlatformId && i.VerificationStatus == VerificationStatusEnum.Verified))
            .OrderBy(el => el.DateVerificationRequested)
            .Select(el => new PendingLinkVerificationDto(
                el.StoryExternalLinkId,
                el.StoryId,
                el.Story.StoryListing != null ? el.Story.StoryListing.StoryTitle : string.Empty,
                "/story/" + el.StoryId,
                el.ExternalPlatformId,
                el.ExternalPlatform.Name,
                el.Url,
                el.Story.AuthorId!.Value,
                el.Story.Author!.UserName ?? string.Empty,
                el.Story.Author!.UserExternalIdentities
                    .Where(i => i.ExternalPlatformId == el.ExternalPlatformId && i.VerificationStatus == VerificationStatusEnum.Verified)
                    .Select(i => i.Handle).First(),
                el.Story.Author!.UserExternalIdentities
                    .Where(i => i.ExternalPlatformId == el.ExternalPlatformId && i.VerificationStatus == VerificationStatusEnum.Verified)
                    .Select(i => i.ProfileUrl).First(),
                el.DateVerificationRequested!.Value))
            .ToListAsync();
    }
}
