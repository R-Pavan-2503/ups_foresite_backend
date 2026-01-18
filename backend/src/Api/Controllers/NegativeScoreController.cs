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
    /// Get all contributor negative scores for a repository.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetNegativeScores(Guid repositoryId)
    {
        try
        {
            var scores = await _negativeScoreService.GetScoresForRepository(repositoryId);
            return Ok(new
            {
                repositoryId,
                scoredContributors = scores.Count,
                scores = scores.Select(s => new
                {
                    contributorName = s.ContributorName,
                    normalizedScore = Math.Round(s.NormalizedScore, 3),
                    rawScore = Math.Round(s.RawScore, 3),
                    totalCommits = s.TotalCommits,
                    eventCount = s.EventCount,
                    lastCalculatedAt = s.LastCalculatedAt,
                    // Score interpretation
                    level = GetScoreLevel(s.NormalizedScore)
                })
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
    public async Task<IActionResult> GetContributorEvents(Guid repositoryId, string contributorName)
    {
        try
        {
            var events = await _negativeScoreService.GetEventsForContributor(repositoryId, contributorName);
            
            // Get file paths for the events
            var fileIds = events.Select(e => e.FileId).Distinct().ToList();
            var files = await _db.GetFilesByIds(fileIds);
            var filePathMap = files.ToDictionary(f => f.Id, f => f.FilePath);

            return Ok(new
            {
                contributorName,
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
