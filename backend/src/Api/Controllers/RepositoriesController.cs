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
        // New Analytics Endpoints
    [HttpGet("{repositoryId}/analytics")]
    public async Task<IActionResult> GetRepositoryAnalytics(Guid repositoryId)
    {
        try
        {
            var repo = await _db.GetRepositoryById(repositoryId);
            if (repo == null) return NotFound();

            // Get all commits
            var commits = await _db.GetCommitsByRepository(repositoryId);
            
            // Get all files
            var files = await _db.GetFilesByRepository(repositoryId);
            
            // Get file changes to calculate hotspots
            var fileChangeCounts = new Dictionary<Guid, int>();
            foreach (var file in files)
            {
                var changes = await _db.GetFileChangesByFile(file.Id);
                fileChangeCounts[file.Id] = changes.Count;
            }

            // Calculate contributors from file ownership
            var uniqueAuthors = new HashSet<string>();
            foreach (var file in files)
            {
                var ownership = await _db.GetFileOwnership(file.Id);
                foreach (var owner in ownership)
                {
                    uniqueAuthors.Add(owner.AuthorName);
                }
            }
            var contributorCount = uniqueAuthors.Count;

            // Calculate activity timeline (last 30 days)
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var recentCommits = commits
                .Where(c => c.CommittedAt >= thirtyDaysAgo)
                .GroupBy(c => c.CommittedAt.Date)
                .Select(g => new {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    Commits = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToList();

            // Calculate file type distribution
            var fileTypes = files
                .GroupBy(f => {
                    var ext = Path.GetExtension(f.FilePath);
                    return string.IsNullOrEmpty(ext) ? "no-ext" : ext.TrimStart('.');
                })
                .Select(g => new {
                    Name = g.Key,
                    Value = g.Count()
                })
                .OrderByDescending(x => x.Value)
                .Take(8)
                .ToList();

            // Find hotspot files (most frequently changed)
            var hotspots = fileChangeCounts
                .OrderByDescending(kvp => kvp.Value)
                .Take(5)
                .Select(kvp => {
                    var file = files.First(f => f.Id == kvp.Key);
                    return new {
                        FilePath = file.FilePath,
                        Changes = kvp.Value
                    };
                })
                .ToList();

            // Calculate last 7 days activity
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
            var recentCommitsCount = commits.Count(c => c.CommittedAt >= sevenDaysAgo);

            return Ok(new {
                TotalFiles = files.Count,
                TotalCommits = commits.Count,
                Contributors = contributorCount,
                RecentCommits = recentCommitsCount,
                ActivityTimeline = recentCommits,
                FileTypes = fileTypes,
                Hotspots = hotspots
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{repositoryId}/summary")]
    public async Task<IActionResult> GetRepositorySummary(Guid repositoryId)
    {
        try
        {
            var repo = await _db.GetRepositoryById(repositoryId);
            if (repo == null) return NotFound();

            var files = await _db.GetFilesByRepository(repositoryId);
            var commits = await _db.GetCommitsByRepository(repositoryId);
            
            // Analyze file types by extension
            var fileExtensions = files
                .Select(f => Path.GetExtension(f.FilePath)?.TrimStart('.'))
                .Where(ext => !string.IsNullOrEmpty(ext))
                .GroupBy(ext => ext)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .ToList();

            // Build summary
            var summary = $"This repository contains {files.Count} files";
            
            if (commits.Any())
            {
                summary += $" with {commits.Count} commits";
            }
            
            if (fileExtensions.Any())
            {
                var topExtensions = string.Join(", ", fileExtensions.Take(3).Select(g => $".{g.Key}"));
                summary += $". Primary file types include {topExtensions}";
            }
            
            summary += ". The codebase has been analyzed for code relationships, dependencies, and semantic understanding.";

            return Ok(new {
                Summary = summary,
                TotalFiles = files.Count,
                RepositoryName = repo.Name,
                FileTypes = fileExtensions.Select(g => new { Extension = g.Key, Count = g.Count() }).ToList()
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{repositoryId}/team-insights")]
    public async Task<IActionResult> GetTeamInsights(Guid repositoryId)
    {
        try
        {
            var repo = await _db.GetRepositoryById(repositoryId);
            if (repo == null) return NotFound();

            // Get all commits
            var commits = await _db.GetCommitsByRepository(repositoryId);
            
            // Get all files
            var files = await _db.GetFilesByRepository(repositoryId);



            // Calculate contributor details directly from commits
            var contributorDetails = new List<object>();
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

            var commitsByAuthor = commits
                .Where(c => !string.IsNullOrEmpty(c.AuthorName))
                .GroupBy(c => c.AuthorName);

            foreach (var group in commitsByAuthor)
            {
                var authorName = group.Key!;
                var authorCommits = group.ToList();
                
                // Try to get real email from users table
                var user = await _db.GetUserByAuthorName(authorName);
                var authorEmail = user?.Email ?? authorCommits.First().AuthorEmail ?? authorName;
                
                var totalAdditions = 0;
                var totalDeletions = 0;
                var filesChanged = new HashSet<Guid>();
                DateTime? lastCommit = null;

                foreach (var commit in authorCommits)
                {
                    if (lastCommit == null || commit.CommittedAt > lastCommit)
                        lastCommit = commit.CommittedAt;
                    
                    var changes = await _db.GetFileChangesByCommit(commit.Id);
                    foreach (var change in changes)
                    {
                        totalAdditions += change.Additions ?? 0;
                        totalDeletions += change.Deletions ?? 0;
                        filesChanged.Add(change.FileId);
                    }
                }

                var isActive = lastCommit.HasValue && lastCommit.Value >= sevenDaysAgo;

                contributorDetails.Add(new {
                    Name = authorName,
                    Email = authorEmail,
                    Commits = authorCommits.Count,
                    LinesAdded = totalAdditions,
                    LinesRemoved = totalDeletions,
                    FilesChanged = filesChanged.Count,
                    LastCommit = lastCommit ?? DateTime.MinValue,
                    Active = isActive
                });
            }

            // Calculate ownership distribution by file type
            var ownershipData = new List<object>();
            var fileTypeGroups = files.GroupBy(f => {
                var ext = Path.GetExtension(f.FilePath);
                return string.IsNullOrEmpty(ext) ? "other" : ext.TrimStart('.');
            }).Take(5);

            foreach (var typeGroup in fileTypeGroups)
            {
                var ownershipByType = new Dictionary<string, int>();
                
                foreach (var file in typeGroup)
                {
                    var ownership = await _db.GetFileOwnership(file.Id);
                    if (ownership.Any())
                    {
                        var topOwner = ownership.OrderByDescending(o => o.SemanticScore).First();
                        var ownerName = topOwner.AuthorName;
                        
                        if (!ownershipByType.ContainsKey(ownerName))
                            ownershipByType[ownerName] = 0;
                        ownershipByType[ownerName]++;
                    }
                }

                var merged = new Dictionary<string, object> { ["type"] = $".{typeGroup.Key}" };
                foreach (var kvp in ownershipByType)
                {
                    merged[kvp.Key] = kvp.Value;
                }
                ownershipData.Add(merged);
            }

            // Calculate most active day
            var dayCommits = commits
                .GroupBy(c => c.CommittedAt.DayOfWeek)
                .Select(g => new { Day = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .FirstOrDefault();

            var mostActiveDay = dayCommits?.Day.ToString() ?? "N/A";

            return Ok(new {
                TotalContributors = contributorDetails.Count,
                ActiveContributors = contributorDetails.Count(c => ((dynamic)c).Active),
                AvgCommitsPerContributor = contributorDetails.Count > 0 
                    ? (commits.Count / (double)contributorDetails.Count).ToString("F1") 
                    : "0",
                MostActiveDay = mostActiveDay,
                Contributors = contributorDetails.OrderByDescending(c => ((dynamic)c).Commits).ToList(),
                OwnershipData = ownershipData
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }