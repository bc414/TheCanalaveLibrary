using TheCanalaveLibrary.Data;

namespace TheCanalaveLibrary.Services;

// This would be in a service class, e.g., UserService.cs
// You would inject your DbContext via the constructor.
// (Assuming your context is named ApplicationDbContext)

using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Models;

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
        // Use a transaction to ensure all operations succeed or fail together.
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var user = await _context.Users
                .Include(u => u.UserStat) // Include 1-to-1 to delete it
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return false; // User not found
            }
            /*

            // --- 1. HANDLE 'UserProfileComment' CONFLICT ---
            // Rule: UserProfileComment.ProfileUserId is set to 'Restrict'.
            // Action: Manually delete all comments on the user's profile.
            var commentsOnProfile = _context.UserProfileComments
                .Where(c => c.ProfileUserId == userId);
            _context.UserProfileComments.RemoveRange(commentsOnProfile);
            
            */

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

            // --- 4. DELETE THE USER AND 1-to-1 DATA ---
            // The database will now handle all 'Cascade' and 'SetNull' rules.
            
            // Delete associated UserStat (1-to-1)
            if (user.UserStat != null)
            {
                _context.UserStats.Remove(user.UserStat);
            }
            
            // Delete the user
            _context.Users.Remove(user);

            // --- 5. SAVE AND COMMIT ---
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            
            return true;
        }
        catch (Exception)
        {
            // Something went wrong, roll back all changes.
            await transaction.RollbackAsync();
            throw; 
        }
    }
}