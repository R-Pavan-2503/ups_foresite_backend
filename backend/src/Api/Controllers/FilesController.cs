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
    private readonly IGroqService _groqService;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        IDatabaseService db,
        IRepositoryService repoService,
        IGroqService groqService,
        ILogger<FilesController> logger)
    {
        _db = db;
        _repoService = repoService;
        _groqService = groqService;
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

        // OPTIMIZED: Get dependency file details in batch
        var dependencyDetails = new List<object>();
        if (dependencies.Any())
        {
            var depFileIds = dependencies.Select(d => d.TargetFileId).ToList();
            var depFiles = await _db.GetFilesByIds(depFileIds);
            var depFilesDict = depFiles.ToDictionary(f => f.Id);

            foreach (var dep in dependencies)
            {
                if (depFilesDict.TryGetValue(dep.TargetFileId, out var targetFile))
                {
                    dependencyDetails.Add(new
                    {
                        filePath = targetFile.FilePath,
                        dependencyType = dep.DependencyType,
                        strength = dep.Strength
                    });
                }
            }
        }

        // OPTIMIZED: Get dependent file details in batch
        var dependentDetails = new List<object>();
        if (dependents.Any())
        {
            var depFileIds = dependents.Select(d => d.SourceFileId).ToList();
            var depFiles = await _db.GetFilesByIds(depFileIds);
            var depFilesDict = depFiles.ToDictionary(f => f.Id);

            foreach (var dep in dependents)
            {
                if (depFilesDict.TryGetValue(dep.SourceFileId, out var sourceFile))
                {
                    dependentDetails.Add(new
                    {
                        filePath = sourceFile.FilePath
                    });
                }
            }
        }

        // Get ownership details with author names AND user profile data (avatars, emails)
        // Normalize semantic scores to percentages (0-1 range) so they add up to 100%
        var ownershipDetails = new List<object>();

        // Calculate total semantic score for normalization
        var totalSemanticScore = ownership
            .Where(o => o.SemanticScore.HasValue)
            .Sum(o => (double)o.SemanticScore.Value);

        // OPTIMIZED: Get all users in batch
        var authorNames = ownership.Select(o => o.AuthorName).ToList();
        var users = await _db.GetUsersByAuthorNames(authorNames);
        var usersDict = users.ToDictionary(u => u.AuthorName);

        foreach (var own in ownership)
        {
            usersDict.TryGetValue(own.AuthorName, out var user);

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

        // FALLBACK: If no semantic ownership exists but file has changes,
        // calculate ownership from commit contributions (for single-commit files)
        if (!ownershipDetails.Any() && changes.Any())
        {
            _logger.LogInformation($"No semantic ownership for file {fileId}, calculating from commit contributions");

            // Aggregate contributions by author from file changes
            var authorContributions = new Dictionary<string, (int additions, int deletions, User? user)>();

            // OPTIMIZED: Get all commits and users in batch
            var commitIds = changes.Select(c => c.CommitId).ToList();
            var commits = await _db.GetCommitsByIds(commitIds);
            var commitsDict = commits.ToDictionary(c => c.Id);

            // Collect all author names first
            var allAuthorNames = commits.Where(c => !string.IsNullOrEmpty(c.AuthorName)).Select(c => c.AuthorName!).Distinct().ToList();
            var allUsers = await _db.GetUsersByAuthorNames(allAuthorNames);
            var fallbackUsersDict = allUsers.ToDictionary(u => u.AuthorName);

            foreach (var change in changes)
            {
                if (!commitsDict.TryGetValue(change.CommitId, out var commit)) continue;
                if (string.IsNullOrEmpty(commit.AuthorName)) continue;

                var authorName = commit.AuthorName;
                var additions = change.Additions ?? 0;
                var deletions = change.Deletions ?? 0;

                if (!authorContributions.ContainsKey(authorName))
                {
                    fallbackUsersDict.TryGetValue(authorName, out var user);
                    authorContributions[authorName] = (additions, deletions, user);
                }
                else
                {
                    var prev = authorContributions[authorName];
                    authorContributions[authorName] = (prev.additions + additions, prev.deletions + deletions, prev.user);
                }
            }

            // Calculate total contribution for percentage calculation
            var totalContribution = authorContributions.Values.Sum(c => c.additions + c.deletions);

            foreach (var kvp in authorContributions.OrderByDescending(c => c.Value.additions + c.Value.deletions))
            {
                var contribution = kvp.Value.additions + kvp.Value.deletions;
                var score = totalContribution > 0
                    ? Math.Round((double)contribution / totalContribution, 4)
                    : 1.0 / authorContributions.Count; // Equal split if no line changes

                ownershipDetails.Add(new
                {
                    authorName = kvp.Key,
                    semanticScore = score,
                    avatarUrl = kvp.Value.user?.AvatarUrl,
                    email = kvp.Value.user?.Email,
                    userId = kvp.Value.user?.Id.ToString(),
                    isContributorBased = true // Flag indicating this is not semantic ownership
                });
            }

            _logger.LogInformation($"Calculated fallback ownership for {authorContributions.Count} contributor(s)");
        }

        // Calculate most frequent author from file changes
        var mostFrequentAuthor = await _db.GetMostActiveAuthorForFile(fileId);

        return Ok(new
        {
            id = file.Id,
            repositoryId = file.RepositoryId,
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

            // OPTIMIZED: Get all commits at once
            var commitIds = changes.Select(c => c.CommitId).ToList();
            var commits = await _db.GetCommitsByIds(commitIds);
            
            var latestDate = commits.Any() 
                ? commits.Max(c => c.CommittedAt) 
                : (DateTime?)null;

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

    [HttpGet("{fileId}/content")]
    public async Task<IActionResult> GetFileContent(
        Guid fileId,
        [FromQuery] string? commitSha = null,
        [FromQuery] string? branchName = null,
        [FromHeader(Name = "Authorization")] string? authorization = null)
    {
        try
        {
            var file = await _db.GetFileById(fileId);
            if (file == null) return NotFound(new { error = "File not found" });

            var repository = await _db.GetRepositoryById(file.RepositoryId);
            if (repository == null) return NotFound(new { error = "Repository not found" });

            // Extract access token from Authorization header
            string? accessToken = null;
            if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                accessToken = authorization.Substring("Bearer ".Length).Trim();
            }

            // Ensure bare clone exists locally (create if doesn't exist)
            var cloneUrl = $"https://github.com/{repository.OwnerUsername}/{repository.Name}.git";
            await _repoService.CloneBareRepository(cloneUrl, repository.OwnerUsername, repository.Name, accessToken);

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

    // Generate AI-powered file summary using Groq
    [HttpGet("{fileId}/summary")]
    public async Task<IActionResult> GetFileSummary(Guid fileId)
    {
        try
        {
            _logger.LogInformation($"Generating summary for file {fileId}");

            // Get all code chunks for this file from Supabase
            var embeddings = await _db.GetEmbeddingsByFile(fileId);

            // If no chunks, return specific message
            if (!embeddings.Any())
            {
                _logger.LogInformation($"No code chunks found for file {fileId}");
                return Ok(new
                {
                    success = false,
                    message = "No code chunks available to generate summary",
                    chunkCount = 0
                });
            }

            // Extract chunk content only
            var chunks = embeddings
                .Select(e => e.ChunkContent)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            if (!chunks.Any())
            {
                return Ok(new
                {
                    success = false,
                    message = "No valid code chunks available to generate summary",
                    chunkCount = 0
                });
            }

            _logger.LogInformation($"Found {chunks.Count} chunks, calling Groq API...");

            // Call Groq service to generate summary
            var (success, summary, error) = await _groqService.GenerateFileSummaryAsync(chunks);

            // Return result
            if (success)
            {
                _logger.LogInformation("Successfully generated file summary");
                return Ok(new
                {
                    success = true,
                    summary = summary,
                    chunkCount = chunks.Count
                });
            }
            else
            {
                _logger.LogWarning($"Failed to generate summary: {error}");
                return Ok(new
                {
                    success = false,
                    error = error ?? "Failed to generate summary",
                    chunkCount = chunks.Count
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error generating file summary: {ex.Message}");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error"
            });
        }
    }
}
