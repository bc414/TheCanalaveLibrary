namespace TheCanalaveLibrary.Server;

/// <summary>
/// Named authorization-policy identifiers, registered once in <c>Program.cs</c>
/// (identity-and-authorization.md §"Role-Based (Moderator) Gating": prefer a named policy over
/// repeating role lists once more than one or two surfaces need it). Referenced by endpoint groups
/// via <c>.RequireAuthorization(AuthorizationPolicies.RequireModerator)</c> — the edge half of the
/// defense-in-depth pair whose service half is the shared <c>ActiveUser.RequireModerator()</c> in every
/// mod-only read and write service (owner ruling D9 extended it to the reads, WU-ModerationIntegrity).
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Moderator-or-Admin role gate. <c>IsInRole</c> is literal — there is no Admin-inherits-
    /// Moderator hierarchy — so the registration lists both roles explicitly.
    /// </summary>
    public const string RequireModerator = "RequireModerator";
}
