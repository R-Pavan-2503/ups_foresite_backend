using Npgsql;
using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Core.Services;

public class LineCommentsService : ILineCommentsService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<LineCommentsService> _logger;

    public LineCommentsService(NpgsqlDataSource dataSource, ILogger<LineCommentsService> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task<List<LineCommentDto>> GetLineCommentsForFileAsync(Guid fileId, Guid userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // Get all shared comments + user's personal comments for this file
        var query = @"
            SELECT 
                lc.id,
                lc.repository_id,
                lc.file_id,
                lc.line_number,
                lc.comment_text,
                lc.is_shared,
                lc.created_by_user_id,
                u.author_name as created_by_username,
                u.avatar_url as created_by_avatar_url,
                lc.created_at,
                lc.updated_at
            FROM file_line_comments lc
            JOIN users u ON lc.created_by_user_id = u.id
            WHERE lc.file_id = @fileId
              AND (lc.is_shared = true OR lc.created_by_user_id = @userId)
            ORDER BY lc.line_number ASC, lc.created_at ASC";

        using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("fileId", fileId);
        cmd.Parameters.AddWithValue("userId", userId);

        var comments = new List<LineCommentDto>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            comments.Add(new LineCommentDto
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                FileId = reader.GetGuid(2),
                LineNumber = reader.GetInt32(3),
                CommentText = reader.GetString(4),
                IsShared = reader.GetBoolean(5),
                CreatedByUserId = reader.GetGuid(6),
                CreatedByUsername = reader.GetString(7),
                CreatedByAvatarUrl = reader.IsDBNull(8) ? null : reader.GetString(8),
                CreatedAt = reader.GetDateTime(9),
                UpdatedAt = reader.GetDateTime(10)
            });
        }

        return comments;
    }

    public async Task<List<LineCommentDto>> GetLineCommentsForLineAsync(Guid fileId, int lineNumber, Guid userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        var query = @"
            SELECT 
                lc.id,
                lc.repository_id,
                lc.file_id,
                lc.line_number,
                lc.comment_text,
                lc.is_shared,
                lc.created_by_user_id,
                u.author_name as created_by_username,
                u.avatar_url as created_by_avatar_url,
                lc.created_at,
                lc.updated_at
            FROM file_line_comments lc
            JOIN users u ON lc.created_by_user_id = u.id
            WHERE lc.file_id = @fileId
              AND lc.line_number = @lineNumber
              AND (lc.is_shared = true OR lc.created_by_user_id = @userId)
            ORDER BY lc.created_at ASC";

        using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("fileId", fileId);
        cmd.Parameters.AddWithValue("lineNumber", lineNumber);
        cmd.Parameters.AddWithValue("userId", userId);

        var comments = new List<LineCommentDto>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            comments.Add(new LineCommentDto
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                FileId = reader.GetGuid(2),
                LineNumber = reader.GetInt32(3),
                CommentText = reader.GetString(4),
                IsShared = reader.GetBoolean(5),
                CreatedByUserId = reader.GetGuid(6),
                CreatedByUsername = reader.GetString(7),
                CreatedByAvatarUrl = reader.IsDBNull(8) ? null : reader.GetString(8),
                CreatedAt = reader.GetDateTime(9),
                UpdatedAt = reader.GetDateTime(10)
            });
        }

        return comments;
    }

    public async Task<LineCommentDto> CreateLineCommentAsync(Guid userId, CreateLineCommentRequest request)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        var query = @"
            INSERT INTO file_line_comments (
                repository_id,
                file_id,
                line_number,
                comment_text,
                is_shared,
                created_by_user_id
            ) VALUES (
                @repositoryId,
                @fileId,
                @lineNumber,
                @commentText,
                @isShared,
                @userId
            ) RETURNING id, created_at, updated_at";

        using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("repositoryId", request.RepositoryId);
        cmd.Parameters.AddWithValue("fileId", request.FileId);
        cmd.Parameters.AddWithValue("lineNumber", request.LineNumber);
        cmd.Parameters.AddWithValue("commentText", request.CommentText);
        cmd.Parameters.AddWithValue("isShared", request.IsShared);
        cmd.Parameters.AddWithValue("userId", userId);

        using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        var id = reader.GetGuid(0);
        var createdAt = reader.GetDateTime(1);
        var updatedAt = reader.GetDateTime(2);
        await reader.CloseAsync();

        // Get user info
        var userQuery = "SELECT author_name, avatar_url FROM users WHERE id = @userId";
        using var userCmd = new NpgsqlCommand(userQuery, conn);
        userCmd.Parameters.AddWithValue("userId", userId);
        using var userReader = await userCmd.ExecuteReaderAsync();
        await userReader.ReadAsync();
        var username = userReader.GetString(0);
        var avatarUrl = userReader.IsDBNull(1) ? null : userReader.GetString(1);

        return new LineCommentDto
        {
            Id = id,
            RepositoryId = request.RepositoryId,
            FileId = request.FileId,
            LineNumber = request.LineNumber,
            CommentText = request.CommentText,
            IsShared = request.IsShared,
            CreatedByUserId = userId,
            CreatedByUsername = username,
            CreatedByAvatarUrl = avatarUrl,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public async Task<LineCommentDto?> UpdateLineCommentAsync(Guid commentId, Guid userId, UpdateLineCommentRequest request)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // First check if the comment exists and belongs to the user
        var checkQuery = "SELECT created_by_user_id FROM file_line_comments WHERE id = @commentId";
        using var checkCmd = new NpgsqlCommand(checkQuery, conn);
        checkCmd.Parameters.AddWithValue("commentId", commentId);
        var result = await checkCmd.ExecuteScalarAsync();
        
        if (result == null) return null; // Comment not found
        
        var ownerId = (Guid)result;
        if (ownerId != userId) return null; // Not the owner

        // Update the comment
        var updateQuery = @"
            UPDATE file_line_comments
            SET comment_text = @commentText,
                updated_at = NOW()
            WHERE id = @commentId
            RETURNING repository_id, file_id, line_number, is_shared, created_at, updated_at";

        using var updateCmd = new NpgsqlCommand(updateQuery, conn);
        updateCmd.Parameters.AddWithValue("commentId", commentId);
        updateCmd.Parameters.AddWithValue("commentText", request.CommentText);

        using var reader = await updateCmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        var repositoryId = reader.GetGuid(0);
        var fileId = reader.GetGuid(1);
        var lineNumber = reader.GetInt32(2);
        var isShared = reader.GetBoolean(3);
        var createdAt = reader.GetDateTime(4);
        var updatedAt = reader.GetDateTime(5);
        await reader.CloseAsync();

        // Get user info
        var userQuery = "SELECT author_name, avatar_url FROM users WHERE id = @userId";
        using var userCmd = new NpgsqlCommand(userQuery, conn);
        userCmd.Parameters.AddWithValue("userId", userId);
        using var userReader = await userCmd.ExecuteReaderAsync();
        await userReader.ReadAsync();
        var username = userReader.GetString(0);
        var avatarUrl = userReader.IsDBNull(1) ? null : userReader.GetString(1);

        return new LineCommentDto
        {
            Id = commentId,
            RepositoryId = repositoryId,
            FileId = fileId,
            LineNumber = lineNumber,
            CommentText = request.CommentText,
            IsShared = isShared,
            CreatedByUserId = userId,
            CreatedByUsername = username,
            CreatedByAvatarUrl = avatarUrl,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public async Task<bool> DeleteLineCommentAsync(Guid commentId, Guid userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // Delete only if the user is the owner
        var query = @"
            DELETE FROM file_line_comments
            WHERE id = @commentId AND created_by_user_id = @userId";

        using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("commentId", commentId);
        cmd.Parameters.AddWithValue("userId", userId);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }
}

