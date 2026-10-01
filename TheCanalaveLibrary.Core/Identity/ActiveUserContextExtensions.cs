namespace TheCanalaveLibrary.Core;

/// <summary>
/// The two shared service-layer guards (MA-210/MA-308; owner ruling D9). Every authenticated write
/// path opens with <see cref="RequireUserId"/>, and every moderator-only read or write opens with
/// <see cref="RequireModerator"/> — one copy each, never re-implemented per service, so every role gate
/// answers an anonymous caller and a signed-in non-moderator the same way
/// (<c>identity-and-authorization.md</c> §"Active-User Context").
/// </summary>
public static class ActiveUserContextExtensions
{
    /// <summary>
    /// Returns the active user's id, or throws <see cref="InvalidOperationException"/> when the
    /// viewer is anonymous. The exception type is load-bearing: the endpoint layer maps
    /// <see cref="InvalidOperationException"/> from these guards to 401 — do not change it.
    /// </summary>
    public static int RequireUserId(this IActiveUserContext activeUser)
    {
        if (activeUser.UserId is not int id)
            throw new InvalidOperationException("This operation requires an authenticated user.");
        return id;
    }

    /// <summary>
    /// Returns the moderator's user id. Anonymous → <see cref="InvalidOperationException"/> (401, the
    /// same load-bearing type as <see cref="RequireUserId"/>); signed in without the Moderator or Admin
    /// role → <see cref="UnauthorizedAccessException"/> (403). <c>IsInRole</c> is literal — Admin does
    /// not inherit Moderator — so both roles are accepted. Used by the moderator-only reads as well as
    /// the writes: a page's <c>[Authorize]</c> does not protect the circuit (D9).
    /// </summary>
    public static int RequireModerator(this IActiveUserContext activeUser)
    {
        int id = activeUser.RequireUserId();
        if (!activeUser.IsModerator && !activeUser.IsAdmin)
            throw new UnauthorizedAccessException("This operation requires the Moderator or Admin role.");
        return id;
    }
}
