using Microsoft.AspNetCore.Mvc;
using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Controllers;

[ApiController]
[Route("repositories")]
public class RepositoriesController : ControllerBase
{
    private readonly IGitHubService _github;
    private readonly IDatabaseService _db;
    private readonly IAnalysisService _analysis;

    public RepositoriesController(IGitHubService github, IDatabaseService db, IAnalysisService analysis)
    {
        _github = github;
        _db = db;
        _analysis = analysis;
    }

    [HttpGet]
    public async Task<IActionResult> GetRepositories([FromHeader(Name = "Authorization")] string authorization, [FromQuery] Guid userId)
    {
        try
        {
            var token = authorization.Replace("Bearer ", "");
            var githubRepos = await _github.GetUserRepositories(token);

            // Get analyzed repos
            var analyzedRepos = await _db.GetUserRepositories(userId);

            var result = githubRepos.Select(gr => new
            {
                gr.Id,
                gr.Name,
                gr.Owner.Login,
                gr.Description,
                gr.CloneUrl,
                Analyzed = analyzedRepos.Any(ar => ar.Name == gr.Name && ar.OwnerUsername == gr.Owner.Login),
                Status = analyzedRepos.FirstOrDefault(ar => ar.Name == gr.Name && ar.OwnerUsername == gr.Owner.Login)?.Status
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{owner}/{repo}/analyze")]
    public async Task<IActionResult> AnalyzeRepository(string owner, string repo, [FromQuery] Guid userId)
    {
        try
        {
            // Check if already exists
            var existing = await _db.GetRepositoryByName(owner, repo);
            if (existing != null)
            {
                return Ok(new { message = "Repository already analyzed", repositoryId = existing.Id });
            }

            // Repositories from "Your Repository" tab are ALWAYS yours
            // (they come from GitHub API, even if you're a collaborator)
            var repository = await _db.CreateRepository(new Repository
            {
                Name = repo,
                OwnerUsername = owner,
                Status = "pending",
                ConnectedByUserId = userId,
                IsMine = true  // Always TRUE for repos from GitHub API
            });

            // Start analysis in background
            _ = Task.Run(async () =>
            {
                await _analysis.AnalyzeRepository(owner, repo, repository.Id, userId);
            });

            return Ok(new { message = "Analysis started", repositoryId = repository.Id });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{repositoryId}")]
    public async Task<IActionResult> GetRepository(Guid repositoryId)
    {
        var repo = await _db.GetRepositoryById(repositoryId);
        if (repo == null) return NotFound();

        return Ok(repo);
    }

    [HttpGet("{owner}/{repo}/status")]
    public async Task<IActionResult> GetRepositoryStatus(string owner, string repo)
    {
        try
        {
            var repository = await _db.GetRepositoryByName(owner, repo);
            if (repository == null)
            {
                return Ok(new { analyzed = false });
            }

            return Ok(new
            {
                analyzed = true,
                repositoryId = repository.Id,
                status = repository.Status,
                name = repository.Name,
                owner = repository.OwnerUsername
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("analyzed")]
    public async Task<IActionResult> GetAnalyzedRepositories([FromQuery] Guid userId, [FromQuery] string filter = "all")
    {
        try
        {
            var repositories = await _db.GetAnalyzedRepositories(userId, filter);
            
            var result = repositories.Select(r => new
            {
                r.Id,
                r.Name,
                r.OwnerUsername,
                r.Status,
                r.IsMine,
                Label = r.IsMine ? "Your Repository" : "Added Repository"
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("analyze-url")]
    public async Task<IActionResult> AnalyzeRepositoryByUrl([FromBody] AnalyzeUrlRequest request)
    {
        try
        {
            // Parse GitHub URL
            var (owner, repo) = ParseGitHubUrl(request.Url);
            
            if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo))
            {
                return BadRequest(new { error = "Invalid GitHub URL. Please provide a valid repository URL (e.g., https://github.com/owner/repo)" });
            }

            // Check if already exists
            var existing = await _db.GetRepositoryByName(owner, repo);
            if (existing != null)
            {
                if (existing.Status == "ready")
                {
                    return Ok(new { 
                        message = "Repository already analyzed", 
                        repositoryId = existing.Id,
                        status = existing.Status,
                        alreadyExists = true
                    });
                }
                else if (existing.Status == "analyzing" || existing.Status == "pending")
                {
                    return Ok(new { 
                        message = "Repository analysis is already in progress", 
                        repositoryId = existing.Id,
                        status = existing.Status,
                        alreadyExists = true
                    });
                }
            }

            // Get user to determine if this is their repo
            var user = await _db.GetUserById(request.UserId);
            bool isMine = user != null && owner.Equals(user.AuthorName, StringComparison.OrdinalIgnoreCase);

            // Create repository record
            var repository = await _db.CreateRepository(new Repository
            {
                Name = repo,
                OwnerUsername = owner,
                Status = "pending",
                ConnectedByUserId = request.UserId,
                IsMine = isMine
            });

            // Start analysis in background
            _ = Task.Run(async () =>
            {
                await _analysis.AnalyzeRepository(owner, repo, repository.Id, request.UserId);
            });

            return Ok(new { 
                message = "Analysis started successfully", 
                repositoryId = repository.Id,
                status = "pending",
                alreadyExists = false
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Failed to analyze repository: {ex.Message}" });
        }
    }

    private (string owner, string repo) ParseGitHubUrl(string url)
    {
        try
        {
            // Handle various GitHub URL formats:
            // https://github.com/owner/repo
            // https://github.com/owner/repo.git
            // https://github.com/owner/repo/
            // github.com/owner/repo
            // owner/repo

            url = url.Trim();
            
            // Remove .git suffix if present
            if (url.EndsWith(".git"))
            {
                url = url.Substring(0, url.Length - 4);
            }

            // Remove trailing slashes
            url = url.TrimEnd('/');

            // Remove protocol and www if present
            url = url.Replace("https://", "").Replace("http://", "").Replace("www.", "");

            // Remove github.com/ if present
            if (url.StartsWith("github.com/"))
            {
                url = url.Substring(11);
            }

            // Now we should have owner/repo or owner/repo/more-stuff
            var parts = url.Split('/');
            
            if (parts.Length >= 2)
            {
                return (parts[0], parts[1]);
            }

            return (string.Empty, string.Empty);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }