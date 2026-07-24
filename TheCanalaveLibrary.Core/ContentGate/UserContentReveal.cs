namespace TheCanalaveLibrary.Core;

/// <summary>
/// A signed-in member's durable per-item mature-content consent ("reveal") — WU-AccessGate.
/// Granted by the consent interstitial's "View this story"/"View this group" action; consulted by
/// the Direct-navigation-plane gated reads (never by Discovery-plane listings — reveals never
/// widen browse/search); revocable from the settings page. Anonymous viewers' reveals live in the
/// prefs cookie instead (see identity-and-authorization.md §"Viewer Consent State"); they are
/// deliberately discarded on login — re-consent is one click.
/// Composite PK (UserId, EntityType, EntityId); cascade-delete with the user.
/// </summary>
public class UserContentReveal
{
    public int UserId { get; set; }
    public RevealedEntityType EntityType { get; set; }
    public int EntityId { get; set; }
    public DateTime DateRevealed { get; set; }

    public virtual User? User { get; set; }
}
