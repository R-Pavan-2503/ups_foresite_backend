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

    [HttpGet("{branchName}/commits")]
    public async Task<IActionResult> GetCommitsByBranch(Guid repositoryId, string branchName)
    {
        try
        {
            var commits = await _db.GetCommitsByBranch(repositoryId, branchName);
            return Ok(commits);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching commits for branch {branchName}: {ex.Message}");
            return StatusCode(500, new { error = "Failed to fetch commits" });
        }
    }

    [HttpGet("{branchName}/files")]
    public async Task<IActionResult> GetFilesByBranch(Guid repositoryId, string branchName)
    {
        try
        {
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
