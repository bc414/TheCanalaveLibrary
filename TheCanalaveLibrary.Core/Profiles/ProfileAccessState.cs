namespace TheCanalaveLibrary.Core;

/// <summary>
/// Why a profile read is (or isn't) visible to the current viewer — lets the profile page render
/// an honest state instead of a soft-404 (WU-AccessGate Phase 1). An account's existence is
/// already public via its comments/stories/group memberships, so distinguishing
/// <see cref="Private"/> from <see cref="NotFound"/> leaks nothing new. (Private custom LISTS
/// keep their indistinguishable not-found by contrast — a list's existence is not otherwise
/// observable.) Not stored in the database.
/// </summary>
public enum ProfileAccessState : short
{
    NotFound = 0,
    Visible = 1,
    /// <summary>UsersOnly profile viewed anonymously — render a sign-in prompt.</summary>
    SignInRequired = 2,
    /// <summary>Private profile viewed by a non-owner — render "This profile is private."</summary>
    Private = 3,
}
