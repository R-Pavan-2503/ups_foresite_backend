using Microsoft.AspNetCore.Mvc;
using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Controllers;

/// <summary>
/// Dashboard API Controller for personalized dashboard widgets
/// </summary>
[ApiController]
[Route("[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IDatabaseService _db;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IDatabaseService db, ILogger<DashboardController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get all dashboard data for a user
    /// </summary>
    [HttpGet("{userId}")]
    public async Task<IActionResult> GetDashboardData(Guid userId)
    {
        try
        {
            // Fetch all widget data in parallel
            var recentFilesTask = _db.GetRecentFileViews(userId, 10);
            var bookmarksTask = _db.GetFileBookmarks(userId);
            var teamActivityTask = _db.GetTeamActivity(userId, 15);
            var fileViewCountTask = _db.GetUserFileViewCount(userId);
            var repoCountTask = _db.GetUserRepositoryCount(userId);

            await Task.WhenAll(recentFilesTask, bookmarksTask, teamActivityTask, fileViewCountTask, repoCountTask);

            var recentFiles = await recentFilesTask;
            var bookmarks = await bookmarksTask;
            var teamActivity = await teamActivityTask;

            // Build response with file details
            var recentFilesWithDetails = await BuildRecentFilesResponse(recentFiles);
            var bookmarksWithDetails = await BuildBookmarksResponse(bookmarks);
            var teamActivityWithDetails = await BuildTeamActivityResponse(teamActivity);

            var response = new
            {
                recentFiles = recentFilesWithDetails,
                bookmarkedFiles = bookmarksWithDetails,
                teamActivity = teamActivityWithDetails,
                quickStats = new
                {
                    filesViewed = await fileViewCountTask,
                    repositoriesAccessed = await repoCountTask,
                    bookmarksCount = bookmarks.Count
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get dashboard data for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to load dashboard" });
        }
    }

    /// <summary>
    /// Get recent files for a user
    /// </summary>
    [HttpGet("{userId}/recent-files")]
    public async Task<IActionResult> GetRecentFiles(Guid userId, [FromQuery] int limit = 10)
    {
        try
        {
            var fileViews = await _db.GetRecentFileViews(userId, limit);
            var response = await BuildRecentFilesResponse(fileViews);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent files for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to load recent files" });
        }
    }

    /// <summary>
    /// Track a file view
    /// </summary>
    [HttpPost("file-view")]
    public async Task<IActionResult> TrackFileView([FromBody] FileViewRequest request)
    {
        try
        {
            await _db.UpsertFileView(request.UserId, request.FileId);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track file view for user {UserId}, file {FileId}", request.UserId, request.FileId);
            return StatusCode(500, new { error = "Failed to track file view" });
        }
    }

    /// <summary>
    /// Get bookmarked files
    /// </summary>
    [HttpGet("{userId}/bookmarks")]
    public async Task<IActionResult> GetBookmarks(Guid userId)
    {
        try
        {
            var bookmarks = await _db.GetFileBookmarks(userId);
            var response = await BuildBookmarksResponse(bookmarks);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get bookmarks for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to load bookmarks" });
        }
    }

    /// <summary>
    /// Check if a file is bookmarked
    /// </summary>
    [HttpGet("{userId}/bookmarks/{fileId}/check")]
    public async Task<IActionResult> CheckBookmark(Guid userId, Guid fileId)
    {
        try
        {
            var isBookmarked = await _db.IsFileBookmarked(userId, fileId);
            return Ok(new { isBookmarked });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check bookmark for user {UserId}, file {FileId}", userId, fileId);
            return StatusCode(500, new { error = "Failed to check bookmark" });
        }
    }

    /// <summary>
    /// Add a bookmark
    /// </summary>
    [HttpPost("bookmark")]
    public async Task<IActionResult> AddBookmark([FromBody] BookmarkRequest request)
    {
        try
        {
            await _db.CreateFileBookmark(request.UserId, request.FileId, request.Category);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add bookmark for user {UserId}, file {FileId}", request.UserId, request.FileId);
            return StatusCode(500, new { error = "Failed to add bookmark" });
        }
    }

    /// <summary>
    /// Remove a bookmark
    /// </summary>
    [HttpDelete("bookmark")]
    public async Task<IActionResult> RemoveBookmark([FromQuery] Guid userId, [FromQuery] Guid fileId)
    {
        try
        {
            await _db.DeleteFileBookmark(userId, fileId);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove bookmark for user {UserId}, file {FileId}", userId, fileId);
            return StatusCode(500, new { error = "Failed to remove bookmark" });
        }
    }

    /// <summary>
    /// Get team activity feed
    /// </summary>
    [HttpGet("{userId}/team-activity")]
    public async Task<IActionResult> GetTeamActivity(Guid userId, [FromQuery] int limit = 20)
    {
        try
        {
            var commits = await _db.GetTeamActivity(userId, limit);
            var response = await BuildTeamActivityResponse(commits);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get team activity for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to load team activity" });
        }
    }

    /// <summary>
    /// Get quick stats for a user
    /// </summary>
    [HttpGet("{userId}/stats")]
    public async Task<IActionResult> GetQuickStats(Guid userId)
    {
        try
        {
            // Fetch all stats in parallel
            var fileViewCountTask = _db.GetUserFileViewCount(userId);
            var repoCountTask = _db.GetUserRepositoryCount(userId);
            var bookmarksTask = _db.GetFileBookmarks(userId);
            var commitCountTask = _db.GetUserCommitCount(userId);
            var prsReviewedTask = _db.GetUserPrsReviewedCount(userId);

            await Task.WhenAll(fileViewCountTask, repoCountTask, bookmarksTask, commitCountTask, prsReviewedTask);

            return Ok(new
            {
                filesViewed = await fileViewCountTask,
                repositoriesAccessed = await repoCountTask,
                bookmarksCount = (await bookmarksTask).Count,
                commitsThisWeek = await commitCountTask,
                prsReviewedThisWeek = await prsReviewedTask
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get quick stats for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to load stats" });
        }
    }

    /// <summary>
    /// Get pending reviews for a user (PRs touching files they own)
    /// </summary>
    [HttpGet("{userId}/pending-reviews")]
    public async Task<IActionResult> GetPendingReviews(Guid userId, [FromQuery] int limit = 10)
    {
        try
        {
            _logger.LogInformation("GetPendingReviews called for userId: {UserId}", userId);
            var prs = await _db.GetPendingReviews(userId, limit);
            _logger.LogInformation("GetPendingReviews returned {Count} PRs for userId: {UserId}", prs.Count, userId);
            foreach (var pr in prs)
            {
                _logger.LogInformation("  PR: #{PrNumber} - {Title} (State: {State}, AuthorId: {AuthorId})", 
                    pr.PrNumber, pr.Title, pr.State, pr.AuthorId);
            }
            var response = await BuildPendingReviewsResponse(prs);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending reviews for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to load pending reviews" });
        }
    }

    /// <summary>
    /// Debug endpoint to check why a PR might not be showing in pending reviews
    /// </summary>
    [HttpGet("{userId}/debug-pr/{prNumber}")]
    public async Task<IActionResult> DebugPendingReview(Guid userId, int prNumber)
    {
        try
        {
            // Get the PR by number
            var allPrs = await _db.GetAllOpenPrs();
            var pr = allPrs.FirstOrDefault(p => p.PrNumber == prNumber);
            
            if (pr == null)
            {
                return Ok(new { 
                    found = false, 
                    message = $"PR #{prNumber} not found in database",
                    totalOpenPrs = allPrs.Count
                });
            }
            
            // Get repo info
            var repo = await _db.GetRepositoryById(pr.RepositoryId);
            
            // Check if user has access
            var hasAccessViaTable = await _db.CheckUserRepositoryAccess(userId, pr.RepositoryId);
            var isOwner = repo?.ConnectedByUserId == userId;
            
            // Check if user already reviewed
            var hasReviewed = await _db.HasUserReviewedPr(userId, pr.Id);
            
            // Check if user is the author
            var isAuthor = pr.AuthorId == userId;
            
            return Ok(new {
                found = true,
                prId = pr.Id,
                prNumber = pr.PrNumber,
                title = pr.Title,
                state = pr.State,
                authorId = pr.AuthorId,
                repositoryId = pr.RepositoryId,
                repositoryName = repo?.Name,
                connectedByUserId = repo?.ConnectedByUserId,
                checks = new {
                    hasAccessViaTable,
                    isOwner,
                    hasReviewed,
                    isAuthor,
                    wouldBeExcluded = isAuthor || hasReviewed || (!hasAccessViaTable && !isOwner)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Debug endpoint failed for userId {UserId}, PR #{PrNumber}", userId, prNumber);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // Helper methods
    private async Task<List<object>> BuildRecentFilesResponse(List<FileView> fileViews)
    {
        var result = new List<object>();
        foreach (var fv in fileViews)
        {
            var file = await _db.GetFileById(fv.FileId);
            if (file != null)
            {
                var repo = await _db.GetRepositoryById(file.RepositoryId);
                result.Add(new
                {
                    fileId = fv.FileId,
                    filePath = file.FilePath,
                    fileName = Path.GetFileName(file.FilePath),
                    repositoryId = file.RepositoryId,
                    repositoryName = repo?.Name ?? "Unknown",
                    ownerUsername = repo?.OwnerUsername ?? "",
                    viewedAt = fv.ViewedAt
                });
            }
        }
        return result;
    }

    private async Task<List<object>> BuildBookmarksResponse(List<FileBookmark> bookmarks)
    {
        var result = new List<object>();
        foreach (var bm in bookmarks)
        {
            var file = await _db.GetFileById(bm.FileId);
            if (file != null)
            {
                var repo = await _db.GetRepositoryById(file.RepositoryId);
                result.Add(new
                {
                    fileId = bm.FileId,
                    filePath = file.FilePath,
                    fileName = Path.GetFileName(file.FilePath),
                    repositoryId = file.RepositoryId,
                    repositoryName = repo?.Name ?? "Unknown",
                    ownerUsername = repo?.OwnerUsername ?? "",
                    category = bm.Category,
                    createdAt = bm.CreatedAt
                });
            }
        }
        return result;
    }

    private async Task<List<object>> BuildTeamActivityResponse(List<Commit> commits)
    {
        var result = new List<object>();
        var repoCache = new Dictionary<Guid, Repository?>();

        foreach (var c in commits)
        {
            if (!repoCache.ContainsKey(c.RepositoryId))
            {
                repoCache[c.RepositoryId] = await _db.GetRepositoryById(c.RepositoryId);
            }
            var repo = repoCache[c.RepositoryId];

            result.Add(new
            {
                commitId = c.Id,
                sha = c.Sha,
                shortSha = c.Sha.Length > 7 ? c.Sha.Substring(0, 7) : c.Sha,
                message = c.Message,
                authorName = c.AuthorName,
                repositoryId = c.RepositoryId,
                repositoryName = repo?.Name ?? "Unknown",
                ownerUsername = repo?.OwnerUsername ?? "",
                committedAt = c.CommittedAt
            });
        }
        return result;
    }

    private async Task<List<object>> BuildPendingReviewsResponse(List<PullRequest> prs)
    {
        var result = new List<object>();
        var repoCache = new Dictionary<Guid, Repository?>();
        var userCache = new Dictionary<Guid, User?>();

        foreach (var pr in prs)
        {
            if (!repoCache.ContainsKey(pr.RepositoryId))
            {
                repoCache[pr.RepositoryId] = await _db.GetRepositoryById(pr.RepositoryId);
            }
            var repo = repoCache[pr.RepositoryId];

            // Get author info if author_id exists
            string? authorLogin = null;
            if (pr.AuthorId.HasValue && !userCache.ContainsKey(pr.AuthorId.Value))
            {
                userCache[pr.AuthorId.Value] = await _db.GetUserById(pr.AuthorId.Value);
            }
            if (pr.AuthorId.HasValue && userCache[pr.AuthorId.Value] != null)
            {
                authorLogin = userCache[pr.AuthorId.Value]?.AuthorName;
            }

            result.Add(new
            {
                prId = pr.Id,
                prNumber = pr.PrNumber,
                title = pr.Title ?? "No title",
                authorLogin = authorLogin ?? "Unknown",
                repositoryId = pr.RepositoryId,
                repositoryName = repo?.Name ?? "Unknown",
                ownerUsername = repo?.OwnerUsername ?? ""
            });
        }
        return result;
    }
}

public class FileViewRequest
{
    public Guid UserId { get; set; }
    public Guid FileId { get; set; }
}

public class BookmarkRequest
{
    public Guid UserId { get; set; }
    public Guid FileId { get; set; }
    public string? Category { get; set; }
}
