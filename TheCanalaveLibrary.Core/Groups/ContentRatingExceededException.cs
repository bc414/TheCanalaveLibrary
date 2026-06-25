namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by <c>ServerGroupWriteService</c> when a story's <see cref="Rating"/> exceeds the
/// group's <see cref="Group.MaxContentRating"/> (tier 2) or a folder's
/// <see cref="GroupFolder.MaxRating"/> (tier 3) in the content-rating waterfall.
/// See <c>layer2-services.md</c> §"Group Rating Waterfall".
/// </summary>
public class ContentRatingExceededException(string message) : Exception(message);
