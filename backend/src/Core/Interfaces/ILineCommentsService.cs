using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Core.Interfaces;

/// <summary>
/// Service interface for managing line-level comments (both shared and personal)
/// </summary>
public interface ILineCommentsService
{
    /// <summary>
    /// Get all line comments for a file (shared comments + user's personal comments)
    /// </summary>
    Task<List<LineCommentDto>> GetLineCommentsForFileAsync(Guid fileId, Guid userId);

    /// <summary>
    /// Get line comments for a specific line number
    /// </summary>
    Task<List<LineCommentDto>> GetLineCommentsForLineAsync(Guid fileId, int lineNumber, Guid userId);

    /// <summary>
    /// Create a new line comment (either shared or personal)
    /// </summary>
    Task<LineCommentDto> CreateLineCommentAsync(Guid userId, CreateLineCommentRequest request);

    /// <summary>
    /// Update an existing line comment (only if user is the author)
    /// </summary>
    Task<LineCommentDto?> UpdateLineCommentAsync(Guid commentId, Guid userId, UpdateLineCommentRequest request);

    /// <summary>
    /// Delete a line comment (only if user is the author)
    /// </summary>
    Task<bool> DeleteLineCommentAsync(Guid commentId, Guid userId);
}
