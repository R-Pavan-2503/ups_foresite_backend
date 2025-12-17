using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CodeFamily.Api.Core.Services;

/// <summary>
/// Service for managing user notifications.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(NpgsqlDataSource dataSource, ILogger<NotificationService> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task<NotificationsResponse> GetNotificationsAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        var offset = (page - 1) * pageSize;

        // Get total count
        using var countCmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM user_notifications WHERE user_id = @userId", conn);
        countCmd.Parameters.AddWithValue("userId", userId);
        var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

        // Get unread count
        using var unreadCmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM user_notifications WHERE user_id = @userId AND is_read = FALSE", conn);
        unreadCmd.Parameters.AddWithValue("userId", userId);
        var unreadCount = Convert.ToInt32(await unreadCmd.ExecuteScalarAsync());

        // Get notifications
        using var cmd = new NpgsqlCommand(
            @"SELECT id, notification_type, title, message, link_url, related_file_id, related_repository_id, is_read, created_at
              FROM user_notifications
              WHERE user_id = @userId
              ORDER BY created_at DESC
              LIMIT @limit OFFSET @offset", conn);

        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("limit", pageSize);
        cmd.Parameters.AddWithValue("offset", offset);

        var notifications = new List<NotificationDto>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            notifications.Add(new NotificationDto
            {
                Id = reader.GetGuid(0),
                NotificationType = reader.GetString(1),
                Title = reader.GetString(2),
                Message = reader.GetString(3),
                LinkUrl = reader.IsDBNull(4) ? null : reader.GetString(4),
                RelatedFileId = reader.IsDBNull(5) ? null : reader.GetGuid(5),
                RelatedRepositoryId = reader.IsDBNull(6) ? null : reader.GetGuid(6),
                IsRead = reader.GetBoolean(7),
                CreatedAt = reader.GetDateTime(8)
            });
        }

        return new NotificationsResponse
        {
            Notifications = notifications,
            TotalCount = totalCount,
            UnreadCount = unreadCount
        };
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM user_notifications WHERE user_id = @userId AND is_read = FALSE", conn);
        cmd.Parameters.AddWithValue("userId", userId);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task CreateMentionNotificationAsync(Guid mentionedUserId, Guid mentionerUserId, string mentionerUsername, Guid? fileId, Guid? repositoryId, string linkUrl)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            @"INSERT INTO user_notifications (id, user_id, notification_type, title, message, link_url, related_file_id, related_repository_id, is_read, created_at)
              VALUES (@id, @userId, 'mention', @title, @message, @linkUrl, @fileId, @repoId, FALSE, @now)", conn);

        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("userId", mentionedUserId);
        cmd.Parameters.AddWithValue("title", $"{mentionerUsername} mentioned you");
        cmd.Parameters.AddWithValue("message", $"You were mentioned in a discussion by {mentionerUsername}");
        cmd.Parameters.AddWithValue("linkUrl", linkUrl);
        cmd.Parameters.AddWithValue("fileId", fileId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("repoId", repositoryId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("now", DateTime.UtcNow);

        await cmd.ExecuteNonQueryAsync();
        _logger.LogInformation("Created mention notification for user {UserId}", mentionedUserId);
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "UPDATE user_notifications SET is_read = TRUE WHERE id = @notificationId AND user_id = @userId", conn);
        cmd.Parameters.AddWithValue("notificationId", notificationId);
        cmd.Parameters.AddWithValue("userId", userId);

        var affected = await cmd.ExecuteNonQueryAsync();
        return affected > 0;
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "UPDATE user_notifications SET is_read = TRUE WHERE user_id = @userId AND is_read = FALSE", conn);
        cmd.Parameters.AddWithValue("userId", userId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> DeleteNotificationAsync(Guid notificationId, Guid userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "DELETE FROM user_notifications WHERE id = @notificationId AND user_id = @userId", conn);
        cmd.Parameters.AddWithValue("notificationId", notificationId);
        cmd.Parameters.AddWithValue("userId", userId);

        var affected = await cmd.ExecuteNonQueryAsync();
        return affected > 0;
    }
}

