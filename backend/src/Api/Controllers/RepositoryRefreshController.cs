using Microsoft.AspNetCore.Mvc;
using CodeFamily.Api.Core.Interfaces;
using LibGit2Sharp;

namespace CodeFamily.Api.Controllers;

[ApiController]
[Route("repositories")]
public class RepositoryRefreshController : ControllerBase
{
    private readonly IDatabaseService _db;
    private readonly IRepositoryService _repoService;
    private readonly IAnalysisService _analysis;
    private readonly ILogger<RepositoryRefreshController> _logger;

    public RepositoryRefreshController(
        IDatabaseService db,
        IRepositoryService repoService,
        IAnalysisService analysis,
        ILogger<RepositoryRefreshController> logger)
    {
        _db = db;
        _repoService = repoService;
        _analysis = analysis;
        _logger = logger;
    }

    [HttpPost("{repositoryId}/refresh")]
    public async Task<IActionResult> RefreshRepository(Guid repositoryId)
    {
        try
        {
            var repository = await _db.GetRepositoryById(repositoryId);
            if (repository == null)
                return NotFound(new { error = "Repository not found" });

            _logger.LogInformation($"Starting refresh for {repository.OwnerUsername}/{repository.Name}");

            // Start refresh in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await PerformIncrementalRefresh(repository);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Refresh failed for {repository.Id}: {ex.Message}");
                }
            });

            return Ok(new
            {
                message = "Refresh started",
                repositoryId = repository.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Refresh request failed: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task PerformIncrementalRefresh(Core.Models.Repository repository)
    {
        try
        {
            // Step 1: Fetch latest commits from GitHub
            _logger.LogInformation($"📥 Fetching updates for {repository.OwnerUsername}/{repository.Name}");
            await _repoService.FetchRepository(repository.OwnerUsername, repository.Name);

            // Step 2: Get the bare clone
            using var repo = _repoService.GetRepository(repository.OwnerUsername, repository.Name);

            // Step 3: Get last analyzed commit SHA
            var lastAnalyzedSha = repository.LastAnalyzedCommitSha;
            Commit? lastAnalyzedCommit = null;

            if (!string.IsNullOrEmpty(lastAnalyzedSha))
            {
                lastAnalyzedCommit = repo.Lookup<Commit>(lastAnalyzedSha);
            }

            // Step 4: Find new commits across all branches
            var newCommits = new List<Commit>();
            var branches = _repoService.GetAllBranches(repo);

            foreach (var branchName in branches)
            {
                var branchCommits = _repoService.GetCommitsByBranch(repo, branchName);
                
                foreach (var commit in branchCommits)
                {
                    // If we have a last analyzed commit, only process commits after it
                    if (lastAnalyzedCommit != null)
                    {
                        if (commit.Sha == lastAnalyzedSha)
                            break; // Stop when we reach the last analyzed commit

                        // Check if this commit is newer than last analyzed
                        if (commit.Author.When <= lastAnalyzedCommit.Author.When)
                            continue;
                    }

                    // Add if not already in list
                    if (!newCommits.Any(c => c.Sha == commit.Sha))
                    {
                        newCommits.Add(commit);
                    }
                }
            }

            // Step 5: Check for branch changes (additions/deletions)
            var dbBranches = await _db.GetBranchesByRepository(repository.Id);
            var dbBranchNames = dbBranches.Select(b => b.Name).ToHashSet();
            var gitBranchNames = branches.ToHashSet();
            
            _logger.LogInformation($"DB Branches: {string.Join(", ", dbBranchNames)}");
            _logger.LogInformation($"Git Branches: {string.Join(", ", gitBranchNames)}");

            bool branchesChanged = !dbBranchNames.SetEquals(gitBranchNames);

            if (newCommits.Count > 0 || branchesChanged)
            {
                if (branchesChanged)
                {
                    _logger.LogInformation($"🔄 Branch structure changed. Triggering analysis to sync branches.");
                }
                else
                {
                    _logger.LogInformation($"🔄 Found {newCommits.Count} new commits to analyze");
                }

                if (repository.ConnectedByUserId == null)
                {
                    _logger.LogError($"Cannot analyze repository {repository.Name}: ConnectedByUserId is null");
                    return;
                }
                
                await _analysis.AnalyzeRepository(
                    repository.OwnerUsername,
                    repository.Name,
                    repository.Id,
                    repository.ConnectedByUserId.Value
                );

                _logger.LogInformation($"✅ Refresh complete for {repository.Name}");
            }
            else
            {
                _logger.LogInformation($"✅ No new commits or branch changes found for {repository.Name}");
                await _db.UpdateLastRefreshTime(repository.Id);
            }

            _logger.LogInformation($"✅ Refresh complete for {repository.Name}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Incremental refresh failed: {ex.Message}");
            throw;
        }
    }

    private async Task UpdateLastRefreshTime(Guid repositoryId)
    {
        await _db.UpdateLastRefreshTime(repositoryId);
        _logger.LogInformation($"Updated last refresh time for {repositoryId}");
    }
}
