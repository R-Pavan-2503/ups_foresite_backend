using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace CodeFamily.Api.Controllers;

/// <summary>
/// API endpoints for Contributor Negative Scores.
/// </summary>
[ApiController]
[Route("repositories/{repositoryId}/negative-scores")]
public class NegativeScoreController : ControllerBase
{
    private readonly INegativeScoreService _negativeScoreService;
    private readonly IDatabaseService _db;
    private readonly ILogger<NegativeScoreController> _logger;

    public NegativeScoreController(
        INegativeScoreService negativeScoreService,
        IDatabaseService db,
        ILogger<NegativeScoreController> logger)
    {
        _negativeScoreService = negativeScoreService;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get all contributor negative scores for a repository, filtered by timeline.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetNegativeScores(
        Guid repositoryId, 
        [FromQuery] int timelineDays = 0)  // 0 = Lifetime (all time), 7 = Past 7 Days, 30 = Past 30 Days
    {
        try
        {
            // Validate timelineDays - allow 0 (Lifetime), 7, or 30
            if (timelineDays != 0 && timelineDays != 7 && timelineDays != 30)
            {
                timelineDays = 0;  // Default to lifetime if invalid
            }

            // Get all replacement events for this repository
            var allEvents = await _db.GetAllReplacementEvents(repositoryId);
            
            // Calculate the cutoff date based on timeline selection
            var cutoffDate = timelineDays == 0 
                ? DateTime.MinValue 
                : DateTime.UtcNow.AddDays(-timelineDays);
            
            // Get all commits to look up replacement commit dates
            var commits = await _db.GetCommitsByRepository(repositoryId);
            var commitDates = commits.ToDictionary(c => c.Id, c => c.CommittedAt);
            
            // Filter events by when the REPLACEMENT actually happened (not when detected)
            var filteredEvents = allEvents.Where(e => 
            {
                if (commitDates.TryGetValue(e.ReplacementCommitId, out var commitDate))
                {
                    return commitDate >= cutoffDate;
                }
                return false; // Skip if we can't find the commit date
            }).ToList();
            
            // Get ALL-TIME commit counts for normalization (use full history, not filtered)
            // This ensures consistent normalization regardless of timeline filter
            var authorCommitCounts = commits
                .GroupBy(c => c.AuthorName ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());
            
            // Group events by original author
            var eventsByAuthor = filteredEvents
                .GroupBy(e => e.OriginalAuthorName)
                .ToDictionary(g => g.Key, g => g.ToList());
            
            // Include ALL contributors who have commits, not just those with events
            var scoresByAuthor = authorCommitCounts.Keys
                .Select(author =>
                {
                    var events = eventsByAuthor.GetValueOrDefault(author, new List<CodeReplacementEvent>());
                    var rawScore = events.Sum(e => e.EventScore);
                    var totalCommits = authorCommitCounts.GetValueOrDefault(author, 1);
                    var normalizedScore = rawScore / Math.Max(1, totalCommits / 10.0);
                    
                    return new
                    {
                        contributorName = author,
                        normalizedScore = Math.Round(normalizedScore, 3),
                        rawScore = Math.Round(rawScore, 3),
                        totalCommits = totalCommits,
                        eventCount = events.Count,
                        lastCalculatedAt = DateTime.UtcNow,
                        level = GetScoreLevel(normalizedScore)
                    };
                })
                .OrderByDescending(s => s.normalizedScore)
                .ToList();

            return Ok(new
            {
                repositoryId,
                timelineDays,
                timelineLabel = timelineDays == 0 ? "All Time" : $"Past {timelineDays} Days",
                scoredContributors = scoresByAuthor.Count,
                totalEvents = filteredEvents.Count,
                scores = scoresByAuthor
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting negative scores for repository {RepositoryId}", repositoryId);
            return StatusCode(500, new { error = "Failed to get negative scores" });
        }
    }

    /// <summary>
    /// Get detailed replacement events for a specific contributor.
    /// </summary>
    [HttpGet("{contributorName}/events")]
    public async Task<IActionResult> GetContributorEvents(
        Guid repositoryId, 
        string contributorName,
        [FromQuery] int timelineDays = 0)  // 0 = Lifetime (all time), 7 = Past 7 Days, 30 = Past 30 Days
    {
        try
        {
            // Validate timelineDays - allow 0 (Lifetime), 7, or 30
            if (timelineDays != 0 && timelineDays != 7 && timelineDays != 30)
            {
                timelineDays = 0;  // Default to lifetime if invalid
            }

            var events = await _negativeScoreService.GetEventsForContributor(repositoryId, contributorName);
            
            // If timeline filtering is needed, filter events
            if (timelineDays > 0)
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-timelineDays);
                
                // Get commits to look up replacement commit dates
                var commits = await _db.GetCommitsByRepository(repositoryId);
                var commitDates = commits.ToDictionary(c => c.Id, c => c.CommittedAt);
                
                // Filter events by when the REPLACEMENT actually happened
                events = events.Where(e => 
                {
                    if (commitDates.TryGetValue(e.ReplacementCommitId, out var commitDate))
                    {
                        return commitDate >= cutoffDate;
                    }
                    return false;
                }).ToList();
            }
            
            // Get file paths for the events
            var fileIds = events.Select(e => e.FileId).Distinct().ToList();
            var files = await _db.GetFilesByIds(fileIds);
            var filePathMap = files.ToDictionary(f => f.Id, f => f.FilePath);

            return Ok(new
            {
                contributorName,
                timelineDays,
                timelineLabel = timelineDays == 0 ? "All Time" : $"Past {timelineDays} Days",
                eventCount = events.Count,
                events = events.Select(e => new
                {
                    id = e.Id,
                    filePath = filePathMap.GetValueOrDefault(e.FileId, "Unknown"),
                    replacedBy = e.ReplacementAuthorName,
                    semanticDissimilarity = Math.Round(e.SemanticDissimilarity, 2),
                    daysAfterOriginal = e.TimeProximityDays,
                    linesChanged = e.ChurnMagnitude,
                    commitSignal = e.CommitMessageSignal,
                    eventScore = Math.Round(e.EventScore, 4),
                    createdAt = e.CreatedAt
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting events for contributor {Contributor}", contributorName);
            return StatusCode(500, new { error = "Failed to get contributor events" });
        }
    }

    /// <summary>
    /// Trigger calculation of negative scores for a repository.
    /// This is typically called manually or after analysis.
    /// </summary>
    [HttpPost("calculate")]
    public async Task<IActionResult> CalculateScores(Guid repositoryId, [FromQuery] Guid userId)
    {
        try
        {
            // Verify user has access to the repository
            var repo = await _db.GetRepositoryById(repositoryId);
            if (repo == null)
            {
                return NotFound(new { error = "Repository not found" });
            }

            // Check if user is owner, admin, or team leader
            var isOwner = repo.ConnectedByUserId == userId;
            var isAdmin = await _db.IsRepoAdmin(userId, repositoryId);
            
            // Check if user is a team leader in any team in this repository
            var isTeamLeader = false;
            var teams = await _db.GetTeamsByRepository(repositoryId);
            foreach (var team in teams)
            {
                var members = await _db.GetTeamMembers(team.Id);
                if (members.Any(m => m.UserId == userId && m.Role == "team_leader"))
                {
                    isTeamLeader = true;
                    break;
                }
            }

            if (!isOwner && !isAdmin && !isTeamLeader)
            {
                return Forbid("Only repository owners, admins, and team leaders can trigger score calculation");
            }

            _logger.LogInformation("Starting negative score calculation for repository {RepositoryId}", repositoryId);

            // Clear existing data for recalculation
            await _db.DeleteNegativeScoreData(repositoryId);

            // Calculate scores
            await _negativeScoreService.CalculateNegativeScoresForRepository(repositoryId);

            // Return the new scores
            var scores = await _negativeScoreService.GetScoresForRepository(repositoryId);

            return Ok(new
            {
                message = "Negative scores calculated successfully",
                repositoryId,
                scoredContributors = scores.Count,
                scores = scores.Select(s => new
                {
                    contributorName = s.ContributorName,
                    normalizedScore = Math.Round(s.NormalizedScore, 3),
                    eventCount = s.EventCount,
                    level = GetScoreLevel(s.NormalizedScore)
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating negative scores for repository {RepositoryId}", repositoryId);
            return StatusCode(500, new { error = "Failed to calculate negative scores", details = ex.Message });
        }
    }

    private static string GetScoreLevel(double score) => score switch
    {
        < 0.5 => "excellent",
        < 1.0 => "good",
        < 2.0 => "moderate",
        < 5.0 => "elevated",
        _ => "high"
    };
}
