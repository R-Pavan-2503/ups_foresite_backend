using Microsoft.AspNetCore.Mvc;
using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Controllers;

[ApiController]
[Route("repositories/{repositoryId}/admins")]
public class AdminsController : ControllerBase
{
    private readonly IDatabaseService _db;
    private readonly ILogger<AdminsController> _logger;

    public AdminsController(IDatabaseService db, ILogger<AdminsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get all admins for a repository (Owner only)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAdmins(Guid repositoryId, [FromQuery] Guid userId)
    {
        try
        {
            var repo = await _db.GetRepositoryById(repositoryId);
            if (repo == null)
                return NotFound(new { error = "Repository not found" });

            // Verify user is owner
            if (repo.ConnectedByUserId != userId)
                return StatusCode(403, new { error = "Only the repository owner can view admins" });

            var admins = await _db.GetRepoAdmins(repositoryId);
            
            // Get user details for each admin
            var adminDetails = new List<object>();
            foreach (var admin in admins)
            {
                var user = await _db.GetUserById(admin.UserId);
                if (user != null)
                {
                    adminDetails.Add(new
                    {
                        admin.Id,
                        admin.UserId,
                        UserName = user.AuthorName,
                        Email = user.Email,
                        AvatarUrl = user.AvatarUrl,
                        admin.AssignedByUserId,
                        admin.CreatedAt
                    });
                }
            }

            return Ok(adminDetails);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting admins for repository {RepositoryId}", repositoryId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Add a new admin to the repository (Owner only)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddAdmin(
        Guid repositoryId, 
        [FromBody] AddAdminRequest request)
    {
        try
        {
            var repo = await _db.GetRepositoryById(repositoryId);
            if (repo == null)
                return NotFound(new { error = "Repository not found" });

            // Verify user is owner
            if (repo.ConnectedByUserId != request.CurrentUserId)
                return StatusCode(403, new { error = "Only the repository owner can add admins" });

            // Verify the user to be added exists
            var userToAdd = await _db.GetUserById(request.AdminUserId);
            if (userToAdd == null)
                return BadRequest(new { error = "User to add as admin not found" });

            // Check if user already an admin
            var existingAdmins = await _db.GetRepoAdmins(repositoryId);
            if (existingAdmins.Any(a => a.UserId == request.AdminUserId))
                return BadRequest(new { error = "User is already an admin" });

            // Create admin
            var admin = await _db.CreateRepoAdmin(repositoryId, request.AdminUserId, request.CurrentUserId);

            // Grant repository access if they don't have it
            var hasAccess = await _db.HasRepositoryAccess(request.AdminUserId, repositoryId);
            if (!hasAccess)
            {
                await _db.GrantRepositoryAccess(request.AdminUserId, repositoryId, request.CurrentUserId);
            }

            _logger.LogInformation(
                "User {OwnerUserId} added {AdminUserId} as admin to repository {RepositoryId}",
                request.CurrentUserId, request.AdminUserId, repositoryId);

            return Ok(new
            {
                message = "Admin added successfully",
                admin = new
                {
                    admin.Id,
                    admin.UserId,
                    UserName = userToAdd.AuthorName,
                    Email = userToAdd.Email,
                    AvatarUrl = userToAdd.AvatarUrl,
                    admin.CreatedAt
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding admin to repository {RepositoryId}", repositoryId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Remove an admin from the repository (Owner only)
    /// </summary>
    [HttpDelete("{adminId}")]
    public async Task<IActionResult> RemoveAdmin(
        Guid repositoryId, 
        Guid adminId, 
        [FromQuery] Guid userId)
    {
        try
        {
            var repo = await _db.GetRepositoryById(repositoryId);
            if (repo == null)
                return NotFound(new { error = "Repository not found" });

            // Verify user is owner
            if (repo.ConnectedByUserId != userId)
                return StatusCode(403, new { error = "Only the repository owner can remove admins" });

            // Verify admin exists
            var admins = await _db.GetRepoAdmins(repositoryId);
            var adminToRemove = admins.FirstOrDefault(a => a.Id == adminId);
            if (adminToRemove == null)
                return NotFound(new { error = "Admin not found" });

            // Remove admin
            await _db.DeleteRepoAdmin(adminId);

            _logger.LogInformation(
                "User {OwnerUserId} removed admin {AdminId} from repository {RepositoryId}",
                userId, adminId, repositoryId);

            return Ok(new { message = "Admin removed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing admin from repository {RepositoryId}", repositoryId);
            return BadRequest(new { error = ex.Message });
        }
    }
}

// Request DTOs
public class AddAdminRequest
{
    public Guid CurrentUserId { get; set; }  // The owner requesting the action
    public Guid AdminUserId { get; set; }     // The user to be made admin
}
