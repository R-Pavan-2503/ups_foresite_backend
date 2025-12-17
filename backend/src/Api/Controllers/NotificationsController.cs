using Microsoft.AspNetCore.Mvc;
using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Controllers;

/// <summary>
/// Controller for user notifications.
/// </summary>
[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(INotificationService notificationService, ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Get paginated notifications for a user.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<NotificationsResponse>> GetNotifications(
        [FromQuery] Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _notificationService.GetNotificationsAsync(userId, page, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notifications for user {UserId}", userId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get unread notification count for a user.
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount([FromQuery] Guid userId)
    {
        try
        {
            var count = await _notificationService.GetUnreadCountAsync(userId);
            return Ok(new { count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count for user {UserId}", userId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Mark a notification as read.
    /// </summary>
    [HttpPut("{notificationId}/read")]
    public async Task<ActionResult> MarkAsRead(Guid notificationId, [FromQuery] Guid userId)
    {
        try
        {
            var success = await _notificationService.MarkAsReadAsync(notificationId, userId);
            if (!success)
            {
                return NotFound(new { error = "Notification not found" });
            }
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {NotificationId} as read", notificationId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Mark all notifications as read for a user.
    /// </summary>
    [HttpPut("mark-all-read")]
    public async Task<ActionResult> MarkAllAsRead([FromQuery] Guid userId)
    {
        try
        {
            await _notificationService.MarkAllAsReadAsync(userId);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read for user {UserId}", userId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a notification.
    /// </summary>
    [HttpDelete("{notificationId}")]
    public async Task<ActionResult> DeleteNotification(Guid notificationId, [FromQuery] Guid userId)
    {
        try
        {
            var success = await _notificationService.DeleteNotificationAsync(notificationId, userId);
            if (!success)
            {
                return NotFound(new { error = "Notification not found" });
            }
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification {NotificationId}", notificationId);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
