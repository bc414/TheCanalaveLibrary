namespace TheCanalaveLibrary.Core;

/// <summary>
/// Cross-cutting constants for the UserStoryInteractions feature cluster.
/// Lives in Core (not Server's SiteConstants) so SharedUI can reference it without a Server dependency.
/// </summary>
public static class InteractionConstants
{
    /// <summary>
    /// Milliseconds the UserStoryInteractionPanel waits after the last toggle before flushing a write.
    /// Distinct from TagSelector's typeahead debounce (300 ms, package-managed).
    /// </summary>
    public const int InteractionDebounceMs = 2000;
}
