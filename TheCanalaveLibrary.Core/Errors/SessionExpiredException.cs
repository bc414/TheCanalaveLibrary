namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown client-side (<c>ClientHttpHelpers</c>) when an API call comes back 401 — the cookie
/// handler's bare 401 (expired/absent session) and a service's "...requires an authenticated
/// user" <see cref="InvalidOperationException"/>→401 arm (<c>EndpointHelpers.ExecuteAsync</c>)
/// both reconstruct as this. Deliberately distinct from <see cref="UnauthorizedAccessException"/>
/// (403, authenticated-but-forbidden) — a 401 means "sign in again," not "you don't have
/// permission" (WU-ErrorHandling2, 2026-07-30; error-handling.md §"The API error envelope";
/// identity-and-authorization.md).
/// </summary>
public sealed class SessionExpiredException()
    : Exception("Your session has expired — sign in again to continue.");
