namespace TheCanalaveLibrary.Core;

/// <summary>
/// Shared replacement for the per-service <c>RequireAuthenticatedUser()</c> copies (MA-210/MA-308):
/// the identical "UserId or throw" guard that every authenticated write path opens with lives here
/// once instead of being re-implemented per service.
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
}
