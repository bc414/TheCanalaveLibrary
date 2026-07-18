using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation for Story Lineage (Feature 10, WU42). Mirrors
/// <see cref="ServerSeriesReadService"/>'s shape (primary-ctor over the read-context factory,
/// protected members for the derived write service).
/// </summary>
public class ServerStoryLineageReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser) : IStoryLineageReadService
{
    /// <summary>Protected so the derived write service can access it without double-capturing the
    /// constructor parameter (CS9107/CS9124 — see layer2-services.md).</summary>
    protected IActiveUserContext ActiveUser { get; } = activeUser;

    protected IDbContextFactory<ReadOnlyApplicationDbContext> ReadDbFactory { get; } = readDbFactory;

    public async Task<IReadOnlyList<StoryLineageDto>> GetLineageForStoryAsync(int storyId)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // Explicit join through Stories (target) applies the viewer's ContentRating/IsTakenDown
        // read filters — a link is only ever returned when its target survives them (mirrors
        // ServerSeriesReadService.GetMembershipsForStoryAsync's join-not-bare-projection rule).
        return await (
            from sl in readDb.StoryLineages
            join target in readDb.Stories on sl.TargetStoryId equals target.StoryId
            where sl.SourceStoryId == storyId && sl.StatusId == StoryLineageStatus.Approved
            orderby sl.RelationshipTypeId, target.StoryListing!.StoryTitle
            select new StoryLineageDto(
                sl.RelationshipTypeId,
                sl.RelationshipType.TypeName,
                sl.TargetStoryId,
                target.StoryListing != null ? target.StoryListing.StoryTitle : string.Empty))
            .ToListAsync();
    }

    public async Task<StoryLineageManageDto> GetManageDataForUserAsync()
    {
        int userId = RequireAuthenticatedUser();
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // Elevated, owner-scoped read: ignore ContentRating/IsTakenDown on both sides so an author
        // can always see and manage their own links even if the other side's story has since gone
        // mature/taken-down for them (this is a management page, not a discovery surface).
        List<StoryLineageOutgoingDto> outgoing = await (
            from sl in readDb.StoryLineages.IgnoreQueryFilters(["ContentRating", "IsTakenDown"])
            join source in readDb.Stories.IgnoreQueryFilters(["ContentRating", "IsTakenDown"])
                on sl.SourceStoryId equals source.StoryId
            join target in readDb.Stories.IgnoreQueryFilters(["ContentRating", "IsTakenDown"])
                on sl.TargetStoryId equals target.StoryId
            where source.AuthorId == userId
            orderby sl.DateCreated descending
            select new StoryLineageOutgoingDto(
                sl.SourceStoryId,
                source.StoryListing != null ? source.StoryListing.StoryTitle : string.Empty,
                sl.TargetStoryId,
                target.StoryListing != null ? target.StoryListing.StoryTitle : string.Empty,
                sl.RelationshipTypeId,
                sl.RelationshipType.TypeName,
                sl.StatusId))
            .ToListAsync();

        List<StoryLineageIncomingRequestDto> incoming = await (
            from sl in readDb.StoryLineages.IgnoreQueryFilters(["ContentRating", "IsTakenDown"])
            join source in readDb.Stories.IgnoreQueryFilters(["ContentRating", "IsTakenDown"])
                on sl.SourceStoryId equals source.StoryId
            join target in readDb.Stories.IgnoreQueryFilters(["ContentRating", "IsTakenDown"])
                on sl.TargetStoryId equals target.StoryId
            where target.AuthorId == userId && sl.StatusId == StoryLineageStatus.Pending
            orderby sl.DateCreated descending
            select new StoryLineageIncomingRequestDto(
                sl.SourceStoryId,
                source.StoryListing != null ? source.StoryListing.StoryTitle : string.Empty,
                source.AuthorId,
                source.Author != null ? source.Author.UserName : null,
                sl.TargetStoryId,
                target.StoryListing != null ? target.StoryListing.StoryTitle : string.Empty,
                sl.RelationshipTypeId,
                sl.RelationshipType.TypeName))
            .ToListAsync();

        return new StoryLineageManageDto(outgoing, incoming);
    }

    public async Task<IReadOnlyList<StoryLineageTypeDto>> GetLineageTypesAsync()
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        return await readDb.StoryLineageTypes
            .OrderBy(t => t.RelationshipTypeId)
            .Select(t => new StoryLineageTypeDto(t.RelationshipTypeId, t.TypeName))
            .ToListAsync();
    }

    /// <summary>Shared by the write service too (protected, avoids double-capturing ActiveUser).
    /// Delegates to the shared <see cref="ActiveUserContextExtensions.RequireUserId"/> guard (MA-210).</summary>
    protected int RequireAuthenticatedUser() => ActiveUser.RequireUserId();
}
