namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the UserStoryInteractions feature cluster. Inherits the read side so the panel
/// composite can inject a single interface and still read back state after a debounce flush.
/// </summary>
public interface IUserStoryInteractionWriteService : IUserStoryInteractionReadService
{
    /// <summary>
    /// Consolidated upsert of the six panel-managed bits for the current viewer + story. Called
    /// once per story after the panel's debounce fires, not on every button click. The service:
    /// <list type="bullet">
    ///   <item>Preserves <c>HasStarted</c> (read-path owned, WU26).</item>
    ///   <item>Stamps / nulls <see cref="UserStoryInteractionDate"/> columns per spec §4.</item>
    ///   <item>Removes the row entirely when all seven bits are false (sparse semantics).</item>
    ///   <item>Rejects logically impossible combinations per the §4 truth table.</item>
    /// </list>
    /// Throws <see cref="InvalidOperationException"/> when the viewer is anonymous.
    /// </summary>
    Task SetInteractionStateAsync(int storyId, InteractionStateUpdate update);
}
