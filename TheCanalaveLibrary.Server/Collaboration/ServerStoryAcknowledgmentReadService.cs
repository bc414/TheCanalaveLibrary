using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation for Story Acknowledgments (WU-StatBadgeProducers). Mirrors
/// <see cref="ServerStoryLineageReadService"/>'s shape (primary-ctor over the read-context factory,
/// protected members for the derived write service).
/// </summary>
public class ServerStoryAcknowledgmentReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser) : IStoryAcknowledgmentReadService
{
    /// <summary>Protected so the derived write service can access it without double-capturing the
    /// constructor parameter (CS9107/CS9124 — see layer2-services.md).</summary>
    protected IActiveUserContext ActiveUser { get; } = activeUser;

    protected IDbContextFactory<ReadOnlyApplicationDbContext> ReadDbFactory { get; } = readDbFactory;

    public async Task<IReadOnlyList<StoryAcknowledgmentDto>> GetAcknowledgmentsForStoryAsync(int storyId)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        return await readDb.StoryAcknowledgments
            .Where(sa => sa.StoryId == storyId && sa.StatusId == StoryAcknowledgmentStatus.Accepted)
            .OrderBy(sa => sa.AcknowledgmentRoleId)
            .ThenBy(sa => sa.AcknowledgedUser.UserName)
            .Select(sa => new StoryAcknowledgmentDto(
                sa.AcknowledgmentRoleId,
                sa.AcknowledgmentRole.RoleName,
                sa.AcknowledgedUserId,
                sa.AcknowledgedUser.UserName!))
            .ToListAsync();
    }

    public async Task<StoryAcknowledgmentManageDto> GetManageDataForUserAsync()
    {
        int userId = RequireAuthenticatedUser();
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // Elevated, owner-scoped read: ignore ContentRating/IsTakenDown so an author can always see
        // and manage credits on their own stories even if the story has since gone mature/taken-down
        // for them (management page, not a discovery surface — mirrors GetManageDataForUserAsync
        // for lineage).
        List<StoryAcknowledgmentOutgoingDto> outgoing = await (
            from sa in readDb.StoryAcknowledgments.IgnoreQueryFilters(["ContentRating", "IsTakenDown", "StoryStatus"])
            join story in readDb.Stories.IgnoreQueryFilters(["ContentRating", "IsTakenDown", "StoryStatus"])
                on sa.StoryId equals story.StoryId
            where story.AuthorId == userId
            orderby sa.DateAcknowledged descending
            select new StoryAcknowledgmentOutgoingDto(
                sa.StoryId,
                story.StoryListing != null ? story.StoryListing.StoryTitle : string.Empty,
                sa.AcknowledgedUserId,
                sa.AcknowledgedUser.UserName,
                sa.AcknowledgmentRoleId,
                sa.AcknowledgmentRole.RoleName,
                sa.StatusId))
            .ToListAsync();

        List<StoryAcknowledgmentIncomingRequestDto> incoming = await (
            from sa in readDb.StoryAcknowledgments.IgnoreQueryFilters(["ContentRating", "IsTakenDown", "StoryStatus"])
            join story in readDb.Stories.IgnoreQueryFilters(["ContentRating", "IsTakenDown", "StoryStatus"])
                on sa.StoryId equals story.StoryId
            where sa.AcknowledgedUserId == userId && sa.StatusId == StoryAcknowledgmentStatus.Pending
            orderby sa.DateAcknowledged descending
            select new StoryAcknowledgmentIncomingRequestDto(
                sa.StoryId,
                story.StoryListing != null ? story.StoryListing.StoryTitle : string.Empty,
                story.AuthorId,
                story.Author != null ? story.Author.UserName : null,
                sa.AcknowledgmentRoleId,
                sa.AcknowledgmentRole.RoleName))
            .ToListAsync();

        return new StoryAcknowledgmentManageDto(outgoing, incoming);
    }

    public async Task<IReadOnlyList<AcknowledgmentRoleDto>> GetAcknowledgmentRolesAsync()
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        return await readDb.AcknowledgmentRoles
            .OrderBy(r => r.AcknowledgmentRoleId)
            .Select(r => new AcknowledgmentRoleDto(r.AcknowledgmentRoleId, r.RoleName))
            .ToListAsync();
    }

    /// <summary>Shared by the write service too (protected, avoids double-capturing ActiveUser).
    /// Delegates to the shared <see cref="ActiveUserContextExtensions.RequireUserId"/> guard (MA-210).</summary>
    protected int RequireAuthenticatedUser() => ActiveUser.RequireUserId();
}
