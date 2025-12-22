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

    /// <summary>
    /// Sync missing files for an already-analyzed repository.
    /// This adds any files that exist at HEAD but aren't in the database,
    /// without requiring a full re-analysis.
    /// </summary>
    [HttpPost("{repositoryId}/sync-files")]
    public async Task<IActionResult> SyncMissingFiles(Guid repositoryId)
    {
        try
        {
            var repository = await _db.GetRepositoryById(repositoryId);
            if (repository == null)
                return NotFound(new { error = "Repository not found" });

            _logger.LogInformation($"📂 Syncing missing files for {repository.OwnerUsername}/{repository.Name}");

            // Fetch latest from remote first
            await _repoService.FetchRepository(repository.OwnerUsername, repository.Name);

            // Get the bare clone
            using var repo = _repoService.GetRepository(repository.OwnerUsername, repository.Name);

            // Get all files at HEAD
            var allFilePaths = _repoService.GetAllFilesAtHead(repo);
            _logger.LogInformation($"📂 Found {allFilePaths.Count} total files in repository at HEAD");

            int createdCount = 0;
            foreach (var filePath in allFilePaths)
            {
                // Check if file already exists in database
                var existingFile = await _db.GetFileByPath(repository.Id, filePath);
                if (existingFile == null)
                {
                    // File not in database - create it
                    await _db.CreateFile(new Core.Models.RepositoryFile
                    {
                        RepositoryId = repository.Id,
                        FilePath = filePath
                    });
                    createdCount++;
                }
            }

            _logger.LogInformation($"✅ Sync complete. Created {createdCount} new file records.");

            return Ok(new
            {
                message = "File sync complete",
                repositoryId = repository.Id,
                totalFilesAtHead = allFilePaths.Count,
                newFilesCreated = createdCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"File sync failed: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task PerformIncrementalRefresh(Core.Models.Repository repository)
    {
        try
        {
            _logger.LogInformation($"📥 Starting incremental refresh for {repository.OwnerUsername}/{repository.Name}");
            
            // Clear deltas tracker for this refresh session
            _analysis.ClearFileAuthorDeltas();

            // Step 1: Fetch latest commits from GitHub
            await _repoService.FetchRepository(repository.OwnerUsername, repository.Name);

            // Step 2: Get the bare clone
            using var repo = _repoService.GetRepository(repository.OwnerUsername, repository.Name);

            // Step 3: Sync branches (add new, remove deleted) - SAME as AnalyzeRepository
            var branches = _repoService.GetAllBranches(repo);
            var dbBranches = await _db.GetBranchesByRepository(repository.Id);
            var dbBranchNames = dbBranches.Select(b => b.Name).ToHashSet();
            var gitBranchNames = branches.ToHashSet();

            // Delete stale branches
            foreach (var dbBranch in dbBranches)
            {
                if (!gitBranchNames.Contains(dbBranch.Name))
                {
                    _logger.LogInformation($"🗑️ Removing stale branch: {dbBranch.Name}");
                    await _db.DeleteBranch(dbBranch.Id);
                }
            }

            // Add new branches
            foreach (var branchName in branches)
            {
                var isDefault = branchName == "main" || branchName == "master";
                var branch = await _db.GetBranchByName(repository.Id, branchName);

                if (branch == null)
                {
                    await _db.CreateBranch(new Core.Models.Branch
                    {
                        RepositoryId = repository.Id,
                        Name = branchName,
                        IsDefault = isDefault
                    });
                    _logger.LogInformation($"➕ Created new branch: {branchName} {(isDefault ? "(default)" : "")}");
                }
            }

            // Step 4: Find new commits - track which branches they're on
            var lastAnalyzedSha = repository.LastAnalyzedCommitSha;
            Commit? lastAnalyzedCommit = null;

            if (!string.IsNullOrEmpty(lastAnalyzedSha))
            {
                lastAnalyzedCommit = repo.Lookup<Commit>(lastAnalyzedSha);
            }

            // Get all existing commit SHAs from database to avoid re-processing
            var existingCommits = await _db.GetCommitsByRepository(repository.Id);
            var existingCommitShas = existingCommits.Select(c => c.Sha).ToHashSet();
            _logger.LogInformation($"📊 Found {existingCommitShas.Count} existing commits in database");

            // Dictionary to track which branches each commit belongs to
            var commitToBranches = new Dictionary<string, List<string>>();
            var newCommitShas = new HashSet<string>();

            foreach (var branchName in branches)
            {
                var branchCommits = _repoService.GetCommitsByBranch(repo, branchName);

                foreach (var commit in branchCommits)
                {
                    // ✅ KEY FIX: Only process commits that don't exist in database yet
                    if (existingCommitShas.Contains(commit.Sha))
                    {
                        continue; // Skip already-processed commits
                    }

                    // Track which branches this commit is on
                    if (!commitToBranches.ContainsKey(commit.Sha))
                    {
                        commitToBranches[commit.Sha] = new List<string>();
                    }
                    commitToBranches[commit.Sha].Add(branchName);
                    newCommitShas.Add(commit.Sha);
                }
            }

            // ✅ ALWAYS sync PRs, even if no new commits (PR state can change without commits!)
            _logger.LogInformation($"📋 Fetching pull requests from GitHub...");
            try
            {
                await _analysis.FetchAndStorePullRequests(repository.OwnerUsername, repository.Name, repository.Id);
                _logger.LogInformation($"  ✅ Pull requests synced");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"  ⚠️ Failed to fetch PRs: {ex.Message}");
            }

            if (newCommitShas.Count == 0)
            {
                _logger.LogInformation($"✅ No new commits found for {repository.Name}");
                await _db.UpdateLastRefreshTime(repository.Id);
                return;
            }

            _logger.LogInformation($"🔄 Processing {newCommitShas.Count} new commits across all branches");

            // Step 5: Process each branch with new commits - SAME pattern as AnalyzeRepository
            foreach (var branchName in branches)
            {
                var branchCommits = _repoService.GetCommitsByBranch(repo, branchName);
                var newCommitsInBranch = branchCommits
                    .Where(c => newCommitShas.Contains(c.Sha))
                    .OrderBy(c => c.Author.When)
                    .ToList();

                if (newCommitsInBranch.Count == 0)
                    continue;

                _logger.LogInformation($"🔄 Processing branch '{branchName}': {newCommitsInBranch.Count} new commits");

                int processedCount = 0;
                foreach (var gitCommit in newCommitsInBranch)
                {
                    try
                    {
                        // Check if commit already exists (may have been created by another branch)
                        var commit = await _db.GetCommitBySha(repository.Id, gitCommit.Sha);
                        bool isNewCommit = false;

                        if (commit == null)
                        {
                            isNewCommit = true;

                            // Get author info from Git commit - SAME as AnalyzeRepository
                            var authorEmail = gitCommit.Author.Email ?? "unknown@example.com";
                            var authorName = gitCommit.Author.Name ?? "unknown";

                            var authorUser = await _analysis.GetOrCreateAuthorUser(
                                authorEmail,
                                authorName,
                                repository.OwnerUsername,
                                repository.Name,
                                gitCommit.Sha,
                                repository.Id
                            );

                            var commitEmail = authorUser.Email;
                            if (string.IsNullOrWhiteSpace(commitEmail) &&
                                !string.IsNullOrWhiteSpace(authorEmail) &&
                                !authorEmail.Contains("@users.noreply.github.com"))
                            {
                                commitEmail = authorEmail;
                            }

                            commit = await _db.CreateCommit(new Core.Models.Commit
                            {
                                RepositoryId = repository.Id,
                                Sha = gitCommit.Sha,
                                Message = gitCommit.MessageShort,
                                AuthorName = authorUser.AuthorName,
                                AuthorEmail = commitEmail,
                                AuthorUserId = authorUser.Id,
                                CommittedAt = gitCommit.Author.When.UtcDateTime
                            });

                            _logger.LogInformation($"  ✅ Created commit {gitCommit.Sha[..7]} by {authorUser.AuthorName}");
                        }

                        // Link commit to branch - SAME as AnalyzeRepository
                        var branch = await _db.GetBranchByName(repository.Id, branchName);
                        if (branch != null)
                        {
                            await _db.LinkCommitToBranch(commit.Id, branch.Id);
                        }

                        // ONLY process files if this is a NEW commit - SAME as AnalyzeRepository
                        if (isNewCommit)
                        {
                            var changedFiles = _repoService.GetChangedFiles(repo, gitCommit);

                            foreach (var filePath in changedFiles)
                            {
                                if (commit.AuthorUserId != null && commit.AuthorUserId != Guid.Empty)
                                {
                                    await _analysis.ProcessFile(repo, commit, gitCommit, filePath, commit.AuthorUserId.Value, commit.AuthorEmail ?? "");
                                }
                            }
                        }

                        processedCount++;
                        if (processedCount % 5 == 0)
                        {
                            _logger.LogInformation($"  📈 Progress: {processedCount}/{newCommitsInBranch.Count} commits in '{branchName}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"  ❌ Error processing commit {gitCommit.Sha[..7]}: {ex.Message}");
                    }
                }

                _logger.LogInformation($"  ✅ Processed {processedCount} new commits in branch '{branchName}'");
            }

            // Step 6: Ensure all files at HEAD are in database - SAME as AnalyzeRepository
            _logger.LogInformation("📂 Ensuring all files at HEAD are in database...");
            await _analysis.EnsureAllFilesAtHead(repo, repository.Id);

            // Step 7: Calculate ownership for files touched by new commits - SAME as AnalyzeRepository
            _logger.LogInformation("🧮 Calculating semantic ownership for modified files...");
            await _analysis.CalculateOwnershipForModifiedFiles(repository.Id);

            // Step 8: Reconcile dependencies at HEAD - SAME as AnalyzeRepository
            _logger.LogInformation("🔄 Reconciling dependencies at HEAD...");
            await _analysis.ReconcileDependenciesAtHead(repo, repository.Id);


            // Step 9: Calculate dependency metrics - SAME as AnalyzeRepository
            _logger.LogInformation("📊 Calculating dependency metrics...");
            await _analysis.CalculateDependencyMetrics(repository.Id);

            // Step 11: Update last analyzed commit and refresh time - SAME as AnalyzeRepository
            var latestCommit = repo.Head.Tip;
            if (latestCommit != null)
            {
                await _db.UpdateLastAnalyzedCommit(repository.Id, latestCommit.Sha);
                _logger.LogInformation($"  ✅ Updated last analyzed commit to {latestCommit.Sha[..7]}");
            }

            await _db.UpdateLastRefreshTime(repository.Id);
            _logger.LogInformation($"✅ Incremental refresh complete: processed {newCommitShas.Count} new commits");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Incremental refresh failed: {ex.Message}");
            _logger.LogError($"Exception type: {ex.GetType().Name}");
            _logger.LogError($"Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                _logger.LogError($"Inner exception: {ex.InnerException.Message}");
            }
            throw;
        }
    }

    private async Task UpdateLastRefreshTime(Guid repositoryId)
    {
        await _db.UpdateLastRefreshTime(repositoryId);
        _logger.LogInformation($"Updated last refresh time for {repositoryId}");
    }
}
