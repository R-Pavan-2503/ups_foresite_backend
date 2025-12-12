using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace CodeFamily.Api.Controllers;

[ApiController]
[Route("api/repositories/{repositoryId}/branches")]
public class BranchesController : ControllerBase
{
    private readonly IDatabaseService _db;
    private readonly ILogger<BranchesController> _logger;

    public BranchesController(IDatabaseService db, ILogger<BranchesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetBranches(Guid repositoryId)
    {
        try
        {
            var branches = await _db.GetBranchesByRepository(repositoryId);
            return Ok(branches);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching branches for repository {repositoryId}: {ex.Message}");
            return StatusCode(500, new { error = "Failed to fetch branches" });
        }
    }

    /// <summary>
    /// Catch-all route that handles branch names with slashes (e.g., feature/new_frontend).
    /// Expects paths like: "{branchName}/commits" or "{branchName}/files"
    /// </summary>
    [HttpGet("{**branchPath}")]
    public async Task<IActionResult> HandleBranchRoute(Guid repositoryId, string branchPath)
    {
        _logger.LogInformation($"BranchesController: Received branchPath = '{branchPath}'");
        
        if (string.IsNullOrEmpty(branchPath))
        {
            return NotFound();
        }

        // Check for /commits suffix
        if (branchPath.EndsWith("/commits"))
        {
            var branchName = branchPath.Substring(0, branchPath.Length - "/commits".Length);
            return await GetCommitsByBranch(repositoryId, branchName);
        }
        
        // Check for /files suffix
        if (branchPath.EndsWith("/files"))
        {
            var branchName = branchPath.Substring(0, branchPath.Length - "/files".Length);
            return await GetFilesByBranch(repositoryId, branchName);
        }

        // No known suffix, return 404
        _logger.LogWarning($"BranchesController: Unknown route pattern for branchPath = '{branchPath}'");
        return NotFound();
    }

    private async Task<IActionResult> GetCommitsByBranch(Guid repositoryId, string branchName)
    {
        try
        {
            _logger.LogInformation($"Fetching commits for branch '{branchName}' in repository {repositoryId}");
            var commits = await _db.GetCommitsByBranch(repositoryId, branchName);
            return Ok(commits);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching commits for branch {branchName}: {ex.Message}");
            return StatusCode(500, new { error = "Failed to fetch commits" });
        }
    }

    private async Task<IActionResult> GetFilesByBranch(Guid repositoryId, string branchName)
    {
        try
        {
            _logger.LogInformation($"Fetching files for branch '{branchName}' in repository {repositoryId}");
            var files = await _db.GetFilesByBranch(repositoryId, branchName);
            return Ok(files);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching files for branch {branchName}: {ex.Message}");
            return StatusCode(500, new { error = "Failed to fetch files" });
        }
    }
}
