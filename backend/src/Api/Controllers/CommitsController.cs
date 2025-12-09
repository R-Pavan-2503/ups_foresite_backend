using Microsoft.AspNetCore.Mvc;
using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Controllers;

[ApiController]
[Route("commits")]
public class CommitsController : ControllerBase
{
    private readonly IDatabaseService _db;
    private readonly IGitHubService _github;

    public CommitsController(IDatabaseService db, IGitHubService github)
    {
        _db = db;
        _github = github;
    }

    [HttpGet("repository/{repositoryId}")]
    public async Task<IActionResult> GetCommits(Guid repositoryId)
    {
        var commits = await _db.GetCommitsByRepository(repositoryId);
        return Ok(commits);
    }

    [HttpGet("{commitId}")]
    public async Task<IActionResult> GetCommit(Guid commitId)
    {
        var commit = await _db.GetCommitById(commitId);
        if (commit == null) return NotFound(new { error = "Commit not found" });
        return Ok(commit);
    }

    [HttpGet("{commitId}/github-details")]
    public async Task<IActionResult> GetCommitGithubDetails(Guid commitId)
    {
        var commit = await _db.GetCommitById(commitId);
        if (commit == null) return NotFound(new { error = "Commit not found" });

        var repo = await _db.GetRepositoryById(commit.RepositoryId);
        if (repo == null) return NotFound(new { error = "Repository not found" });

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

            // Fetch details from GitHub API
            var details = await _github.GetCommit(repo.OwnerUsername, repo.Name, commit.Sha, accessToken);
            return Ok(details);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to fetch GitHub details: {ex.Message}" });
        }
    }
}
