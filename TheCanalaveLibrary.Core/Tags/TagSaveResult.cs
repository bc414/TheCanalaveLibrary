namespace TheCanalaveLibrary.Core;

/// <summary>
/// Result returned by tag create / update. The save always succeeds when returned (validation
/// exceptions are thrown, not included here). <see cref="SpriteWarning"/> is non-null when the
/// <see cref="Tag.SpriteIdentifier"/> was non-empty but no matching static asset was found in the
/// default theme — a non-blocking advisory (out-of-band provisioning may lag tag creation).
/// </summary>
/// <param name="TagId">The created or updated tag's id.</param>
/// <param name="SpriteWarning">
/// Optional advisory message. Show inline in the editor form; do not block or revert the save.
/// </param>
public sealed record TagSaveResult(int TagId, string? SpriteWarning);
