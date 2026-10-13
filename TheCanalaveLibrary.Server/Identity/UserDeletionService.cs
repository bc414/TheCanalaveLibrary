using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Deletes an account: a <b>hard delete of the <c>User</c> row</b>, producing the survival set owner
/// ruling D13 ratified as the site's erasure policy (<c>cross-cutting.md</c> §"Delete Policy Summary" →
/// "Account deletion"). Authored content survives anonymized through the database's <c>SET NULL</c>
/// edges, personal data goes through its <c>CASCADE</c> edges, and this service does the rest, in one
/// transaction:
/// <list type="number">
/// <item><b>Before the delete</b>, it resolves the User-rooted <c>RESTRICT</c> edges (see
/// IdentityConfigurations.cs and FollowingConfigurations.cs): the comments on the user's profile go
/// through their base rows, notifications they caused lose their source, and follows and vouches they
/// received are deleted.</item>
/// <item><b>Reports on what this destroys are closed here</b> (owner ruling D7's sub-edge, picked by
/// WU-ModerationIntegrity 2026-09-30 — at the source, not by a reconciler): the open reports about the
/// user themselves and about the comments on their profile, via
/// <see cref="ReportLedger.CloseForDestroyedTargetsAsync"/>. Reports the user <em>filed</em> or was
/// answerable for survive with those FKs nulled (SET NULL — the report row is the audit ledger).</item>
/// <item><b>After the save</b>, it deletes the conversations the deletion left with no participant
/// (D13 edge (a)), first closing open reports on their messages. A conversation where the other person
/// remains survives intact: she keeps the thread, and the user's messages lose their sender.</item>
/// </list>
/// <para>Server-only (the static-SSR <c>DeletePersonalData</c> page and the dev endpoint resolve it by
/// type); it has no Core interface.</para>
/// </summary>
public class UserDeletionService(ApplicationDbContext writeDb)
{
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
        var strategy = writeDb.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            // Clear first (layer2-services.md §"Hard deletes of content parents"): the profile wall goes
            // by raw SQL below, which never reaches the tracker, and EF refuses to Remove the user while
            // a tracked UserProfileComment (a RESTRICT dependent) remains. A retried attempt must not
            // inherit the failed one's tracked state either.
            writeDb.ChangeTracker.Clear();
            await using var transaction = await writeDb.Database.BeginTransactionAsync();

            var user = await writeDb.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return false; // User not found
            }

            // --- 1. HANDLE 'UserProfileComment' CONFLICT ---
            // Rule: UserProfileComment.ProfileUserId is set to 'Restrict' (the in-schema precedent for
            // owner ruling D10's content-parent posture).
            // Action: delete all comments left on this user's profile through their base rows
            // (TptDelete — the one shape every content-parent delete uses; never materialized). Only
            // their ids are read first: step 1b closes the reports against them.
            List<long> commentIdsOnProfile = await writeDb.UserProfileComments
                .Where(c => c.ProfileUserId == userId)
                .Select(c => c.CommentId)
                .ToListAsync();
            await TptDelete.ProfileWallCommentsAsync(writeDb, userId);

            // --- 1b. CLOSE THE REPORTS ON WHAT THIS DESTROYS (D7 sub-edge) ---
            // Reports have no FK to their target, so they would survive as Open rows the queue can never
            // show or resolve. Closed silently as ResolvedNoAction with no moderator (ReportLedger's doc
            // says why). The user's authored stories, posts and recommendations survive (SET NULL), so
            // reports on them stay open — those targets still exist and can still be acted on.
            await ReportLedger.CloseForDestroyedTargetsAsync(writeDb, ReportedEntityType.Comment,
                commentIdsOnProfile,
                // Not "its owner's": the comment's author is the commenter, whose account survives — and
                // whose per-user history (ReportedUserId) is where a moderator reads this note.
                "Closed automatically: the reported comment was on a profile whose account was deleted.");
            await ReportLedger.CloseForDestroyedTargetsAsync(writeDb, ReportedEntityType.User,
                [userId],
                "Closed automatically: the reported account was deleted.");

            // --- 2. HANDLE 'Notification' CONFLICT ---
            // Rule: Notification.SourceUserId is set to 'Restrict'.
            // Action: Manually set SourceUserId to NULL for all notifications sent by this user.
            await writeDb.Notifications
                .Where(n => n.SourceUserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.SourceUserId, (int?)null));

            // --- 3. HANDLE 'FollowedUser' CONFLICT ---
            // Rule: FollowedUser.FollowedUserId is set to 'Restrict'.
            // Action: Manually delete all 'Follow' entries where this user *is the one being followed*.
            var followsOnUser = writeDb.FollowedUsers
                .Where(f => f.FollowedUserId == userId);
            writeDb.FollowedUsers.RemoveRange(followsOnUser);

            // --- 4. HANDLE 'Vouch' CONFLICT ---
            // Rule: Vouch.VouchedUserId is set to 'Restrict' (VouchingUserId side is Cascade).
            // Action: Manually delete vouches received by this user.
            var vouchesReceived = writeDb.Vouches
                .Where(v => v.VouchedUserId == userId);
            writeDb.Vouches.RemoveRange(vouchesReceived);

            // --- 5. CAPTURE THE USER'S CONVERSATIONS (for step 8) ---
            // Read before the delete: the participant rows cascade away with the user, after which
            // nothing links these conversations to this account. Index-backed (the participant FK index
            // on user_id).
            List<int> conversationIds = await writeDb.ConversationParticipants
                .Where(p => p.UserId == userId)
                .Select(p => p.ConversationId)
                .ToListAsync();

            // --- 6. DELETE THE USER ---
            // A HARD delete, and that is load-bearing (owner ruling D13's GDPR posture): severing every
            // FK is what makes the surviving content anonymous data rather than personal data. Never
            // soften this into an anonymize-in-place, and never keep a deleted_user → old_user_id
            // mapping for support or any other purpose — either turns anonymization into
            // pseudonymization (cross-cutting.md §"Account deletion").
            // The database now handles every remaining 'Cascade'/'SetNull' rule, including the
            // UserStat 1-to-1 Cascade and the poll votes' SET NULL (D13: votes survive anonymized).
            writeDb.Users.Remove(user);

            // --- 7. SAVE ---
            await writeDb.SaveChangesAsync();

            // --- 8. DELETE THE CONVERSATIONS THIS LEFT EMPTY (D13 edge (a)) ---
            // After the save, because the participant rows vanish only with the user row — so this is
            // set-based SQL, not a tracked delete. Scoped to the conversations captured in step 5: an
            // implementation refinement of the owner's "remove conversations with no remaining
            // participants" that keeps the statement O(this user's conversations) instead of an
            // anti-join over the whole table. Account deletion is the only designed producer of an
            // empty conversation, so cleaning up at the source is exhaustive; no sweeper exists.
            if (conversationIds.Count > 0)
            {
                IQueryable<Conversation> emptied = writeDb.Conversations
                    .Where(c => conversationIds.Contains(c.ConversationId) && !c.ConversationParticipants.Any());

                // Open reports on the messages about to go (amendment U3 — the D7 class): only the
                // reported message ids are read, never the whole thread.
                List<long> reportedMessageIds = await writeDb.Reports
                    .Where(r => r.ReportedEntityType == ReportedEntityType.Message
                                && (r.ReportStatusId == ReportStatusEnum.Open
                                    || r.ReportStatusId == ReportStatusEnum.UnderReview)
                                && writeDb.PrivateMessages.Any(m => m.MessageId == r.ReportedEntityId
                                    && emptied.Any(c => c.ConversationId == m.ConversationId)))
                    .Select(r => r.ReportedEntityId)
                    .Distinct()
                    .ToListAsync();
                await ReportLedger.CloseForDestroyedTargetsAsync(writeDb, ReportedEntityType.Message,
                    reportedMessageIds,
                    "Closed automatically: both participants in the reported message's conversation deleted their accounts.");

                // Messages cascade with their conversation (private_messages.conversation_id CASCADE).
                await emptied.ExecuteDeleteAsync();
            }

            await transaction.CommitAsync();

            // Post-commit seam: deleting the avatar blob (WU-BlobCleanup, owner ruling D14) belongs
            // after the strategy returns, never inside the retried delegate.
            return true;
        });
    }
}
