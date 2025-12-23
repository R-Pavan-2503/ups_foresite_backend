using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Text.RegularExpressions;

namespace CodeFamily.Api.Core.Services;

/// <summary>
/// Service for managing file-level and repository-level notes.
/// </summary>
public class NotesService : INotesService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly INotificationService _notificationService;
    private readonly ISupabaseStorageService _storageService;
    private readonly ILogger<NotesService> _logger;

    public NotesService(
        NpgsqlDataSource dataSource,
        INotificationService notificationService,
        ISupabaseStorageService storageService,
        ILogger<NotesService> logger)
    {
        _dataSource = dataSource;
        _notificationService = notificationService;
        _storageService = storageService;
        _logger = logger;
    }

    // ==========================================
    // FILE-LEVEL STICKY NOTES
    // ==========================================

    public async Task<List<StickyNoteDto>> GetFileStickyNotesAsync(Guid fileId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            @"SELECT n.id, n.repository_id, n.file_id, n.created_by_user_id, n.note_type, 
                     n.content, n.document_url, n.document_name, n.document_size, 
                     n.created_at, n.updated_at,
                     u.author_name, u.avatar_url
              FROM file_sticky_notes n
              LEFT JOIN users u ON n.created_by_user_id = u.id
              WHERE n.file_id = @fileId
              ORDER BY n.created_at DESC", conn);

        cmd.Parameters.AddWithValue("fileId", fileId);

        var notes = new List<StickyNoteDto>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            notes.Add(new StickyNoteDto
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                FileId = reader.GetGuid(2),
                CreatedByUserId = reader.GetGuid(3),
                NoteType = reader.GetString(4),
                Content = reader.IsDBNull(5) ? null : reader.GetString(5),
                DocumentUrl = reader.IsDBNull(6) ? null : reader.GetString(6),
                DocumentName = reader.IsDBNull(7) ? null : reader.GetString(7),
                DocumentSize = reader.IsDBNull(8) ? null : reader.GetInt64(8),
                CreatedAt = reader.GetDateTime(9),
                UpdatedAt = reader.GetDateTime(10),
                CreatedByUsername = reader.IsDBNull(11) ? "Unknown" : reader.GetString(11),
                CreatedByAvatarUrl = reader.IsDBNull(12) ? null : reader.GetString(12)
            });
        }
        return notes;
    }

    public async Task<StickyNoteDto> CreateFileStickyNoteAsync(Guid userId, CreateTextStickyNoteRequest request)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        var noteId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        using var cmd = new NpgsqlCommand(
            @"INSERT INTO file_sticky_notes (id, repository_id, file_id, created_by_user_id, note_type, content, created_at, updated_at)
              VALUES (@id, @repoId, @fileId, @userId, 'text', @content, @now, @now)", conn);

        cmd.Parameters.AddWithValue("id", noteId);
        cmd.Parameters.AddWithValue("repoId", request.RepositoryId);
        cmd.Parameters.AddWithValue("fileId", request.FileId!);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("content", request.Content);
        cmd.Parameters.AddWithValue("now", now);

        await cmd.ExecuteNonQueryAsync();

        // Get user info for response
        var user = await GetUserInfoAsync(conn, userId);

        return new StickyNoteDto
        {
            Id = noteId,
            RepositoryId = request.RepositoryId,
            FileId = request.FileId,
            CreatedByUserId = userId,
            NoteType = "text",
            Content = request.Content,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUsername = user?.AuthorName ?? "Unknown",
            CreatedByAvatarUrl = user?.AvatarUrl
        };
    }

    public async Task<StickyNoteDto> CreateFileDocumentNoteAsync(Guid userId, Guid repositoryId, Guid fileId, Stream fileStream, string fileName)
    {
        // Upload to Supabase Storage
        var documentUrl = await _storageService.UploadFileAsync(fileStream, fileName, "file-note-dcouments");

        await using var conn = await _dataSource.OpenConnectionAsync();

        var noteId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var fileSize = fileStream.Length;

        using var cmd = new NpgsqlCommand(
            @"INSERT INTO file_sticky_notes (id, repository_id, file_id, created_by_user_id, note_type, document_url, document_name, document_size, created_at, updated_at)
              VALUES (@id, @repoId, @fileId, @userId, 'document', @docUrl, @docName, @docSize, @now, @now)", conn);

        cmd.Parameters.AddWithValue("id", noteId);
        cmd.Parameters.AddWithValue("repoId", repositoryId);
        cmd.Parameters.AddWithValue("fileId", fileId);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("docUrl", documentUrl);
        cmd.Parameters.AddWithValue("docName", fileName);
        cmd.Parameters.AddWithValue("docSize", fileSize);
        cmd.Parameters.AddWithValue("now", now);

        await cmd.ExecuteNonQueryAsync();

        var user = await GetUserInfoAsync(conn, userId);

        return new StickyNoteDto
        {
            Id = noteId,
            RepositoryId = repositoryId,
            FileId = fileId,
            CreatedByUserId = userId,
            NoteType = "document",
            DocumentUrl = documentUrl,
            DocumentName = fileName,
            DocumentSize = fileSize,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUsername = user?.AuthorName ?? "Unknown",
            CreatedByAvatarUrl = user?.AvatarUrl
        };
    }

    public async Task<StickyNoteDto?> UpdateStickyNoteAsync(Guid noteId, Guid userId, UpdateStickyNoteRequest request)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // Verify ownership
        using var checkCmd = new NpgsqlCommand(
            "SELECT created_by_user_id FROM file_sticky_notes WHERE id = @noteId", conn);
        checkCmd.Parameters.AddWithValue("noteId", noteId);
        var ownerId = await checkCmd.ExecuteScalarAsync();

        if (ownerId == null || (Guid)ownerId != userId)
        {
            // Try repo sticky notes
            using var checkRepoCmd = new NpgsqlCommand(
                "SELECT created_by_user_id FROM repo_sticky_notes WHERE id = @noteId", conn);
            checkRepoCmd.Parameters.AddWithValue("noteId", noteId);
            ownerId = await checkRepoCmd.ExecuteScalarAsync();

            if (ownerId == null || (Guid)ownerId != userId)
                return null;

            // Update repo note
            using var updateRepoCmd = new NpgsqlCommand(
                @"UPDATE repo_sticky_notes SET content = @content, tagged_file_ids = @taggedFiles, tagged_branch_ids = @taggedBranches, updated_at = @now WHERE id = @noteId", conn);
            updateRepoCmd.Parameters.AddWithValue("content", request.Content ?? (object)DBNull.Value);
            updateRepoCmd.Parameters.AddWithValue("taggedFiles", request.TaggedFileIds?.ToArray() ?? (object)DBNull.Value);
            updateRepoCmd.Parameters.AddWithValue("taggedBranches", request.TaggedBranchIds?.ToArray() ?? (object)DBNull.Value);
            updateRepoCmd.Parameters.AddWithValue("now", DateTime.UtcNow);
            updateRepoCmd.Parameters.AddWithValue("noteId", noteId);
            await updateRepoCmd.ExecuteNonQueryAsync();
        }
        else
        {
            // Update file note
            using var updateCmd = new NpgsqlCommand(
                @"UPDATE file_sticky_notes SET content = @content, updated_at = @now WHERE id = @noteId", conn);
            updateCmd.Parameters.AddWithValue("content", request.Content ?? (object)DBNull.Value);
            updateCmd.Parameters.AddWithValue("now", DateTime.UtcNow);
            updateCmd.Parameters.AddWithValue("noteId", noteId);
            await updateCmd.ExecuteNonQueryAsync();
        }

        return new StickyNoteDto { Id = noteId };
    }

    public async Task<bool> DeleteStickyNoteAsync(Guid noteId, Guid userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // Try file sticky notes first
        using var checkCmd = new NpgsqlCommand(
            "SELECT created_by_user_id, document_url FROM file_sticky_notes WHERE id = @noteId", conn);
        checkCmd.Parameters.AddWithValue("noteId", noteId);
        using var reader = await checkCmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            var ownerId = reader.GetGuid(0);
            var documentUrl = reader.IsDBNull(1) ? null : reader.GetString(1);
            reader.Close();

            if (ownerId != userId) return false;

            // Delete from storage if document
            if (!string.IsNullOrEmpty(documentUrl))
            {
                await _storageService.DeleteFileAsync(documentUrl, "file-note-dcouments");
            }

            using var deleteCmd = new NpgsqlCommand("DELETE FROM file_sticky_notes WHERE id = @noteId", conn);
            deleteCmd.Parameters.AddWithValue("noteId", noteId);
            await deleteCmd.ExecuteNonQueryAsync();
            return true;
        }

        reader.Close();

        // Try repo sticky notes
        using var checkRepoCmd = new NpgsqlCommand(
            "SELECT created_by_user_id, document_url FROM repo_sticky_notes WHERE id = @noteId", conn);
        checkRepoCmd.Parameters.AddWithValue("noteId", noteId);
        using var repoReader = await checkRepoCmd.ExecuteReaderAsync();

        if (await repoReader.ReadAsync())
        {
            var ownerId = repoReader.GetGuid(0);
            var documentUrl = repoReader.IsDBNull(1) ? null : repoReader.GetString(1);
            repoReader.Close();

            if (ownerId != userId) return false;

            if (!string.IsNullOrEmpty(documentUrl))
            {
                await _storageService.DeleteFileAsync(documentUrl, "file-note-dcouments");
            }

            using var deleteCmd = new NpgsqlCommand("DELETE FROM repo_sticky_notes WHERE id = @noteId", conn);
            deleteCmd.Parameters.AddWithValue("noteId", noteId);
            await deleteCmd.ExecuteNonQueryAsync();
            return true;
        }

        return false;
    }

    // ==========================================
    // FILE-LEVEL DISCUSSION THREADS
    // ==========================================

    public async Task<DiscussionThreadDto?> GetFileDiscussionThreadAsync(Guid fileId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // Get thread
        using var threadCmd = new NpgsqlCommand(
            @"SELECT id, repository_id, file_id, created_at FROM file_discussion_threads WHERE file_id = @fileId", conn);
        threadCmd.Parameters.AddWithValue("fileId", fileId);

        DiscussionThreadDto? thread = null;
        using var threadReader = await threadCmd.ExecuteReaderAsync();
        if (await threadReader.ReadAsync())
        {
            thread = new DiscussionThreadDto
            {
                Id = threadReader.GetGuid(0),
                RepositoryId = threadReader.GetGuid(1),
                FileId = threadReader.GetGuid(2),
                CreatedAt = threadReader.GetDateTime(3),
                Messages = new List<DiscussionMessageDto>()
            };
        }
        threadReader.Close();

        if (thread == null) return null;

        // Get messages
        using var msgCmd = new NpgsqlCommand(
            @"SELECT m.id, m.thread_id, m.user_id, m.message, m.mentioned_users, m.referenced_line_numbers, 
                     m.created_at, m.updated_at, u.author_name, u.avatar_url
              FROM file_discussion_messages m
              LEFT JOIN users u ON m.user_id = u.id
              WHERE m.thread_id = @threadId
              ORDER BY m.created_at ASC", conn);
        msgCmd.Parameters.AddWithValue("threadId", thread.Id);

        using var msgReader = await msgCmd.ExecuteReaderAsync();
        while (await msgReader.ReadAsync())
        {
            var mentionedUserIds = msgReader.IsDBNull(4) ? null : (Guid[])msgReader.GetValue(4);
            var lineNumbers = msgReader.IsDBNull(5) ? null : (int[])msgReader.GetValue(5);

            thread.Messages.Add(new DiscussionMessageDto
            {
                Id = msgReader.GetGuid(0),
                ThreadId = msgReader.GetGuid(1),
                UserId = msgReader.GetGuid(2),
                Message = msgReader.GetString(3),
                ReferencedLineNumbers = lineNumbers?.ToList(),
                CreatedAt = msgReader.GetDateTime(6),
                UpdatedAt = msgReader.GetDateTime(7),
                Username = msgReader.IsDBNull(8) ? "Unknown" : msgReader.GetString(8),
                AvatarUrl = msgReader.IsDBNull(9) ? null : msgReader.GetString(9)
            });
        }

        return thread;
    }

    public async Task<DiscussionMessageDto> PostFileDiscussionMessageAsync(Guid fileId, Guid userId, PostMessageRequest request)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // Get or create thread
        Guid threadId;
        Guid repositoryId;

        using var getThreadCmd = new NpgsqlCommand(
            "SELECT id, repository_id FROM file_discussion_threads WHERE file_id = @fileId", conn);
        getThreadCmd.Parameters.AddWithValue("fileId", fileId);
        using var threadReader = await getThreadCmd.ExecuteReaderAsync();

        if (await threadReader.ReadAsync())
        {
            threadId = threadReader.GetGuid(0);
            repositoryId = threadReader.GetGuid(1);
            threadReader.Close();
        }
        else
        {
            threadReader.Close();

            // Get repository_id from file
            repositoryId = await GetRepositoryIdFromFileAsync(fileId) ?? throw new Exception("File not found");

            threadId = Guid.NewGuid();
            using var createThreadCmd = new NpgsqlCommand(
                @"INSERT INTO file_discussion_threads (id, repository_id, file_id, created_at) 
                  VALUES (@id, @repoId, @fileId, @now)", conn);
            createThreadCmd.Parameters.AddWithValue("id", threadId);
            createThreadCmd.Parameters.AddWithValue("repoId", repositoryId);
            createThreadCmd.Parameters.AddWithValue("fileId", fileId);
            createThreadCmd.Parameters.AddWithValue("now", DateTime.UtcNow);
            await createThreadCmd.ExecuteNonQueryAsync();
        }

        // Parse mentions and line numbers
        var mentionedUserIds = await ParseMentionsAsync(conn, request.Message, repositoryId);
        var lineNumbers = ParseLineNumbers(request.Message);

        // Create message
        var messageId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        using var msgCmd = new NpgsqlCommand(
            @"INSERT INTO file_discussion_messages (id, thread_id, user_id, message, mentioned_users, referenced_line_numbers, created_at, updated_at)
              VALUES (@id, @threadId, @userId, @message, @mentions, @lines, @now, @now)", conn);

        msgCmd.Parameters.AddWithValue("id", messageId);
        msgCmd.Parameters.AddWithValue("threadId", threadId);
        msgCmd.Parameters.AddWithValue("userId", userId);
        msgCmd.Parameters.AddWithValue("message", request.Message);
        msgCmd.Parameters.AddWithValue("mentions", mentionedUserIds.Count > 0 ? mentionedUserIds.ToArray() : DBNull.Value);
        msgCmd.Parameters.AddWithValue("lines", lineNumbers.Count > 0 ? lineNumbers.ToArray() : DBNull.Value);
        msgCmd.Parameters.AddWithValue("now", now);

        await msgCmd.ExecuteNonQueryAsync();

        // Create notifications for mentioned users
        var user = await GetUserInfoAsync(conn, userId);
        foreach (var mentionedUserId in mentionedUserIds)
        {
            if (mentionedUserId != userId) // Don't notify yourself
            {
                await _notificationService.CreateMentionNotificationAsync(
                    mentionedUserId, userId, user?.AuthorName ?? "Someone",
                    fileId, repositoryId, $"/file/{fileId}?tab=notes");
            }
        }

        return new DiscussionMessageDto
        {
            Id = messageId,
            ThreadId = threadId,
            UserId = userId,
            Message = request.Message,
            ReferencedLineNumbers = lineNumbers,
            CreatedAt = now,
            UpdatedAt = now,
            Username = user?.AuthorName ?? "Unknown",
            AvatarUrl = user?.AvatarUrl
        };
    }

    public async Task<DiscussionMessageDto?> UpdateDiscussionMessageAsync(Guid messageId, Guid userId, UpdateMessageRequest request)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // Check ownership in file messages
        using var checkCmd = new NpgsqlCommand(
            "SELECT user_id FROM file_discussion_messages WHERE id = @messageId", conn);
        checkCmd.Parameters.AddWithValue("messageId", messageId);
        var ownerId = await checkCmd.ExecuteScalarAsync();

        if (ownerId == null || (Guid)ownerId != userId)
        {
            // Try repo messages
            using var checkRepoCmd = new NpgsqlCommand(
                "SELECT user_id FROM repo_discussion_messages WHERE id = @messageId", conn);
            checkRepoCmd.Parameters.AddWithValue("messageId", messageId);
            ownerId = await checkRepoCmd.ExecuteScalarAsync();

            if (ownerId == null || (Guid)ownerId != userId) return null;

            using var updateCmd = new NpgsqlCommand(
                "UPDATE repo_discussion_messages SET message = @message, updated_at = @now WHERE id = @messageId", conn);
            updateCmd.Parameters.AddWithValue("message", request.Message);
            updateCmd.Parameters.AddWithValue("now", DateTime.UtcNow);
            updateCmd.Parameters.AddWithValue("messageId", messageId);
            await updateCmd.ExecuteNonQueryAsync();
        }
        else
        {
            using var updateCmd = new NpgsqlCommand(
                "UPDATE file_discussion_messages SET message = @message, updated_at = @now WHERE id = @messageId", conn);
            updateCmd.Parameters.AddWithValue("message", request.Message);
            updateCmd.Parameters.AddWithValue("now", DateTime.UtcNow);
            updateCmd.Parameters.AddWithValue("messageId", messageId);
            await updateCmd.ExecuteNonQueryAsync();
        }

        return new DiscussionMessageDto { Id = messageId, Message = request.Message };
    }

    public async Task<bool> DeleteDiscussionMessageAsync(Guid messageId, Guid userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // Try file messages
        using var deleteCmd = new NpgsqlCommand(
            "DELETE FROM file_discussion_messages WHERE id = @messageId AND user_id = @userId", conn);
        deleteCmd.Parameters.AddWithValue("messageId", messageId);
        deleteCmd.Parameters.AddWithValue("userId", userId);
        var affected = await deleteCmd.ExecuteNonQueryAsync();

        if (affected > 0) return true;

        // Try repo messages
        using var deleteRepoCmd = new NpgsqlCommand(
            "DELETE FROM repo_discussion_messages WHERE id = @messageId AND user_id = @userId", conn);
        deleteRepoCmd.Parameters.AddWithValue("messageId", messageId);
        deleteRepoCmd.Parameters.AddWithValue("userId", userId);
        affected = await deleteRepoCmd.ExecuteNonQueryAsync();

        return affected > 0;
    }

    // ==========================================
    // FILE-LEVEL PERSONAL NOTES
    // ==========================================

    public async Task<List<PersonalNoteDto>> GetFilePersonalNotesAsync(Guid fileId, Guid userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            @"SELECT id, file_id, content, line_number, created_at, updated_at
              FROM file_personal_notes
              WHERE file_id = @fileId AND user_id = @userId
              ORDER BY line_number NULLS LAST, created_at DESC", conn);

        cmd.Parameters.AddWithValue("fileId", fileId);
        cmd.Parameters.AddWithValue("userId", userId);

        var notes = new List<PersonalNoteDto>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            notes.Add(new PersonalNoteDto
            {
                Id = reader.GetGuid(0),
                FileId = reader.GetGuid(1),
                Content = reader.GetString(2),
                LineNumber = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                CreatedAt = reader.GetDateTime(4),
                UpdatedAt = reader.GetDateTime(5)
            });
        }
        return notes;
    }

    public async Task<PersonalNoteDto> CreateFilePersonalNoteAsync(Guid userId, CreatePersonalNoteRequest request)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        var noteId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        using var cmd = new NpgsqlCommand(
            @"INSERT INTO file_personal_notes (id, file_id, user_id, content, line_number, created_at, updated_at)
              VALUES (@id, @fileId, @userId, @content, @lineNumber, @now, @now)", conn);

        cmd.Parameters.AddWithValue("id", noteId);
        cmd.Parameters.AddWithValue("fileId", request.FileId!);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("content", request.Content);
        cmd.Parameters.AddWithValue("lineNumber", request.LineNumber ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("now", now);

        await cmd.ExecuteNonQueryAsync();

        return new PersonalNoteDto
        {
            Id = noteId,
            FileId = request.FileId,
            Content = request.Content,
            LineNumber = request.LineNumber,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public async Task<PersonalNoteDto?> UpdatePersonalNoteAsync(Guid noteId, Guid userId, UpdatePersonalNoteRequest request)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // Try file personal notes
        using var updateCmd = new NpgsqlCommand(
            @"UPDATE file_personal_notes SET content = @content, line_number = @lineNumber, updated_at = @now 
              WHERE id = @noteId AND user_id = @userId", conn);
        updateCmd.Parameters.AddWithValue("content", request.Content);
        updateCmd.Parameters.AddWithValue("lineNumber", request.LineNumber ?? (object)DBNull.Value);
        updateCmd.Parameters.AddWithValue("now", DateTime.UtcNow);
        updateCmd.Parameters.AddWithValue("noteId", noteId);
        updateCmd.Parameters.AddWithValue("userId", userId);

        var affected = await updateCmd.ExecuteNonQueryAsync();

        if (affected > 0)
        {
            return new PersonalNoteDto { Id = noteId, Content = request.Content, LineNumber = request.LineNumber };
        }

        // Try repo personal notes
        using var updateRepoCmd = new NpgsqlCommand(
            @"UPDATE repo_personal_notes SET content = @content, tagged_file_ids = @taggedFiles, tagged_branch_ids = @taggedBranches, updated_at = @now 
              WHERE id = @noteId AND user_id = @userId", conn);
        updateRepoCmd.Parameters.AddWithValue("content", request.Content);
        updateRepoCmd.Parameters.AddWithValue("taggedFiles", request.TaggedFileIds?.ToArray() ?? (object)DBNull.Value);
        updateRepoCmd.Parameters.AddWithValue("taggedBranches", request.TaggedBranchIds?.ToArray() ?? (object)DBNull.Value);
        updateRepoCmd.Parameters.AddWithValue("now", DateTime.UtcNow);
        updateRepoCmd.Parameters.AddWithValue("noteId", noteId);
        updateRepoCmd.Parameters.AddWithValue("userId", userId);

        affected = await updateRepoCmd.ExecuteNonQueryAsync();

        return affected > 0 ? new PersonalNoteDto { Id = noteId, Content = request.Content } : null;
    }

    public async Task<bool> DeletePersonalNoteAsync(Guid noteId, Guid userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // Try file personal notes
        using var deleteCmd = new NpgsqlCommand(
            "DELETE FROM file_personal_notes WHERE id = @noteId AND user_id = @userId", conn);
        deleteCmd.Parameters.AddWithValue("noteId", noteId);
        deleteCmd.Parameters.AddWithValue("userId", userId);
        var affected = await deleteCmd.ExecuteNonQueryAsync();

        if (affected > 0) return true;

        // Try repo personal notes
        using var deleteRepoCmd = new NpgsqlCommand(
            "DELETE FROM repo_personal_notes WHERE id = @noteId AND user_id = @userId", conn);
        deleteRepoCmd.Parameters.AddWithValue("noteId", noteId);
        deleteRepoCmd.Parameters.AddWithValue("userId", userId);
        affected = await deleteRepoCmd.ExecuteNonQueryAsync();

        return affected > 0;
    }

    // ==========================================
    // REPOSITORY-LEVEL STICKY NOTES
    // ==========================================

    public async Task<List<StickyNoteDto>> GetRepoStickyNotesAsync(Guid repositoryId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            @"SELECT n.id, n.repository_id, n.created_by_user_id, n.note_type, 
   n.content, n.document_url, n.document_name, n.document_size, n.tagged_file_ids, n.tagged_branch_ids,
                     n.created_at, n.updated_at,
                     u.author_name, u.avatar_url
              FROM repo_sticky_notes n
              LEFT JOIN users u ON n.created_by_user_id = u.id
              WHERE n.repository_id = @repoId
              ORDER BY n.created_at DESC", conn);

        cmd.Parameters.AddWithValue("repoId", repositoryId);

        var notes = new List<StickyNoteDto>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var taggedFileIds = reader.IsDBNull(8) ? null : ((Guid[])reader.GetValue(8)).ToList();
            var taggedBranchIds = reader.IsDBNull(9) ? null : ((Guid[])reader.GetValue(9)).ToList();
            notes.Add(new StickyNoteDto
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                FileId = null, // repo-level
                CreatedByUserId = reader.GetGuid(2),
                NoteType = reader.GetString(3),
                Content = reader.IsDBNull(4) ? null : reader.GetString(4),
                DocumentUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
                DocumentName = reader.IsDBNull(6) ? null : reader.GetString(6),
                DocumentSize = reader.IsDBNull(7) ? null : reader.GetInt64(7),
                TaggedFileIds = taggedFileIds,
                TaggedBranchIds = taggedBranchIds,
                CreatedAt = reader.GetDateTime(10),
                UpdatedAt = reader.GetDateTime(11),
                CreatedByUsername = reader.IsDBNull(12) ? "Unknown" : reader.GetString(12),
                CreatedByAvatarUrl = reader.IsDBNull(13) ? null : reader.GetString(13)
            });
        }
        reader.Close();

        // Fetch tagged file and branch details for all notes
        foreach (var note in notes)
        {
            if (note.TaggedFileIds?.Count > 0)
                note.TaggedFiles = await GetFileInfoAsync(conn, note.TaggedFileIds);

            if (note.TaggedBranchIds?.Count > 0)
                note.TaggedBranches = await GetBranchInfoAsync(conn, note.TaggedBranchIds);
        }

        return notes;
    }

    public async Task<StickyNoteDto> CreateRepoStickyNoteAsync(Guid userId, CreateTextStickyNoteRequest request)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        var noteId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        using var cmd = new NpgsqlCommand(
            @"INSERT INTO repo_sticky_notes (id, repository_id, created_by_user_id, note_type, content, tagged_file_ids, tagged_branch_ids, created_at, updated_at)
              VALUES (@id, @repoId, @userId, 'text', @content, @taggedFiles, @taggedBranches, @now, @now)", conn);

        cmd.Parameters.AddWithValue("id", noteId);
        cmd.Parameters.AddWithValue("repoId", request.RepositoryId);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("content", request.Content);
        cmd.Parameters.AddWithValue("taggedFiles", request.TaggedFileIds?.ToArray() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("taggedBranches", request.TaggedBranchIds?.ToArray() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("now", now);

        await cmd.ExecuteNonQueryAsync();

        var user = await GetUserInfoAsync(conn, userId);

        // Fetch tagged file and branch info
        List<TaggedFileDto>? taggedFiles = null;
        List<TaggedBranchDto>? taggedBranches = null;

        if (request.TaggedFileIds?.Count > 0)
            taggedFiles = await GetFileInfoAsync(conn, request.TaggedFileIds);

        if (request.TaggedBranchIds?.Count > 0)
            taggedBranches = await GetBranchInfoAsync(conn, request.TaggedBranchIds);

        return new StickyNoteDto
        {
            Id = noteId,
            RepositoryId = request.RepositoryId,
            FileId = null,
            CreatedByUserId = userId,
            NoteType = "text",
            Content = request.Content,
            TaggedFileIds = request.TaggedFileIds,
            TaggedFiles = taggedFiles,
            TaggedBranchIds = request.TaggedBranchIds,
            TaggedBranches = taggedBranches,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUsername = user?.AuthorName ?? "Unknown",
            CreatedByAvatarUrl = user?.AvatarUrl
        };
    }

    public async Task<StickyNoteDto> CreateRepoDocumentNoteAsync(Guid userId, Guid repositoryId, Stream fileStream, string fileName, List<Guid>? taggedFileIds)
    {
        var documentUrl = await _storageService.UploadFileAsync(fileStream, fileName, "file-note-dcouments");

        await using var conn = await _dataSource.OpenConnectionAsync();

        var noteId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var fileSize = fileStream.Length;

        using var cmd = new NpgsqlCommand(
            @"INSERT INTO repo_sticky_notes (id, repository_id, created_by_user_id, note_type, document_url, document_name, document_size, tagged_file_ids, tagged_branch_ids, created_at, updated_at)
              VALUES (@id, @repoId, @userId, 'document', @docUrl, @docName, @docSize, @taggedFiles, @taggedBranches, @now, @now)", conn);

        cmd.Parameters.AddWithValue("id", noteId);
        cmd.Parameters.AddWithValue("repoId", repositoryId);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("docUrl", documentUrl);
        cmd.Parameters.AddWithValue("docName", fileName);
        cmd.Parameters.AddWithValue("docSize", fileSize);
        cmd.Parameters.AddWithValue("taggedFiles", taggedFileIds?.ToArray() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("taggedBranches", (object)DBNull.Value);
        cmd.Parameters.AddWithValue("now", now);

        await cmd.ExecuteNonQueryAsync();

        var user = await GetUserInfoAsync(conn, userId);

        return new StickyNoteDto
        {
            Id = noteId,
            RepositoryId = repositoryId,
            FileId = null,
            CreatedByUserId = userId,
            NoteType = "document",
            DocumentUrl = documentUrl,
            DocumentName = fileName,
            DocumentSize = fileSize,
            TaggedFileIds = taggedFileIds,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUsername = user?.AuthorName ?? "Unknown",
            CreatedByAvatarUrl = user?.AvatarUrl
        };
    }

    // ==========================================
    // REPOSITORY-LEVEL DISCUSSION THREADS
    // ==========================================

    public async Task<DiscussionThreadDto?> GetRepoDiscussionThreadAsync(Guid repositoryId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var threadCmd = new NpgsqlCommand(
            @"SELECT id, repository_id, created_at FROM repo_discussion_threads WHERE repository_id = @repoId", conn);
        threadCmd.Parameters.AddWithValue("repoId", repositoryId);

        DiscussionThreadDto? thread = null;
        using var threadReader = await threadCmd.ExecuteReaderAsync();
        if (await threadReader.ReadAsync())
        {
            thread = new DiscussionThreadDto
            {
                Id = threadReader.GetGuid(0),
                RepositoryId = threadReader.GetGuid(1),
                FileId = null,
                CreatedAt = threadReader.GetDateTime(2),
                Messages = new List<DiscussionMessageDto>()
            };
        }
        threadReader.Close();

        if (thread == null) return null;

        using var msgCmd = new NpgsqlCommand(
            @"SELECT m.id, m.thread_id, m.user_id, m.message, m.mentioned_users, m.tagged_file_ids, m.tagged_branch_ids,
                     m.created_at, m.updated_at, u.author_name, u.avatar_url
              FROM repo_discussion_messages m
              LEFT JOIN users u ON m.user_id = u.id
              WHERE m.thread_id = @threadId
              ORDER BY m.created_at ASC", conn);
        msgCmd.Parameters.AddWithValue("threadId", thread.Id);

        var messages = new List<DiscussionMessageDto>();
        using var msgReader = await msgCmd.ExecuteReaderAsync();
        while (await msgReader.ReadAsync())
        {
            var taggedFileIds = msgReader.IsDBNull(5) ? null : ((Guid[])msgReader.GetValue(5)).ToList();
            var taggedBranchIds = msgReader.IsDBNull(6) ? null : ((Guid[])msgReader.GetValue(6)).ToList();

            messages.Add(new DiscussionMessageDto
            {
                Id = msgReader.GetGuid(0),
                ThreadId = msgReader.GetGuid(1),
                UserId = msgReader.GetGuid(2),
                Message = msgReader.GetString(3),
                TaggedFileIds = taggedFileIds,
                TaggedBranchIds = taggedBranchIds,
                CreatedAt = msgReader.GetDateTime(7),
                UpdatedAt = msgReader.GetDateTime(8),
                Username = msgReader.IsDBNull(9) ? "Unknown" : msgReader.GetString(9),
                AvatarUrl = msgReader.IsDBNull(10) ? null : msgReader.GetString(10)
            });
        }
        msgReader.Close();

        // Fetch tagged files and branches for all messages
        foreach (var msg in messages)
        {
            if (msg.TaggedFileIds?.Count > 0)
                msg.TaggedFiles = await GetFileInfoAsync(conn, msg.TaggedFileIds);

            if (msg.TaggedBranchIds?.Count > 0)
                msg.TaggedBranches = await GetBranchInfoAsync(conn, msg.TaggedBranchIds);
        }

        thread.Messages = messages;
        return thread;
    }

    public async Task<DiscussionMessageDto> PostRepoDiscussionMessageAsync(Guid repositoryId, Guid userId, PostMessageRequest request)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // Get or create thread
        Guid threadId;

        using var getThreadCmd = new NpgsqlCommand(
            "SELECT id FROM repo_discussion_threads WHERE repository_id = @repoId", conn);
        getThreadCmd.Parameters.AddWithValue("repoId", repositoryId);
        var existingThreadId = await getThreadCmd.ExecuteScalarAsync();

        if (existingThreadId != null)
        {
            threadId = (Guid)existingThreadId;
        }
        else
        {
            threadId = Guid.NewGuid();
            using var createThreadCmd = new NpgsqlCommand(
                @"INSERT INTO repo_discussion_threads (id, repository_id, created_at) 
                  VALUES (@id, @repoId, @now)", conn);
            createThreadCmd.Parameters.AddWithValue("id", threadId);
            createThreadCmd.Parameters.AddWithValue("repoId", repositoryId);
            createThreadCmd.Parameters.AddWithValue("now", DateTime.UtcNow);
            await createThreadCmd.ExecuteNonQueryAsync();
        }

        // Parse mentions
        var mentionedUserIds = await ParseMentionsAsync(conn, request.Message, repositoryId);

        var messageId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        using var msgCmd = new NpgsqlCommand(
            @"INSERT INTO repo_discussion_messages (id, thread_id, user_id, message, mentioned_users, tagged_file_ids, tagged_branch_ids, created_at, updated_at)
              VALUES (@id, @threadId, @userId, @message, @mentions, @taggedFiles, @taggedBranches, @now, @now)", conn);

        msgCmd.Parameters.AddWithValue("id", messageId);
        msgCmd.Parameters.AddWithValue("threadId", threadId);
        msgCmd.Parameters.AddWithValue("userId", userId);
        msgCmd.Parameters.AddWithValue("message", request.Message);
        msgCmd.Parameters.AddWithValue("mentions", mentionedUserIds.Count > 0 ? mentionedUserIds.ToArray() : DBNull.Value);
        msgCmd.Parameters.AddWithValue("taggedFiles", request.TaggedFileIds?.ToArray() ?? (object)DBNull.Value);
        msgCmd.Parameters.AddWithValue("taggedBranches", request.TaggedBranchIds?.ToArray() ?? (object)DBNull.Value);
        msgCmd.Parameters.AddWithValue("now", now);

        await msgCmd.ExecuteNonQueryAsync();

        // Create notifications for mentioned users
        var user = await GetUserInfoAsync(conn, userId);
        foreach (var mentionedUserId in mentionedUserIds)
        {
            if (mentionedUserId != userId)
            {
                await _notificationService.CreateMentionNotificationAsync(
                    mentionedUserId, userId, user?.AuthorName ?? "Someone",
                    null, repositoryId, $"/repo/{repositoryId}?tab=notes");
            }
        }

        // Fetch tagged files and branches
        List<TaggedFileDto>? taggedFiles = null;
        List<TaggedBranchDto>? taggedBranches = null;

        if (request.TaggedFileIds?.Count > 0)
            taggedFiles = await GetFileInfoAsync(conn, request.TaggedFileIds);

        if (request.TaggedBranchIds?.Count > 0)
            taggedBranches = await GetBranchInfoAsync(conn, request.TaggedBranchIds);

        return new DiscussionMessageDto
        {
            Id = messageId,
            ThreadId = threadId,
            UserId = userId,
            Message = request.Message,
            TaggedFileIds = request.TaggedFileIds,
            TaggedFiles = taggedFiles,
            TaggedBranchIds = request.TaggedBranchIds,
            TaggedBranches = taggedBranches,
            CreatedAt = now,
            UpdatedAt = now,
            Username = user?.AuthorName ?? "Unknown",
            AvatarUrl = user?.AvatarUrl
        };
    }

    // ==========================================
    // REPOSITORY-LEVEL PERSONAL NOTES
    // ==========================================

    public async Task<List<PersonalNoteDto>> GetRepoPersonalNotesAsync(Guid repositoryId, Guid userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            @"SELECT id, repository_id, content, tagged_file_ids, tagged_branch_ids, created_at, updated_at
              FROM repo_personal_notes
              WHERE repository_id = @repoId AND user_id = @userId
              ORDER BY created_at DESC", conn);

        cmd.Parameters.AddWithValue("repoId", repositoryId);
        cmd.Parameters.AddWithValue("userId", userId);

        var notes = new List<PersonalNoteDto>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var taggedFileIds = reader.IsDBNull(3) ? null : ((Guid[])reader.GetValue(3)).ToList();
            var taggedBranchIds = reader.IsDBNull(4) ? null : ((Guid[])reader.GetValue(4)).ToList();

            notes.Add(new PersonalNoteDto
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                Content = reader.GetString(2),
                TaggedFileIds = taggedFileIds,
                TaggedBranchIds = taggedBranchIds,
                CreatedAt = reader.GetDateTime(5),
                UpdatedAt = reader.GetDateTime(6)
            });
        }
        reader.Close();

        // Fetch tagged files and branches for all notes
        foreach (var note in notes)
        {
            if (note.TaggedFileIds?.Count > 0)
                note.TaggedFiles = await GetFileInfoAsync(conn, note.TaggedFileIds);

            if (note.TaggedBranchIds?.Count > 0)
                note.TaggedBranches = await GetBranchInfoAsync(conn, note.TaggedBranchIds);
        }

        return notes;
    }

    public async Task<PersonalNoteDto> CreateRepoPersonalNoteAsync(Guid userId, CreatePersonalNoteRequest request)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        var noteId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        using var cmd = new NpgsqlCommand(
            @"INSERT INTO repo_personal_notes (id, repository_id, user_id, content, tagged_file_ids, tagged_branch_ids, created_at, updated_at)
              VALUES (@id, @repoId, @userId, @content, @taggedFiles, @taggedBranches, @now, @now)", conn);

        cmd.Parameters.AddWithValue("id", noteId);
        cmd.Parameters.AddWithValue("repoId", request.RepositoryId!);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("content", request.Content);
        cmd.Parameters.AddWithValue("taggedFiles", request.TaggedFileIds?.ToArray() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("taggedBranches", request.TaggedBranchIds?.ToArray() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("now", now);

        await cmd.ExecuteNonQueryAsync();

        // Fetch tagged files and branches
        List<TaggedFileDto>? taggedFiles = null;
        List<TaggedBranchDto>? taggedBranches = null;

        if (request.TaggedFileIds?.Count > 0)
            taggedFiles = await GetFileInfoAsync(conn, request.TaggedFileIds);

        if (request.TaggedBranchIds?.Count > 0)
            taggedBranches = await GetBranchInfoAsync(conn, request.TaggedBranchIds);

        return new PersonalNoteDto
        {
            Id = noteId,
            RepositoryId = request.RepositoryId,
            Content = request.Content,
            TaggedFileIds = request.TaggedFileIds,
            TaggedFiles = taggedFiles,
            TaggedBranchIds = request.TaggedBranchIds,
            TaggedBranches = taggedBranches,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    // ==========================================
    // HELPERS
    // ==========================================

    public async Task<Guid?> GetRepositoryIdFromFileAsync(Guid fileId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT repository_id FROM repository_files WHERE id = @fileId", conn);
        cmd.Parameters.AddWithValue("fileId", fileId);

        var result = await cmd.ExecuteScalarAsync();
        return result as Guid?;
    }

    public async Task<List<MentionedUserDto>> GetUsersWithRepoAccessAsync(Guid repositoryId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            @"SELECT u.id, u.author_name, u.avatar_url 
              FROM users u
              INNER JOIN repository_user_access rua ON u.id = rua.user_id
              WHERE rua.repository_id = @repoId", conn);
        cmd.Parameters.AddWithValue("repoId", repositoryId);

        var users = new List<MentionedUserDto>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            users.Add(new MentionedUserDto
            {
                Id = reader.GetGuid(0),
                Username = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1),
                AvatarUrl = reader.IsDBNull(2) ? null : reader.GetString(2)
            });
        }
        return users;
    }

    private async Task<User?> GetUserInfoAsync(NpgsqlConnection conn, Guid userId)
    {
        using var cmd = new NpgsqlCommand(
            "SELECT id, github_id, author_name, email, avatar_url FROM users WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", userId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new User
            {
                Id = reader.GetGuid(0),
                GithubId = reader.GetInt64(1),
                AuthorName = reader.GetString(2),
                Email = reader.IsDBNull(3) ? null : reader.GetString(3),
                AvatarUrl = reader.IsDBNull(4) ? null : reader.GetString(4)
            };
        }
        return null;
    }

    private async Task<List<Guid>> ParseMentionsAsync(NpgsqlConnection conn, string message, Guid repositoryId)
    {
        // Find @username patterns (supports letters, numbers, hyphens, underscores)
        // Using explicit character class with escaped hyphen for clarity
        var matches = Regex.Matches(message, @"@([\w\-]+)");
        if (matches.Count == 0) return new List<Guid>();

        var usernames = matches.Select(m => m.Groups[1].Value).Distinct().ToList();
        _logger.LogInformation($"ParseMentions: Found {usernames.Count} mentions in message: {string.Join(", ", usernames)}");
        
        var userIds = new List<Guid>();

        foreach (var username in usernames)
        {
            _logger.LogInformation($"ParseMentions: Looking up user '{username}' in repository {repositoryId}");
            
            using var cmd = new NpgsqlCommand(
                @"SELECT u.id FROM users u
                  INNER JOIN repository_user_access rua ON u.id = rua.user_id
                  WHERE rua.repository_id = @repoId AND LOWER(u.author_name) = LOWER(@username)", conn);
            cmd.Parameters.AddWithValue("repoId", repositoryId);
            cmd.Parameters.AddWithValue("username", username);

            var userId = await cmd.ExecuteScalarAsync();
            if (userId != null)
            {
                _logger.LogInformation($"ParseMentions: Found user {username} with ID {userId}");
                userIds.Add((Guid)userId);
            }
            else
            {
                _logger.LogWarning($"ParseMentions: User '{username}' not found in repository {repositoryId}");
            }
        }

        return userIds;
    }

    private List<int> ParseLineNumbers(string message)
    {
        // Find #L123 or #L123-145 patterns
        var matches = Regex.Matches(message, @"#L(\d+)(?:-(\d+))?");
        var lineNumbers = new List<int>();

        foreach (Match match in matches)
        {
            var startLine = int.Parse(match.Groups[1].Value);
            lineNumbers.Add(startLine);

            if (match.Groups[2].Success)
            {
                var endLine = int.Parse(match.Groups[2].Value);
                for (int i = startLine + 1; i <= endLine; i++)
                {
                    lineNumbers.Add(i);
                }
            }
        }

        return lineNumbers.Distinct().OrderBy(x => x).ToList();
    }

    private async Task<List<TaggedFileDto>> GetFileInfoAsync(NpgsqlConnection conn, List<Guid> fileIds)
    {
        if (fileIds == null || fileIds.Count == 0)
            return new List<TaggedFileDto>();

        using var cmd = new NpgsqlCommand(
            "SELECT id, file_path FROM repository_files WHERE id = ANY(@ids)", conn);
        cmd.Parameters.AddWithValue("ids", fileIds.ToArray());

        var files = new List<TaggedFileDto>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            files.Add(new TaggedFileDto
            {
                Id = reader.GetGuid(0),
                FilePath = reader.GetString(1)
            });
        }
        return files;
    }

    private async Task<List<TaggedBranchDto>> GetBranchInfoAsync(NpgsqlConnection conn, List<Guid> branchIds)
    {
        if (branchIds == null || branchIds.Count == 0)
            return new List<TaggedBranchDto>();

        using var cmd = new NpgsqlCommand(
            "SELECT id, name, is_default FROM branches WHERE id = ANY(@ids)", conn);
        cmd.Parameters.AddWithValue("ids", branchIds.ToArray());

        var branches = new List<TaggedBranchDto>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            branches.Add(new TaggedBranchDto
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                IsDefault = reader.GetBoolean(2)
            });
        }
        return branches;
    }
}
