using Microsoft.AspNetCore.Mvc;
using CodeFamily.Api.Core.Interfaces;

namespace CodeFamily.Api.Controllers;

[ApiController]
[Route("azuredevops")]
public class AzureDevOpsController : ControllerBase
{
    private readonly IAzureDevOpsService _adoService;
    private readonly ILogger<AzureDevOpsController> _logger;

    public AzureDevOpsController(
        IAzureDevOpsService adoService,
        ILogger<AzureDevOpsController> logger)
    {
        _adoService = adoService;
        _logger = logger;
    }

    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects()
    {
        try
        {
            var projects = await _adoService.GetProjectsAsync();
            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Azure DevOps projects");
            return StatusCode(500, new { error = "Failed to fetch projects from Azure DevOps", details = ex.Message });
        }
    }

    [HttpGet("projects/{projectId}/teams")]
    public async Task<IActionResult> GetTeams(string projectId)
    {
        try
        {
            var teams = await _adoService.GetTeamsAsync(projectId);
            return Ok(teams);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting teams for project: {projectId}");
            return StatusCode(500, new { error = $"Failed to fetch teams for project {projectId}", details = ex.Message });
        }
    }

    [HttpGet("projects/{projectId}/workitems")]
    public async Task<IActionResult> GetWorkItems(
        string projectId,
        [FromQuery] string? teamId = null,
        [FromQuery] string? state = null)
    {
        try
        {
            var workItems = await _adoService.GetWorkItemsAsync(projectId, teamId, state);
            return Ok(workItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting work items for project: {projectId}");
            return StatusCode(500, new { error = $"Failed to fetch work items for project {projectId}", details = ex.Message });
        }
    }

    [HttpGet("workitems/{id}")]
    public async Task<IActionResult> GetWorkItemDetails(int id)
    {
        try
        {
            var workItem = await _adoService.GetWorkItemDetailsAsync(id);
            
            if (workItem == null)
            {
                return NotFound(new { error = $"Work item {id} not found" });
            }

            return Ok(workItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting work item: {id}");
            return StatusCode(500, new { error = $"Failed to fetch work item {id}", details = ex.Message });
        }
    }
}
