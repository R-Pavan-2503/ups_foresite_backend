using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibGit2Sharp;

using LibGitRepository = LibGit2Sharp.Repository;
using LibGitCommit = LibGit2Sharp.Commit;
using DbCommit = CodeFamily.Api.Core.Models.Commit;
using DbRepository = CodeFamily.Api.Core.Models.Repository;

namespace CodeFamily.Api.Core.Services;

/// <summary>
/// ENHANCED Analysis Service implementing COMPLETE ingestion pipeline.
/// </summary>
public class AnalysisService : IAnalysisService
{
    private readonly IDatabaseService _db;
    private readonly IRepositoryService _repoService;
    private readonly ITreeSitterService _treeSitter;
    private readonly IGeminiService _gemini;
    private readonly IGitHubService _github;
    private readonly ILogger<AnalysisService> _logger;

    // Track author contributions per file for ownership calculation
    private readonly Dictionary<Guid, Dictionary<string, List<double>>> _fileAuthorDeltas = new();

    public AnalysisService(
        IDatabaseService db,
        IRepositoryService repoService,
        ITreeSitterService treeSitter,
        IGeminiService gemini,
        IGitHubService github,
        ILogger<AnalysisService> logger)
    {
        _db = db;
        _repoService = repoService;
        _treeSitter = treeSitter;
        _gemini = gemini;
        _github = github;
        _logger = logger;
    }
public async Task AnalyzeRepository(string owner, string repoName, Guid repositoryId, Guid userId)
    {
        _logger.LogInformation($"🚀 Starting COMPLETE analysis of {owner}/{repoName}");
        _fileAuthorDeltas.Clear();

        try
        {
            await _db.UpdateRepositoryStatus(repositoryId, "analyzing");

            // Step 1: Bare clone (or reuse existing)
            var cloneUrl = $"https://github.com/{owner}/{repoName}.git";
            var repoPath = await _repoService.CloneBareRepository(cloneUrl, owner, repoName);

            using var repo = _repoService.GetRepository(owner, repoName);

            // Step 2: Get ALL commits (full history)
            var branches = _repoService.GetAllBranches(repo);
            _logger.LogInformation($"📊 Found{branches.Count} branches to process");

            // Step 3: Store branch information in database and prune deleted branches
            var dbBranches = await _db.GetBranchesByRepository(repositoryId);
            var dbBranchNames = dbBranches.Select(b => b.Name).ToHashSet();
            var gitBranchNames = branches.ToHashSet();

            // Identify and delete stale branches
            foreach (var dbBranch in dbBranches)
            {
                if (!gitBranchNames.Contains(dbBranch.Name))
                {
                    _logger.LogInformation($"🗑️ Pruning stale branch from DB: {dbBranch.Name}");
                    await _db.DeleteBranch(dbBranch.Id);
                }
            }

            foreach (var branchName in branches)
            {
                var isDefault = branchName == "main" || branchName == "master";
                // We can skip checking DB if we know we just pruned the stale ones, 
                // but checking if it exists is safer/idempotent
                var branch = await _db.GetBranchByName(repositoryId, branchName);

                if (branch == null)
                {
                    branch = await _db.CreateBranch(new CodeFamily.Api.Core.Models.Branch
                    {
                        RepositoryId = repositoryId,
                        Name = branchName,
                        IsDefault = isDefault
                    });
                    _logger.LogInformation($"  ✅ Created branch: {branchName} {(isDefault ? "(default)" : "")}");
                }
            }

            // Step 4: Process each branch
            foreach (var branchName in branches)
            {
                _logger.LogInformation($"🔄 Processing branch: {branchName}");

                // Get commits for this specific branch
                var commits = _repoService.GetCommitsByBranch(repo, branchName);
                _logger.LogInformation($"   📊 Found {commits.Count} commits in branch '{branchName}'");

                // Step 5: Process each commit with branch tracking
                int processedCount = 0;
                foreach (var gitCommit in commits)
                {
                    try
                    {
                        // Check if commit already exists
                        var commit = await _db.GetCommitBySha(repositoryId, gitCommit.Sha);
                        bool isNewCommit = false;

                        if (commit == null)
                        {
                            isNewCommit = true;
                            
                            var authorEmail = gitCommit.Author.Email ?? "unknown@example.com";
                            var authorName = gitCommit.Author.Name ?? "unknown";

                            // Get or create user (with GitHub API lookup for unknown emails)
                            // ONLY do this for NEW commits
                            var authorUser = await GetOrCreateAuthorUser(
                                authorEmail,
                                authorName,
                                owner,
                                repoName,
                                gitCommit.Sha,
                                repositoryId
                            );

                            // NEVER store noreply emails - use user's email or null
                            var commitEmail = authorUser.Email;
                            if (string.IsNullOrWhiteSpace(commitEmail) && 
                                !string.IsNullOrWhiteSpace(authorEmail) && 
                                !authorEmail.Contains("@users.noreply.github.com"))
                            {
                                commitEmail = authorEmail; // Only use real emails
                            }
                            
                            commit = await _db.CreateCommit(new DbCommit
                            {
                                RepositoryId = repositoryId,
                                Sha = gitCommit.Sha,
                                Message = gitCommit.MessageShort,
                                AuthorName = authorUser.AuthorName, // Use GitHub username from user record
                                AuthorEmail = commitEmail, // NEVER noreply - use user's email or null
                                AuthorUserId = authorUser.Id,
                                CommittedAt = gitCommit.Author.When.UtcDateTime
                            });
                        }

                        // Link commit to branch using junction table
                        var branch = await _db.GetBranchByName(repositoryId, branchName);
                        if (branch != null)
                        {
                            await _db.LinkCommitToBranch(commit.Id, branch.Id);
                        }

                        // ONLY process files if this is a NEW commit
                        if (isNewCommit)
                        {
                            // Get changed files for this commit
                            var changedFiles = _repoService.GetChangedFiles(repo, gitCommit);

                            foreach (var filePath in changedFiles)
                            {
                                if (commit != null)
                                {
                                    await ProcessFile(repo, commit, gitCommit, filePath, commit.AuthorUserId ?? Guid.Empty, commit.AuthorEmail ?? "");
                                }
                            }
                        }

                        processedCount++;
                        if (processedCount % 10 == 0)
                        {
                            _logger.LogInformation($"   📈 Processed {processedCount}/{commits.Count} commits in '{branchName}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"   ❌ Error processing commit {gitCommit.Sha}: {ex.Message}");
                    }
                }

                _logger.LogInformation($"   ✅ Processed all {processedCount} commits in branch '{branchName}'");
            }

            _logger.LogInformation($"✅ Processed all {branches.Count} branches");

            // Step 4: Calculate semantic ownership for ALL files
            _logger.LogInformation("🧮 Calculating semantic ownership scores...");
            await CalculateAllFileOwnership(repositoryId);

            // Step 4.5: Final dependency reconciliation at HEAD
            // Re-analyze all files at the latest commit to catch dependencies
            // where the target file was added after the importer
            _logger.LogInformation("🔄 Reconciling dependencies at HEAD...");
            await ReconcileDependenciesAtHead(repo, repositoryId);

            // Step 4.6: Fetch and store pull requests from GitHub
            _logger.LogInformation($"📋 Fetching pull  requests from GitHub for {owner}/{repoName}...");
            await FetchAndStorePullRequests(owner, repoName, repositoryId);

            // Step 5: Calculate dependency graph and blast radius
            _logger.LogInformation("📊 Calculating dependency graph and blast radius...");
            await CalculateDependencyMetrics(repositoryId);

            // Step 6: Register webhook for real‑time updates
            _logger.LogInformation($"🔔 Registering webhook for {owner}/{repoName}...");
            await RegisterWebhook(owner, repoName);

            // Step 7: Update last analyzed commit and refresh time
            if (repo.Head.Tip != null)
            {
                await _db.UpdateLastAnalyzedCommit(repositoryId, repo.Head.Tip.Sha);
            }

            await _db.UpdateRepositoryStatus(repositoryId, "ready");
            _logger.LogInformation($"🎉 COMPLETE analysis finished for {owner}/{repoName}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"💥 Analysis failed for {owner}/{repoName}: {ex.Message}");
            await _db.UpdateRepositoryStatus(repositoryId, "error");
            throw;
        }
    }
}