using Microsoft.AspNetCore.Mvc;
using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Controllers;

/// <summary>
/// Controller for managing line-level comments (both shared and personal)
/// </summary>
[ApiController]
[Route("api/linecomments")]
public class LineCommentsController : ControllerBase
{
    private readonly ILineCommentsService _lineCommentsService;
    private readonly ILogger<LineCommentsController> _logger;

    public LineCommentsController(ILineCommentsService lineCommentsService, ILogger<LineCommentsController> logger)
    {
        _lineCommentsService = lineCommentsService;
        _logger = logger;
    }

    /// <summary>
    /// Get all line comments for a file (shared + user's personal comments)
    /// </summary>
    [HttpGet("file/{fileId}")]
    public async Task<ActionResult<List<LineCommentDto>>> GetLineCommentsForFile(Guid fileId, [FromQuery] Guid userId)
    {
        try
        {
            var comments = await _lineCommentsService.GetLineCommentsForFileAsync(fileId, userId);
            return Ok(comments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting line comments for file {FileId}", fileId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get line comments for a specific line number
    /// </summary>
    [HttpGet("file/{fileId}/line/{lineNumber}")]
    public async Task<ActionResult<List<LineCommentDto>>> GetLineCommentsForLine(Guid fileId, int lineNumber, [FromQuery] Guid userId)
    {
        try
        {
            var comments = await _lineCommentsService.GetLineCommentsForLineAsync(fileId, lineNumber, userId);
            return Ok(comments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting line comments for file {FileId} line {LineNumber}", fileId, lineNumber);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create a new line comment (either shared or personal)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<LineCommentDto>> CreateLineComment([FromQuery] Guid userId, [FromBody] CreateLineCommentRequest request)
    {
        try
        {
            if (request.LineNumber <= 0)
            {
                return BadRequest(new { error = "Line number must be greater than 0" });
            }

            if (string.IsNullOrWhiteSpace(request.CommentText))
            {
                return BadRequest(new { error = "Comment text cannot be empty" });
            }

            if (request.CommentText.Length > 5000)
            {
                return BadRequest(new { error = "Comment text cannot exceed 5000 characters" });
            }

            var comment = await _lineCommentsService.CreateLineCommentAsync(userId, request);
            return Ok(comment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating line comment");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing line comment (only if user is the author)
    /// </summary>
    [HttpPut("{commentId}")]
    public async Task<ActionResult<LineCommentDto>> UpdateLineComment(Guid commentId, [FromQuery] Guid userId, [FromBody] UpdateLineCommentRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.CommentText))
            {
                return BadRequest(new { error = "Comment text cannot be empty" });
            }

            if (request.CommentText.Length > 5000)
            {
                return BadRequest(new { error = "Comment text cannot exceed 5000 characters" });
            }

            var comment = await _lineCommentsService.UpdateLineCommentAsync(commentId, userId, request);
            if (comment == null)
            {
                return NotFound(new { error = "Comment not found or you don't have permission to edit it" });
            }

            return Ok(comment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating line comment {CommentId}", commentId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a line comment (only if user is the author)
    /// </summary>
    [HttpDelete("{commentId}")]
    public async Task<ActionResult> DeleteLineComment(Guid commentId, [FromQuery] Guid userId)
    {
        try
        {
            var deleted = await _lineCommentsService.DeleteLineCommentAsync(commentId, userId);
            if (!deleted)
            {
                return NotFound(new { error = "Comment not found or you don't have permission to delete it" });
            }

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting line comment {CommentId}", commentId);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
