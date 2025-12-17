using Microsoft.AspNetCore.Mvc;
using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Controllers;

/// <summary>
/// Controller for file-level and repository-level notes.
/// </summary>
[ApiController]
[Route("api/notes")]
public class NotesController : ControllerBase
{
    private readonly INotesService _notesService;
    private readonly ILogger<NotesController> _logger;

    public NotesController(INotesService notesService, ILogger<NotesController> logger)
    {
        _notesService = notesService;
        _logger = logger;
    }

    // ==========================================
    // FILE-LEVEL STICKY NOTES
    // ==========================================

    /// <summary>
    /// Get all sticky notes for a file.
    /// </summary>
    [HttpGet("file/sticky/{fileId}")]
    public async Task<ActionResult<List<StickyNoteDto>>> GetFileStickyNotes(Guid fileId)
    {
        try
        {
            var notes = await _notesService.GetFileStickyNotesAsync(fileId);
            return Ok(notes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file sticky notes for file {FileId}", fileId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create a text sticky note for a file.
    /// </summary>
    [HttpPost("file/sticky/text")]
    public async Task<ActionResult<StickyNoteDto>> CreateFileStickyNote([FromQuery] Guid userId, [FromBody] CreateTextStickyNoteRequest request)
    {
        try
        {
            if (request.FileId == null)
            {
                return BadRequest(new { error = "FileId is required for file-level sticky notes" });
            }

            var note = await _notesService.CreateFileStickyNoteAsync(userId, request);
            return Ok(note);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating file sticky note");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Upload a document sticky note for a file.
    /// </summary>
    [HttpPost("file/sticky/document")]
    public async Task<ActionResult<StickyNoteDto>> CreateFileDocumentNote(
        [FromQuery] Guid userId,
        [FromQuery] Guid repositoryId,
        [FromQuery] Guid fileId,
        IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "File is required" });
            }

            if (file.Length > 50 * 1024 * 1024) // 50MB limit
            {
                return BadRequest(new { error = "File size exceeds 50MB limit" });
            }

            using var stream = file.OpenReadStream();
            var note = await _notesService.CreateFileDocumentNoteAsync(userId, repositoryId, fileId, stream, file.FileName);
            return Ok(note);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating file document note");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update a sticky note.
    /// </summary>
    [HttpPut("sticky/{noteId}")]
    public async Task<ActionResult<StickyNoteDto>> UpdateStickyNote(Guid noteId, [FromQuery] Guid userId, [FromBody] UpdateStickyNoteRequest request)
    {
        try
        {
            var note = await _notesService.UpdateStickyNoteAsync(noteId, userId, request);
            if (note == null)
            {
                return NotFound(new { error = "Note not found or you don't have permission to edit it" });
            }
            return Ok(note);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating sticky note {NoteId}", noteId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a sticky note.
    /// </summary>
    [HttpDelete("sticky/{noteId}")]
    public async Task<ActionResult> DeleteStickyNote(Guid noteId, [FromQuery] Guid userId)
    {
        try
        {
            var deleted = await _notesService.DeleteStickyNoteAsync(noteId, userId);
            if (!deleted)
            {
                return NotFound(new { error = "Note not found or you don't have permission to delete it" });
            }
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting sticky note {NoteId}", noteId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ==========================================
    // FILE-LEVEL DISCUSSION THREADS
    // ==========================================

    /// <summary>
    /// Get discussion thread and messages for a file.
    /// </summary>
    [HttpGet("file/discussion/{fileId}")]
    public async Task<ActionResult<DiscussionThreadDto>> GetFileDiscussionThread(Guid fileId)
    {
        try
        {
            var thread = await _notesService.GetFileDiscussionThreadAsync(fileId);
            if (thread == null)
            {
                // Return empty thread structure if none exists yet
                return Ok(new DiscussionThreadDto { FileId = fileId, Messages = new List<DiscussionMessageDto>() });
            }
            return Ok(thread);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file discussion thread for file {FileId}", fileId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Post a message to a file's discussion thread.
    /// </summary>
    [HttpPost("file/discussion/{fileId}/messages")]
    public async Task<ActionResult<DiscussionMessageDto>> PostFileDiscussionMessage(Guid fileId, [FromQuery] Guid userId, [FromBody] PostMessageRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { error = "Message cannot be empty" });
            }

            var message = await _notesService.PostFileDiscussionMessageAsync(fileId, userId, request);
            return Ok(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting file discussion message for file {FileId}", fileId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update a discussion message.
    /// </summary>
    [HttpPut("discussion/messages/{messageId}")]
    public async Task<ActionResult<DiscussionMessageDto>> UpdateDiscussionMessage(Guid messageId, [FromQuery] Guid userId, [FromBody] UpdateMessageRequest request)
    {
        try
        {
            var message = await _notesService.UpdateDiscussionMessageAsync(messageId, userId, request);
            if (message == null)
            {
                return NotFound(new { error = "Message not found or you don't have permission to edit it" });
            }
            return Ok(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating discussion message {MessageId}", messageId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a discussion message.
    /// </summary>
    [HttpDelete("discussion/messages/{messageId}")]
    public async Task<ActionResult> DeleteDiscussionMessage(Guid messageId, [FromQuery] Guid userId)
    {
        try
        {
            var deleted = await _notesService.DeleteDiscussionMessageAsync(messageId, userId);
            if (!deleted)
            {
                return NotFound(new { error = "Message not found or you don't have permission to delete it" });
            }
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting discussion message {MessageId}", messageId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ==========================================
    // FILE-LEVEL PERSONAL NOTES
    // ==========================================

    /// <summary>
    /// Get personal notes for a file (only user's own notes).
    /// </summary>
    [HttpGet("file/personal/{fileId}")]
    public async Task<ActionResult<List<PersonalNoteDto>>> GetFilePersonalNotes(Guid fileId, [FromQuery] Guid userId)
    {
        try
        {
            var notes = await _notesService.GetFilePersonalNotesAsync(fileId, userId);
            return Ok(notes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file personal notes for file {FileId}", fileId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create a personal note for a file.
    /// </summary>
    [HttpPost("file/personal")]
    public async Task<ActionResult<PersonalNoteDto>> CreateFilePersonalNote([FromQuery] Guid userId, [FromBody] CreatePersonalNoteRequest request)
    {
        try
        {
            if (request.FileId == null)
            {
                return BadRequest(new { error = "FileId is required for file-level personal notes" });
            }

            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(new { error = "Content cannot be empty" });
            }

            var note = await _notesService.CreateFilePersonalNoteAsync(userId, request);
            return Ok(note);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating file personal note");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update a personal note.
    /// </summary>
    [HttpPut("personal/{noteId}")]
    public async Task<ActionResult<PersonalNoteDto>> UpdatePersonalNote(Guid noteId, [FromQuery] Guid userId, [FromBody] UpdatePersonalNoteRequest request)
    {
        try
        {
            var note = await _notesService.UpdatePersonalNoteAsync(noteId, userId, request);
            if (note == null)
            {
                return NotFound(new { error = "Note not found or you don't have permission to edit it" });
            }
            return Ok(note);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating personal note {NoteId}", noteId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a personal note.
    /// </summary>
    [HttpDelete("personal/{noteId}")]
    public async Task<ActionResult> DeletePersonalNote(Guid noteId, [FromQuery] Guid userId)
    {
        try
        {
            var deleted = await _notesService.DeletePersonalNoteAsync(noteId, userId);
            if (!deleted)
            {
                return NotFound(new { error = "Note not found or you don't have permission to delete it" });
            }
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting personal note {NoteId}", noteId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ==========================================
    // REPOSITORY-LEVEL STICKY NOTES
    // ==========================================

    /// <summary>
    /// Get all sticky notes for a repository.
    /// </summary>
    [HttpGet("repo/sticky/{repositoryId}")]
    public async Task<ActionResult<List<StickyNoteDto>>> GetRepoStickyNotes(Guid repositoryId)
    {
        try
        {
            var notes = await _notesService.GetRepoStickyNotesAsync(repositoryId);
            return Ok(notes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting repo sticky notes for repo {RepositoryId}", repositoryId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create a text sticky note for a repository.
    /// </summary>
    [HttpPost("repo/sticky/text")]
    public async Task<ActionResult<StickyNoteDto>> CreateRepoStickyNote([FromQuery] Guid userId, [FromBody] CreateTextStickyNoteRequest request)
    {
        try
        {
            var note = await _notesService.CreateRepoStickyNoteAsync(userId, request);
            return Ok(note);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating repo sticky note");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Upload a document sticky note for a repository.
    /// </summary>
    [HttpPost("repo/sticky/document")]
    public async Task<ActionResult<StickyNoteDto>> CreateRepoDocumentNote(
        [FromQuery] Guid userId,
        [FromQuery] Guid repositoryId,
        [FromQuery] string? taggedFileIds,
        IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "File is required" });
            }

            if (file.Length > 50 * 1024 * 1024) // 50MB limit
            {
                return BadRequest(new { error = "File size exceeds 50MB limit" });
            }

            List<Guid>? taggedFiles = null;
            if (!string.IsNullOrEmpty(taggedFileIds))
            {
                taggedFiles = taggedFileIds.Split(',').Select(Guid.Parse).ToList();
            }

            using var stream = file.OpenReadStream();
            var note = await _notesService.CreateRepoDocumentNoteAsync(userId, repositoryId, stream, file.FileName, taggedFiles);
            return Ok(note);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating repo document note");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ==========================================
    // REPOSITORY-LEVEL DISCUSSION THREADS
    // ==========================================

    /// <summary>
    /// Get discussion thread and messages for a repository.
    /// </summary>
    [HttpGet("repo/discussion/{repositoryId}")]
    public async Task<ActionResult<DiscussionThreadDto>> GetRepoDiscussionThread(Guid repositoryId)
    {
        try
        {
            var thread = await _notesService.GetRepoDiscussionThreadAsync(repositoryId);
            if (thread == null)
            {
                return Ok(new DiscussionThreadDto { RepositoryId = repositoryId, Messages = new List<DiscussionMessageDto>() });
            }
            return Ok(thread);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting repo discussion thread for repo {RepositoryId}", repositoryId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Post a message to a repository's discussion thread.
    /// </summary>
    [HttpPost("repo/discussion/{repositoryId}/messages")]
    public async Task<ActionResult<DiscussionMessageDto>> PostRepoDiscussionMessage(Guid repositoryId, [FromQuery] Guid userId, [FromBody] PostMessageRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { error = "Message cannot be empty" });
            }

            var message = await _notesService.PostRepoDiscussionMessageAsync(repositoryId, userId, request);
            return Ok(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting repo discussion message for repo {RepositoryId}", repositoryId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ==========================================
    // REPOSITORY-LEVEL PERSONAL NOTES
    // ==========================================

    /// <summary>
    /// Get personal notes for a repository (only user's own notes).
    /// </summary>
    [HttpGet("repo/personal/{repositoryId}")]
    public async Task<ActionResult<List<PersonalNoteDto>>> GetRepoPersonalNotes(Guid repositoryId, [FromQuery] Guid userId)
    {
        try
        {
            var notes = await _notesService.GetRepoPersonalNotesAsync(repositoryId, userId);
            return Ok(notes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting repo personal notes for repo {RepositoryId}", repositoryId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create a personal note for a repository.
    /// </summary>
    [HttpPost("repo/personal")]
    public async Task<ActionResult<PersonalNoteDto>> CreateRepoPersonalNote([FromQuery] Guid userId, [FromBody] CreatePersonalNoteRequest request)
    {
        try
        {
            if (request.RepositoryId == null)
            {
                return BadRequest(new { error = "RepositoryId is required for repo-level personal notes" });
            }

            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(new { error = "Content cannot be empty" });
            }

            var note = await _notesService.CreateRepoPersonalNoteAsync(userId, request);
            return Ok(note);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating repo personal note");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ==========================================
    // HELPERS
    // ==========================================

    /// <summary>
    /// Get users with access to a repository (for @mention autocomplete).
    /// </summary>
    [HttpGet("users/{repositoryId}")]
    public async Task<ActionResult<List<MentionedUserDto>>> GetUsersWithRepoAccess(Guid repositoryId)
    {
        try
        {
            var users = await _notesService.GetUsersWithRepoAccessAsync(repositoryId);
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users with repo access for repo {RepositoryId}", repositoryId);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
