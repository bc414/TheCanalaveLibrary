using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Resolves the User-rooted Restrict conflicts in OnModelCreating (see IdentityConfigurations.cs and
/// FollowingConfigurations.cs) before deleting a user. Every other FK rooted at User is Cascade or
/// SetNull and is handled by the database once the user row is removed.
/// </summary>
public class UserDeletionService
{
    private readonly ApplicationDbContext _context;

    public UserDeletionService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Deletes a user and all their associated data, correctly handling
    /// complex foreign key constraints before the delete operation.
    /// </summary>
    /// <param name="userId">The ID of the user to delete.</param>
    /// <returns>True if the user was deleted, false if not found.</returns>
    public async Task<bool> DeleteUserAsync(int userId)
    {
        // EnableRetryOnFailure() (Program.cs DbContext registration — settled WU12, see
        // layer2-services.md "DbContext Registration") enables Npgsql's retrying execution strategy,
        // which refuses user-initiated transactions started directly via BeginTransactionAsync — the
        // whole retriable unit (including the transaction) must run through CreateExecutionStrategy().
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return false; // User not found
            }

            // --- 1. HANDLE 'UserProfileComment' CONFLICT ---
            // Rule: UserProfileComment.ProfileUserId is set to 'Restrict'.
            // Action: delete all comments left on this user's profile (TPT — no direct DbSet).
            var commentsOnProfile = _context.BaseComments.OfType<UserProfileComment>()
                .Where(c => c.ProfileUserId == userId);
            _context.BaseComments.RemoveRange(commentsOnProfile);

            // --- 2. HANDLE 'Notification' CONFLICT ---
            // Rule: Notification.SourceUserId is set to 'Restrict'.
            // Action: Manually set SourceUserId to NULL for all notifications sent by this user.
            await _context.Notifications
                .Where(n => n.SourceUserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.SourceUserId, (int?)null));

            // --- 3. HANDLE 'FollowedUser' CONFLICT ---
            // Rule: FollowedUser.FollowedUserId is set to 'Restrict'.
            // Action: Manually delete all 'Follow' entries where this user *is the one being followed*.
            var followsOnUser = _context.FollowedUsers
                .Where(f => f.FollowedUserId == userId);
            _context.FollowedUsers.RemoveRange(followsOnUser);

            // --- 4. HANDLE 'Vouch' CONFLICT ---
            // Rule: Vouch.VouchedUserId is set to 'Restrict' (VouchingUserId side is Cascade).
            // Action: Manually delete vouches received by this user.
            var vouchesReceived = _context.Vouches
                .Where(v => v.VouchedUserId == userId);
            _context.Vouches.RemoveRange(vouchesReceived);

            // --- 5. DELETE THE USER ---
            // The database now handles every remaining 'Cascade'/'SetNull' rule, including the
            // UserStat 1-to-1 Cascade.
            _context.Users.Remove(user);

            // --- 6. SAVE AND COMMIT ---
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return true;
        });
    }
}