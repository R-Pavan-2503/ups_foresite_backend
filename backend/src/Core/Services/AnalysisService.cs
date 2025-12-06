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

// ---------------------------------------------------------------------
    // Helper: Get or create a user record for an author
    // Email-first approach: Only call GitHub API for unknown emails
    // ---------------------------------------------------------------------
    private async Task<User> GetOrCreateAuthorUser(
        string email,
        string username,
        string repoOwner,
        string repoName,
        string commitSha,
        Guid repositoryId)
    {
        // Step 1: Parse noreply emails to extract GitHub username and ID
        string? githubUsername = null;
        long githubId = 0;
        
        if (!string.IsNullOrWhiteSpace(email) && email.Contains("@users.noreply.github.com"))
        {
            var parts = email.Split('@')[0].Split('+');
            if (parts.Length == 2)
            {
                githubUsername = parts[1]; // Real GitHub username
                long.TryParse(parts[0], out githubId);
                _logger.LogInformation($"📧 Parsed noreply email: '{email}' → GitHub username: '{githubUsername}', ID: {githubId}");
            }
        }

        // Step 2: For REAL emails (not noreply), check if user exists by email FIRST
        if (!string.IsNullOrWhiteSpace(email) && !email.Contains("@users.noreply.github.com"))
        {
            var existingByEmail = await _db.GetUserByEmail(email);
            if (existingByEmail != null)
            {
                _logger.LogInformation($"✅ Found existing user by email '{email}': {existingByEmail.AuthorName}");
                return existingByEmail;
            }
            
            // Email not found - need to call GitHub API to get real username
            _logger.LogInformation($"🔍 Unknown email '{email}' - calling GitHub API for commit {commitSha[..7]}");
            
            try
            {
                var commitAuthor = await _github.GetCommitAuthor(repoOwner, repoName, commitSha);
                if (commitAuthor != null)
                {
                    githubUsername = commitAuthor.Login;
                    githubId = commitAuthor.Id;
                    _logger.LogInformation($"✅ GitHub API resolved: {githubUsername} (ID: {githubId})");
                }
                else
                {
                    _logger.LogWarning($"⚠️ GitHub API returned null for commit {commitSha[..7]}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"⚠️ GitHub API call failed for commit {commitSha[..7]}: {ex.Message}");
            }
        }

        // Step 3: If we have GitHub username, check if user exists
        if (!string.IsNullOrWhiteSpace(githubUsername))
        {
            // First try by GitHub ID
            if (githubId > 0)
            {
                var existingById = await _db.GetUserByGitHubId(githubId);
                if (existingById != null)
                {
                    // Update email if different
                    if (!string.IsNullOrWhiteSpace(email) && !email.Contains("@users.noreply.github.com") && existingById.Email != email)
                    {
                        await _db.UpdateUserEmail(existingById.Id, email);
                        existingById.Email = email;
                        _logger.LogInformation($"📝 Updated email for user '{existingById.AuthorName}' to '{email}'");
                    }
                    return existingById;
                }
            }
            
            // Then try by GitHub username
            var existingByUsername = await _db.GetUserByAuthorName(githubUsername);
            if (existingByUsername != null)
            {
                // Update email if different
                if (!string.IsNullOrWhiteSpace(email) && !email.Contains("@users.noreply.github.com") && existingByUsername.Email != email)
                {
                    await _db.UpdateUserEmail(existingByUsername.Id, email);
                    existingByUsername.Email = email;
                    _logger.LogInformation($"📝 Updated email for user '{existingByUsername.AuthorName}' to '{email}'");
                }
                return existingByUsername;
            }
            
            // User doesn't exist - ALWAYS call GitHub API to get real username before creating
            if (string.IsNullOrWhiteSpace(email) || email.Contains("@users.noreply.github.com"))
            {
                _logger.LogInformation($"🔍 Calling GitHub API to get real username for commit {commitSha[..7]}");
                try
                {
                    var commitAuthor = await _github.GetCommitAuthor(repoOwner, repoName, commitSha);
                    if (commitAuthor != null)
                    {
                        // Update githubId and username if we got better data
                        githubId = commitAuthor.Id;
                        githubUsername = commitAuthor.Login;
                        _logger.LogInformation($"✅ GitHub API confirmed: {githubUsername} (ID: {githubId})");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"⚠️ GitHub API call failed: {ex.Message}");
                }
            }
            
            // Create user - set email to null for noreply, or use real email
            var userEmail = email != null && email.Contains("@users.noreply.github.com") ? null : email;
            
            var newUser = await _db.CreateUser(new User
            {
                GithubId = githubId,
                AuthorName = githubUsername, // Use GitHub username, NOT Git config name
                Email = userEmail,
                AvatarUrl = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(githubUsername)}"
            });
            
            _logger.LogInformation($"➕ Created new user: {githubUsername} (GitHub ID: {githubId}, Email: {userEmail ?? "none"})");
            return newUser;
        }

        // Step 4: Fallback - couldn't get GitHub username (API failed or noreply parse failed)
        // Use Git config name as last resort
        _logger.LogWarning($"⚠️ Could not resolve GitHub username for '{username}' - using Git config name as fallback");
        
        var fallbackUser = await _db.GetUserByAuthorName(username);
        if (fallbackUser != null)
        {
            return fallbackUser;
        }
        
        var fallbackEmail = email != null && email.Contains("@users.noreply.github.com") ? null : email;
        var createdFallbackUser = await _db.CreateUser(new User
        {
            GithubId = 0,
            AuthorName = username, // Fallback to Git config name
            Email = fallbackEmail,
            AvatarUrl = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(username)}"
        });
        
        _logger.LogWarning($"➕ Created fallback user: {username}");
        return createdFallbackUser;
    }