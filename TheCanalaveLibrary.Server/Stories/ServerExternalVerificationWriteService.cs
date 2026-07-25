using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server write implementation of <see cref="IExternalVerificationWriteService"/> (Feature 53,
/// WU39, settled 2026-07-24, audit/Moderation.md F53). Inherits the read path via primary-constructor
/// chaining (CQRS-lite with write-inherits-read).
///
/// <para><b>Two-tier verification, both manual — no server-side outbound HTTP, ever</b> (SSRF
/// surface + Cloudflare/FFN blocking risk; permanently deferred, not a future phase). A moderator
/// confirms both tiers by opening the URL in their own browser: the account tier confirms the
/// public <see cref="User.VerificationCode"/> is present on the external profile (once per user ×
/// platform, ever); the per-link tier confirms the linked story's listed author matches that
/// confirmed account (per link — platform work URLs don't name their author, so account-verified
/// alone doesn't prove any specific linked story is theirs).</para>
///
/// <para><b>Display-only — adds no gate.</b> Neither tier affects story approval or visibility
/// (Feature 48 untouched). A rejected link is never hidden (hiding reads as an accusation and
/// invites misdirected reports) — the author gets private feedback (status + reason +
/// notification) to fix and re-request; a moderator who suspects actual theft uses the existing
/// Feature 46 report / 48 takedown path by hand.</para>
///
/// <para><b>Notifications are best-effort</b> — every <c>NotifyXxx</c> call happens after the
/// primary <c>SaveChangesAsync</c> inside a <c>try/catch</c> that logs and swallows, mirroring
/// <c>ServerModerationWriteService</c>.</para>
/// </summary>
public class ServerExternalVerificationWriteService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    IWriteRateLimitService rateLimit,
    INotificationWriteService notifications,
    ILogger<ServerExternalVerificationWriteService> logger)
    : ServerExternalVerificationReadService(readDbFactory, activeUser), IExternalVerificationWriteService
{
    // ── Author — account tier ─────────────────────────────────────────────────────

    public async Task<string> EnsureMyVerificationCodeAsync()
    {
        if (ActiveUser.UserId is not int userId)
            throw new InvalidOperationException("Requires an authenticated user.");

        User user = await writeDb.Users.SingleAsync(u => u.Id == userId);
        if (user.VerificationCode is not null)
            return user.VerificationCode;

        // Collision space is ~1 billion (32^6) — check-then-use is simple and sufficient; the
        // filtered unique index on User.VerificationCode is the actual backstop against a race.
        string candidate;
        int attempt = 0;
        do
        {
            candidate = PublicVerificationCode.New();
            attempt++;
        } while (attempt < 5 && await writeDb.Users.AnyAsync(u => u.VerificationCode == candidate));

        user.VerificationCode = candidate;
        await writeDb.SaveChangesAsync();
        return candidate;
    }

    public async Task SubmitAccountForVerificationAsync(AddExternalAccountRequest request)
    {
        if (ActiveUser.UserId is not int userId)
            throw new InvalidOperationException("Requires an authenticated user.");

        rateLimit.EnsureAllowed(WriteActionKind.VerificationRequest, userId);

        if (!Uri.TryCreate(request.ProfileUrl, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("Profile URL must be an absolute http or https URL.");

        if (string.IsNullOrWhiteSpace(request.Handle))
            throw new InvalidOperationException("Handle is required.");

        ExternalPlatform platform = await writeDb.ExternalPlatforms
            .SingleAsync(p => p.ExternalPlatformId == request.ExternalPlatformId);
        if (!platform.SupportsVerification)
            throw new InvalidOperationException($"{platform.Name} does not support verification.");

        // The code must exist before the author is told to go place it.
        await EnsureMyVerificationCodeAsync();

        UserExternalIdentity? identity = await writeDb.UserExternalIdentities
            .SingleOrDefaultAsync(i => i.UserId == userId && i.ExternalPlatformId == request.ExternalPlatformId);

        if (identity is null)
        {
            identity = new UserExternalIdentity { UserId = userId, ExternalPlatformId = request.ExternalPlatformId };
            writeDb.UserExternalIdentities.Add(identity);
        }

        // Upsert to Unverified — a re-submit after Rejected clears the prior review, same as a
        // fresh submission (settled: one row per user × platform, resubmission just resets it).
        identity.ProfileUrl = request.ProfileUrl;
        identity.Handle = request.Handle.Trim();
        identity.VerificationStatus = VerificationStatusEnum.Unverified;
        identity.DateRequested = DateTime.UtcNow;
        identity.DateReviewed = null;
        identity.ReviewedByModeratorUserId = null;
        identity.RejectionReason = null;

        await writeDb.SaveChangesAsync();
    }

    // ── Author — per-link tier ────────────────────────────────────────────────────

    public async Task RequestLinkVerificationAsync(int storyExternalLinkId)
    {
        if (ActiveUser.UserId is not int userId)
            throw new InvalidOperationException("Requires an authenticated user.");

        StoryExternalLink link = await writeDb.StoryExternalLinks
            .Include(l => l.Story)
            .SingleAsync(l => l.StoryExternalLinkId == storyExternalLinkId);

        if (link.Story.AuthorId != userId)
            throw new UnauthorizedAccessException("You must be the author of this story.");

        bool accountVerified = await writeDb.UserExternalIdentities.AnyAsync(i =>
            i.UserId == userId && i.ExternalPlatformId == link.ExternalPlatformId &&
            i.VerificationStatus == VerificationStatusEnum.Verified);

        if (!accountVerified)
        {
            string platformName = await writeDb.ExternalPlatforms
                .Where(p => p.ExternalPlatformId == link.ExternalPlatformId)
                .Select(p => p.Name)
                .SingleAsync();
            throw new InvalidOperationException($"Verify your {platformName} account first.");
        }

        rateLimit.EnsureAllowed(WriteActionKind.VerificationRequest, userId);

        link.DateVerificationRequested = DateTime.UtcNow;
        link.RejectionReason = null;
        await writeDb.SaveChangesAsync();
    }

    // ── Moderator — account tier ──────────────────────────────────────────────────

    public async Task ApproveAccountVerificationAsync(int userExternalIdentityId)
    {
        int modId = RequireModerator();

        UserExternalIdentity identity = await writeDb.UserExternalIdentities
            .SingleAsync(i => i.UserExternalIdentityId == userExternalIdentityId);

        identity.VerificationStatus = VerificationStatusEnum.Verified;
        identity.DateReviewed = DateTime.UtcNow;
        identity.ReviewedByModeratorUserId = modId;
        identity.RejectionReason = null;

        await writeDb.SaveChangesAsync();

        try { await notifications.NotifyExternalAccountVerifiedAsync(identity.UserId, modId); }
        catch (Exception ex) { logger.LogWarning(ex, "ExternalAccountVerified notification failed for identity {Id}", userExternalIdentityId); }
    }

    public async Task RejectAccountVerificationAsync(int userExternalIdentityId, string reason)
    {
        int modId = RequireModerator();

        UserExternalIdentity identity = await writeDb.UserExternalIdentities
            .SingleAsync(i => i.UserExternalIdentityId == userExternalIdentityId);

        identity.VerificationStatus = VerificationStatusEnum.Rejected;
        identity.DateReviewed = DateTime.UtcNow;
        identity.ReviewedByModeratorUserId = modId;
        identity.RejectionReason = reason;

        await writeDb.SaveChangesAsync();

        try { await notifications.NotifyExternalAccountRejectedAsync(identity.UserId, modId); }
        catch (Exception ex) { logger.LogWarning(ex, "ExternalAccountRejected notification failed for identity {Id}", userExternalIdentityId); }
    }

    // ── Moderator — per-link tier ─────────────────────────────────────────────────

    public async Task ApproveLinkVerificationAsync(int storyExternalLinkId)
    {
        int modId = RequireModerator();

        StoryExternalLink link = await writeDb.StoryExternalLinks
            .Include(l => l.Story)
            .SingleAsync(l => l.StoryExternalLinkId == storyExternalLinkId);

        link.VerificationStatus = VerificationStatusEnum.Verified;
        link.RejectionReason = null;

        await writeDb.SaveChangesAsync();

        try
        {
            if (link.Story.AuthorId.HasValue)
                await notifications.NotifyExternalLinkVerifiedAsync(link.Story.AuthorId.Value, link.StoryId, modId);
        }
        catch (Exception ex) { logger.LogWarning(ex, "ExternalLinkVerified notification failed for link {Id}", storyExternalLinkId); }
    }

    public async Task RejectLinkVerificationAsync(int storyExternalLinkId, string reason)
    {
        int modId = RequireModerator();

        StoryExternalLink link = await writeDb.StoryExternalLinks
            .Include(l => l.Story)
            .SingleAsync(l => l.StoryExternalLinkId == storyExternalLinkId);

        link.VerificationStatus = VerificationStatusEnum.Rejected;
        link.RejectionReason = reason;

        await writeDb.SaveChangesAsync();

        try
        {
            if (link.Story.AuthorId.HasValue)
                await notifications.NotifyExternalLinkRejectedAsync(link.Story.AuthorId.Value, link.StoryId, modId);
        }
        catch (Exception ex) { logger.LogWarning(ex, "ExternalLinkRejected notification failed for link {Id}", storyExternalLinkId); }
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    /// <summary>Mirrors <c>ServerModerationWriteService.RequireModerator</c> exactly.</summary>
    private int RequireModerator()
    {
        if (ActiveUser.UserId is not int id)
            throw new InvalidOperationException("Moderator action requires an authenticated user.");
        if (!ActiveUser.IsModerator && !ActiveUser.IsAdmin)
            throw new UnauthorizedAccessException("Moderator action requires the Moderator or Admin role.");
        return id;
    }
}
