using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Core.Interfaces;

/// <summary>
/// Service for managing user notifications.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Get paginated notifications for a user.
    /// </summary>
    Task<NotificationsResponse> GetNotificationsAsync(Guid userId, int page = 1, int pageSize = 20);

    /// <summary>
    /// Get count of unread notifications for a user.
    /// </summary>
    Task<int> GetUnreadCountAsync(Guid userId);

    /// <summary>
    /// Create a mention notification.
    /// </summary>
    Task CreateMentionNotificationAsync(Guid mentionedUserId, Guid mentionerUserId, string mentionerUsername, Guid? fileId, Guid? repositoryId, string linkUrl);

    /// <summary>
    /// Mark a notification as read.
    /// </summary>
    Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId);

    /// <summary>
    /// Mark all notifications as read for a user.
    /// </summary>
    Task MarkAllAsReadAsync(Guid userId);

    /// <summary>
    /// Delete a notification.
    /// </summary>
    Task<bool> DeleteNotificationAsync(Guid notificationId, Guid userId);
}
