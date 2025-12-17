using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Core.Interfaces;

/// <summary>
/// Service for managing file-level and repository-level notes.
/// </summary>
public interface INotesService
{
    // ==========================================
    // FILE-LEVEL STICKY NOTES
    // ==========================================
    Task<List<StickyNoteDto>> GetFileStickyNotesAsync(Guid fileId);
    Task<StickyNoteDto> CreateFileStickyNoteAsync(Guid userId, CreateTextStickyNoteRequest request);
    Task<StickyNoteDto> CreateFileDocumentNoteAsync(Guid userId, Guid repositoryId, Guid fileId, Stream fileStream, string fileName);
    Task<StickyNoteDto?> UpdateStickyNoteAsync(Guid noteId, Guid userId, UpdateStickyNoteRequest request);
    Task<bool> DeleteStickyNoteAsync(Guid noteId, Guid userId);

    // ==========================================
    // FILE-LEVEL DISCUSSION THREADS
    // ==========================================
    Task<DiscussionThreadDto?> GetFileDiscussionThreadAsync(Guid fileId);
    Task<DiscussionMessageDto> PostFileDiscussionMessageAsync(Guid fileId, Guid userId, PostMessageRequest request);
    Task<DiscussionMessageDto?> UpdateDiscussionMessageAsync(Guid messageId, Guid userId, UpdateMessageRequest request);
    Task<bool> DeleteDiscussionMessageAsync(Guid messageId, Guid userId);

    // ==========================================
    // FILE-LEVEL PERSONAL NOTES
    // ==========================================
    Task<List<PersonalNoteDto>> GetFilePersonalNotesAsync(Guid fileId, Guid userId);
    Task<PersonalNoteDto> CreateFilePersonalNoteAsync(Guid userId, CreatePersonalNoteRequest request);
    Task<PersonalNoteDto?> UpdatePersonalNoteAsync(Guid noteId, Guid userId, UpdatePersonalNoteRequest request);
    Task<bool> DeletePersonalNoteAsync(Guid noteId, Guid userId);

    // ==========================================
    // REPOSITORY-LEVEL STICKY NOTES
    // ==========================================
    Task<List<StickyNoteDto>> GetRepoStickyNotesAsync(Guid repositoryId);
    Task<StickyNoteDto> CreateRepoStickyNoteAsync(Guid userId, CreateTextStickyNoteRequest request);
    Task<StickyNoteDto> CreateRepoDocumentNoteAsync(Guid userId, Guid repositoryId, Stream fileStream, string fileName, List<Guid>? taggedFileIds);

    // ==========================================
    // REPOSITORY-LEVEL DISCUSSION THREADS
    // ==========================================
    Task<DiscussionThreadDto?> GetRepoDiscussionThreadAsync(Guid repositoryId);
    Task<DiscussionMessageDto> PostRepoDiscussionMessageAsync(Guid repositoryId, Guid userId, PostMessageRequest request);

    // ==========================================
    // REPOSITORY-LEVEL PERSONAL NOTES
    // ==========================================
    Task<List<PersonalNoteDto>> GetRepoPersonalNotesAsync(Guid repositoryId, Guid userId);
    Task<PersonalNoteDto> CreateRepoPersonalNoteAsync(Guid userId, CreatePersonalNoteRequest request);

    // ==========================================
    // HELPERS
    // ==========================================
    Task<Guid?> GetRepositoryIdFromFileAsync(Guid fileId);
    Task<List<MentionedUserDto>> GetUsersWithRepoAccessAsync(Guid repositoryId);
}
