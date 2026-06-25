namespace TheCanalaveLibrary.Core;

/// <summary>
/// Controls the ordering of notifications returned by
/// <see cref="INotificationReadService.GetNotificationsAsync"/>.
/// Added additively in WU33 — existing callers omit this parameter and default to
/// <see cref="NewestFirst"/>, leaving their behaviour unchanged.
/// </summary>
public enum NotificationFeedOrder
{
    /// <summary>Most recently created notifications first (default).</summary>
    NewestFirst = 0,

    /// <summary>
    /// Unread notifications in chronological order (oldest unread first), followed by
    /// read notifications in chronological order. Optimised for "catch up in sequence"
    /// reading of a backlog.
    /// </summary>
    OldestUnreadFirst = 1
}
