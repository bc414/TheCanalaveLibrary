using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation of <see cref="IAccountStatusReadService"/>. Anonymous viewers
/// resolve to <see cref="AccountStatusEnum.Active"/> (the "nothing to disclose" state) rather than
/// throwing — mirrors <c>ServerMessagingReadService.GetUnreadConversationCountAsync</c>'s
/// null-viewer handling. One indexed PK lookup; read context created per call, never held for the
/// service's lifetime (layer2-services.md §"Read-context concurrency: factory per method").
/// </summary>
public class ServerAccountStatusReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser) : IAccountStatusReadService
{
    public async Task<AccountStatusDto> GetMyAccountStatusAsync()
    {
        if (activeUser.UserId is not int userId)
            return new AccountStatusDto(AccountStatusEnum.Active, null);

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

        return await readDb.Users
            .Where(u => u.Id == userId)
            .Select(u => new AccountStatusDto(u.AccountStatus, u.SuspendedUntilUtc))
            .SingleOrDefaultAsync()
            ?? new AccountStatusDto(AccountStatusEnum.Active, null);
    }
}
