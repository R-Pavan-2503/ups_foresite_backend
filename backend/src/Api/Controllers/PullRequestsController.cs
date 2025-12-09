using Microsoft.AspNetCore.Mvc;
using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Controllers;

[ApiController]
[Route("pullrequests")]
public class PullRequestsController : ControllerBase
{
    private readonly IDatabaseService _db;
    private readonly IGitHubService _github;

    public PullRequestsController(IDatabaseService db, IGitHubService github)
    {
        _db = db;
        _github = github;
    }

    [HttpGet("repository/{repositoryId}")]
    public async Task<IActionResult> GetPullRequests(Guid repositoryId)
    {
        var prs = await _db.GetAllPullRequests(repositoryId);
        return Ok(prs);
    }

    [HttpGet("{prId}")]
    public async Task<IActionResult> GetPullRequest(Guid prId)
    {
        var pr = await _db.GetPullRequestByNumber(Guid.Empty, 0); // Simplified
        if (pr == null) return NotFound();

        var files = await _db.GetPrFiles(prId);

        return Ok(new { pr, files });
    }

    [HttpGet("{owner}/{repo}/{prNumber}/details")]
    public async Task<IActionResult> GetPullRequestDetails(string owner, string repo, int prNumber)
    {
        try
        {
            string? accessToken = null;
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                var headerValue = authHeader.ToString();
                if (headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    accessToken = headerValue.Substring("Bearer ".Length).Trim();
                }
            }

            // Get PR from GitHub API
            var pr = await _github.GetPullRequest(owner, repo, prNumber, accessToken);
            
            // Get files changed
            var files = await _github.GetPullRequestFiles(owner, repo, prNumber, accessToken);
            
            // ✨ NEW: Get repository from database to access file ownership and conflicts
            var repository = await _db.GetRepositoryByName(owner, repo);
            List<object>? recommendedReviewers = null;
            List<PrConflict>? potentialConflicts = null;
            
            if (repository != null)
            {
                // Get PR from database
                var dbPr = await _db.GetPullRequestByNumber(repository.Id, prNumber);
                
                if (dbPr != null)
                {
                    // ✨ Feature 1: Aggregate recommended reviewers across all files
                    var prFiles = await _db.GetPrFiles(dbPr.Id);
                    var authorTotals = new Dictionary<string, (decimal score, int fileCount, string? avatarUrl, string? email)>();
                    
                    foreach (var file in prFiles)
                    {
                        var ownership = await _db.GetFileOwnership(file.Id);
                        foreach (var ownerRecord in ownership)
                        {
                            if (!authorTotals.ContainsKey(ownerRecord.AuthorName))
                            {
                                // Get user details
                                var user = await _db.GetUserByAuthorName(ownerRecord.AuthorName);
                                authorTotals[ownerRecord.AuthorName] = (0m, 0, user?.AvatarUrl, user?.Email);
                            }
                            
                            var current = authorTotals[ownerRecord.AuthorName];
                            authorTotals[ownerRecord.AuthorName] = (
                                current.score + (ownerRecord.SemanticScore ?? 0m),
                                current.fileCount + 1,
                                current.avatarUrl,
                                current.email
                            );
                        }
                    }
                    
                    // Sort by total score and take top 2
                    var totalScore = authorTotals.Values.Sum(v => v.score);
                    recommendedReviewers = authorTotals
                        .OrderByDescending(kvp => kvp.Value.score)
                        .Take(2)
                        .Select(kvp => new
                        {
                            authorName = kvp.Key,
                            totalScore = Math.Round(kvp.Value.score, 2),
                            percentage = totalScore > 0 ? Math.Round((double)(kvp.Value.score / totalScore * 100), 1) : 0,
                            filesContributed = kvp.Value.fileCount,
                            avatarUrl = kvp.Value.avatarUrl,
                            email = kvp.Value.email
                        })
                        .Cast<object>()
                        .ToList();
                    
                    // ✨ Feature 2: Detect potential merge conflicts
                    potentialConflicts = await _db.GetPotentialConflicts(dbPr.Id, repository.Id);
                }
            }
            
            // Return comprehensive details (preserving existing structure)
            return Ok(new
            {
                Number = pr.Number,
                Title = pr.Title,
                Body = pr.Body,
                State = pr.State.StringValue,
                Author = new
                {
                    Login = pr.User.Login,
                    AvatarUrl = pr.User.AvatarUrl
                },
                BaseBranch = pr.Base.Ref,
                HeadBranch = pr.Head.Ref,
                CreatedAt = pr.CreatedAt,
                UpdatedAt = pr.UpdatedAt,
                MergedAt = pr.MergedAt,
                Merged = pr.Merged,
                Mergeable = pr.Mergeable,
                FilesChanged = files.Select(f => new
                {
                    Filename = f.FileName,
                    Status = f.Status,
                    Additions = f.Additions,
                    Deletions = f.Deletions,
                    Changes = f.Changes,
                    Patch = f.Patch
                }),
                // ✨ NEW FIELDS
                RecommendedReviewers = recommendedReviewers ?? new List<object>(),
                PotentialConflicts = potentialConflicts ?? new List<PrConflict>()
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Failed to fetch PR details: {ex.Message}" });
        }
    }
}
