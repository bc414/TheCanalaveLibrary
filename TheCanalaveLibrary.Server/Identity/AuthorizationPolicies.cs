namespace TheCanalaveLibrary.Server;

/// <summary>
/// Named authorization-policy identifiers, registered once in <c>Program.cs</c>
/// (identity-and-authorization.md §"Role-Based (Moderator) Gating": prefer a named policy over
/// repeating role lists once more than one or two surfaces need it). Referenced by endpoint groups
/// via <c>.RequireAuthorization(AuthorizationPolicies.RequireModerator)</c> — the edge half of the
/// defense-in-depth pair whose service half is <c>RequireModerator()</c> in the mod write services.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Moderator-or-Admin role gate. <c>IsInRole</c> is literal — there is no Admin-inherits-
    /// Moderator hierarchy — so the registration lists both roles explicitly.
    /// </summary>
    public const string RequireModerator = "RequireModerator";
}
