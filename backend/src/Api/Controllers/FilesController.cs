using Microsoft.AspNetCore.Mvc;
using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Controllers;

[ApiController]
[Route("files")]
public class FilesController : ControllerBase
{
    private readonly IDatabaseService _db;
    private readonly IRepositoryService _repoService;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        IDatabaseService db, 
        IRepositoryService repoService,
        ILogger<FilesController> logger)
    {
        _db = db;
        _repoService = repoService;
        _logger = logger;
    }

    [HttpGet("repository/{repositoryId}")]
    public async Task<IActionResult> GetFiles(Guid repositoryId)
    {
        var files = await _db.GetFilesByRepository(repositoryId);
        return Ok(files);
    }

    // Get individual file metadata
    [HttpGet("{fileId}")]
    public async Task<IActionResult> GetFile(Guid fileId)
    {
        var file = await _db.GetFileById(fileId);
        if (file == null) return NotFound(new { error = "File not found" });

        // Get file analysis data
        var ownership = await _db.GetFileOwnership(fileId);
        var dependencies = await _db.GetDependenciesForFile(fileId);
        var dependents = await _db.GetDependentsForFile(fileId);
        var embeddings = await _db.GetEmbeddingsByFile(fileId);
        var changes = await _db.GetFileChangesByFile(fileId);

        // Get semantic neighbors (similar files)
        var similarFiles = new List<RepositoryFile>();
        if (embeddings.Any())
        {
            try
            {
                var neighbors = await _db.FindSimilarFiles(embeddings.First().Embedding!, file.RepositoryId, fileId, 5);
                similarFiles = neighbors.Select(n => n.Item1).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to find similar files: {ex.Message}");
            }
        }

        // Get dependency file details
        var dependencyDetails = new List<object>();
        foreach (var dep in dependencies)
        {
            var targetFile = await _db. GetFileById(dep.TargetFileId);
            if (targetFile != null)
            {
                dependencyDetails.Add(new
                {
                    filePath = targetFile.FilePath,
                    dependencyType = dep.DependencyType,
                    strength = dep.Strength
                });
            }
        }

        // Get dependent file details
        var dependentDetails = new List<object>();
        foreach (var dep in dependents)
        {
            var sourceFile = await _db.GetFileById(dep.SourceFileId);
            if (sourceFile != null)
            {
                dependentDetails.Add(new
                {
                    filePath = sourceFile.FilePath
                });
            }
        }

        // Get ownership details with author names AND user profile data (avatars, emails)
        // Normalize semantic scores to percentages (0-1 range) so they add up to 100%
        var ownershipDetails = new List<object>();
        
        // Calculate total semantic score for normalization
        var totalSemanticScore = ownership
            .Where(o => o.SemanticScore.HasValue)
            .Sum(o => (double)o.SemanticScore.Value);
        
        foreach (var own in ownership)
        {
            var user = await _db.GetUserByAuthorName(own.AuthorName);
            
            // Normalize the score: divide by total so all scores sum to 1.0 (100%)
            var normalizedScore = 0.0;
            if (totalSemanticScore > 0 && own.SemanticScore.HasValue)
            {
                normalizedScore = Math.Round((double)own.SemanticScore.Value / totalSemanticScore, 4);
            }
            
            ownershipDetails.Add(new
            {
                authorName = own.AuthorName,
                semanticScore = normalizedScore, // Now returns 0-1 range (e.g., 0.65 = 65%)
                avatarUrl = user?.AvatarUrl,
                email = user?.Email,
                userId = user?.Id.ToString() // For backward compatibility
            });
        }

        // Calculate most frequent author from file changes
        var mostFrequentAuthor = await _db.GetMostActiveAuthorForFile(fileId);

        return Ok(new
        {
            id = file.Id,
            filePath = file.FilePath,
            totalLines = file.TotalLines,
            purpose = "Semantic summary will be generated from embeddings", // TODO: Generate from embeddings
            ownership = ownershipDetails,
            dependencies = dependencyDetails,
            dependents = dependentDetails,
            semanticNeighbors = similarFiles.Select(f => new { filePath = f.FilePath }),
            changeCount = changes.Count,
            mostFrequentAuthor = mostFrequentAuthor ?? "N/A",
            lastModified = changes.Any() ? await GetLastModifiedDate(fileId) : (DateTime?)null,
            isInOpenPr = await _db.IsFileInOpenPr(fileId)
        });
    }
 
    private async Task<DateTime?> GetLastModifiedDate(Guid fileId)
    {
        try
        {
            var changes = await _db.GetFileChangesByFile(fileId);
            if (!changes.Any()) return null;

            // Get the most recent commit that modified this file
            var commitIds = changes.Select(c => c.CommitId).ToList();
            DateTime? latestDate = null;

            foreach (var commitId in commitIds)
            {
                var commit = await _db.GetCommitById(commitId);
                if (commit != null && (latestDate == null || commit.CommittedAt > latestDate))
                {
                    latestDate = commit.CommittedAt;
                }
            }

            return latestDate;
        }
        catch
        {
            return null;
        }
    }

    // Get commit history for a file
    [HttpGet("{fileId}/commits")]
    public async Task<IActionResult> GetFileCommits(Guid fileId)
    {
        var file = await _db.GetFileById(fileId);
        if (file == null) return NotFound(new { error = "File not found" });

        var commits = await _db.GetCommitsForFile(fileId);
        return Ok(commits);
    }

    // Get file content from Git repository
    [HttpGet("{fileId}/content")]
    public async Task<IActionResult> GetFileContent(
        Guid fileId, 
        [FromQuery] string? commitSha = null, 
        [FromQuery] string? branchName = null)
    {
        try
        {
            var file = await _db.GetFileById(fileId);
            if (file == null) return NotFound(new { error = "File not found" });

            var repository = await _db.GetRepositoryById(file.RepositoryId);
            if (repository == null) return NotFound(new { error = "Repository not found" });

            // Get the cloned repository
            using var repo = _repoService.GetRepository(repository.OwnerUsername, repository.Name);

            // Resolve commit with priority: commitSha > branchName > default HEAD
            LibGit2Sharp.Commit? commit = null;
            
            if (!string.IsNullOrEmpty(commitSha))
            {
                // Priority 1: Specific commit SHA (for commit navigation)
                commit = repo.Lookup(commitSha) as LibGit2Sharp.Commit;
            }
            else if (!string.IsNullOrEmpty(branchName))
            {
                // Priority 2: Branch name - resolve to branch tip
                var normalizedBranchName = branchName.Replace("origin/", "");
                var branch = repo.Branches[normalizedBranchName] 
                             ?? repo.Branches[$"origin/{normalizedBranchName}"];
                
                if (branch != null)
                {
                    commit = branch.Tip;
                }
                else
                {
                    _logger.LogWarning($"Branch '{branchName}' not found, falling back to HEAD");
                    commit = repo.Head.Tip;
                }
            }
            else
            {
                // Priority 3: Default to HEAD (backward compatibility)
                commit = repo.Head.Tip;
            }

            if (commit == null) return NotFound(new { error = "Commit not found" });

            // Get the blob (file content) at this commit
            var blob = commit[file.FilePath]?.Target as LibGit2Sharp.Blob;
            if (blob == null) return NotFound(new { error = "File not found in commit" });

            // Return content as text
            var content = blob.GetContentText();

            return Ok(new
            {
                filePath = file.FilePath,
                commitSha = commit.Sha,
                content = content,
                size = blob.Size
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to get file content: {ex.Message}");
            return StatusCode(500, new { error = $"Failed to retrieve file content: {ex.Message}" });
        }
    }
}
