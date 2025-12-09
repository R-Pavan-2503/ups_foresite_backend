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

    // ---------------------------------------------------------------------
    // Process a single file at a specific commit
    // ---------------------------------------------------------------------
    private async Task ProcessFile(
        LibGitRepository repo,
        DbCommit commit,
        LibGitCommit gitCommit,
        string filePath,
        Guid authorId,
        string authorEmail)
    {
        // Ensure file record exists
        var file = await _db.GetFileByPath(commit.RepositoryId, filePath);
        if (file == null)
        {
            file = await _db.CreateFile(new RepositoryFile
            {
                RepositoryId = commit.RepositoryId,
                FilePath = filePath
            });
        }

        // Retrieve file content at this commit
        var content = _repoService.GetFileContentAtCommit(repo, gitCommit.Sha, filePath);
        if (string.IsNullOrEmpty(content)) return; // nothing to analyse

        // Determine language for parsing
        var language = _repoService.GetLanguageFromPath(filePath);
        if (language == "unknown") return; // skip unsupported files

        // Parse source code using Tree‑sitter
        var parseResult = await _treeSitter.ParseCode(content, language);

        // -----------------------------------------------------------------
        // Embeddings & semantic delta tracking
        // -----------------------------------------------------------------
        var previousEmbeddings = await _db.GetEmbeddingsByFile(file.Id);
        var currentDeltas = new List<double>();
        foreach (var function in parseResult.Functions)
        {
            try
            {
                var embedding = await _gemini.GenerateEmbedding(function.Code);
                await _db.CreateEmbedding(new CodeEmbedding
                {
                    FileId = file.Id,
                    Embedding = embedding,
                    ChunkContent = function.Code,
                    CreatedAt = DateTime.UtcNow
                });

                if (previousEmbeddings.Count > 0)
                {
                    var delta = CalculateSemanticDelta(embedding, previousEmbeddings[^1].Embedding);
                    currentDeltas.Add(delta);
                    _logger.LogInformation($"📊 File {file.Id} has semantic delta of {delta}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"⚠️ Failed to generate embedding for function in {filePath}: {ex.Message}");
            }
        }

        // -----------------------------------------------------------------
        // Ownership contribution aggregation
        // -----------------------------------------------------------------
        if (currentDeltas.Count > 0)
        {
            if (!_fileAuthorDeltas.ContainsKey(file.Id))
                _fileAuthorDeltas[file.Id] = new Dictionary<string, List<double>>();

            if (!_fileAuthorDeltas[file.Id].ContainsKey(authorEmail))
                _fileAuthorDeltas[file.Id][authorEmail] = new List<double>();

            _fileAuthorDeltas[file.Id][authorEmail].AddRange(currentDeltas);
        }

        // -----------------------------------------------------------------
        // Dependency creation based on imports
        // -----------------------------------------------------------------
        // -----------------------------------------------------------------
        // Dependency creation based on imports
        // -----------------------------------------------------------------
        _logger.LogInformation($"🔍 Analyzing dependencies for {filePath}. Found {parseResult.Imports.Count} imports.");
        
        foreach (var import in parseResult.Imports)
        {
            try
            {
                _logger.LogInformation($"  👉 Processing import: '{import.Module}'");
                var targetPath = await ResolveImportPathAsync(filePath, import.Module, language, commit.RepositoryId);
                
                if (targetPath != null)
                {
                    _logger.LogInformation($"    ✅ Resolved path: {targetPath}");
                    var targetFile = await _db.GetFileByPath(commit.RepositoryId, targetPath);
                    
                    if (targetFile != null)
                    {
                        await _db.CreateDependency(new Dependency
                        {
                            SourceFileId = file.Id,
                            TargetFileId = targetFile.Id,
                            DependencyType = "import",
                            Strength = 1
                        });
                        _logger.LogInformation($"    🔗 Created dependency: {filePath} -> {targetPath}");
                    }
                    else
                    {
                        _logger.LogWarning($"    ⚠️ Resolved path '{targetPath}' not found in database for repo {commit.RepositoryId}");
                    }
                }
                else
                {
                    _logger.LogInformation($"    ❌ Could not resolve path for '{import.Module}'");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"    ⚠️ Exception processing import '{import.Module}': {ex.Message}");
            }
        }

        // -----------------------------------------------------------------
        // Record file change with REAL additions/deletions from Git diff
        // -----------------------------------------------------------------
        var (additions, deletions) = _repoService.GetFileLineStats(repo, gitCommit, filePath);
        await _db.CreateFileChange(new FileChange
        {
            CommitId = commit.Id,
            FileId = file.Id,
            Additions = additions,
            Deletions = deletions
        });
    }

    // ---------------------------------------------------------------------
    // Calculate and persist ownership percentages for all files in a repo
    // ---------------------------------------------------------------------
    private async Task CalculateAllFileOwnership(Guid repositoryId)
    {
        var files = await _db.GetFilesByRepository(repositoryId);
        foreach (var file in files)
        {
            await CalculateSemanticOwnership(file.Id, repositoryId);
        }
    }

    public async Task CalculateSemanticOwnership(Guid fileId, Guid repositoryId)
    {
        if (!_fileAuthorDeltas.ContainsKey(fileId)) return;

        var authorContributions = _fileAuthorDeltas[fileId];
        var totalDelta = authorContributions.Values.SelectMany(d => d).Sum();
        if (totalDelta == 0) return;

        foreach (var kvp in authorContributions)
        {
            var authorEmail = kvp.Key;
            var deltas = kvp.Value;
            var authorDelta = deltas.Sum();
            var ownershipScore = (decimal)(authorDelta / totalDelta * 100);

            // User should already exist from commit processing - just look up by email
            var user = await _db.GetUserByEmail(authorEmail);
            if (user == null)
            {
                _logger.LogWarning($"⚠️ User with email {authorEmail} not found during ownership calculation");
                continue;
            }

            _logger.LogInformation($"👤 File {fileId}: {authorEmail} owns {ownershipScore:F2}%");
            await _db.UpsertFileOwnership(new FileOwnership
            {
                FileId = fileId,
                AuthorName = authorEmail,
                SemanticScore = ownershipScore,
                LastUpdated = DateTime.UtcNow
            });
        }
    }

    // ---------------------------------------------------------------------
    // Dependency graph metrics (blast radius etc.)
    // ---------------------------------------------------------------------
    private async Task CalculateDependencyMetrics(Guid repositoryId)
    {
        var files = await _db.GetFilesByRepository(repositoryId);
        foreach (var file in files)
        {
            var dependencies = await _db.GetDependenciesForFile(file.Id);
            var dependents = await _db.GetDependentsForFile(file.Id);
            _logger.LogInformation($"📦 File {file.FilePath}: {dependencies.Count} dependencies, {dependents.Count} dependents");
            // Blast radius is simply the number of dependents for now
            // TODO: Persist if needed
        }
    }


    // ---------------------------------------------------------------------
    // Reconcile dependencies at HEAD - catch any missing dependencies
    // where target files were added after their importers
    // ---------------------------------------------------------------------
    private async Task ReconcileDependenciesAtHead(LibGitRepository repo, Guid repositoryId)
    {
        var headCommit = repo.Head.Tip;
        if (headCommit == null)
        {
            _logger.LogWarning("No HEAD commit found, skipping reconciliation");
            return;
        }

        var allFiles = await _db.GetFilesByRepository(repositoryId);
        _logger.LogInformation($"🔍 Reconciling {allFiles.Count} files at HEAD commit {headCommit.Sha[..7]}");

        int reconciledCount = 0;
        foreach (var file in allFiles)
        {
            try
            {
                // Get file content at HEAD
                var content = _repoService.GetFileContentAtCommit(repo, headCommit.Sha, file.FilePath);
                if (content == null) continue;

                var language = _repoService.GetLanguageFromPath(file.FilePath);
                if (language == "unknown") continue;

                // Extract imports using ParseCode
                var parseResult = await _treeSitter.ParseCode(content, language);
                if (parseResult?.Imports == null || parseResult.Imports.Count == 0) continue;

                // Try to resolve each import and create missing dependencies
                foreach (var import in parseResult.Imports)
                {
                    try
                    {
                        var targetPath = await ResolveImportPathAsync(file.FilePath, import.Module, language, repositoryId);
                        if (targetPath != null)
                        {
                            var targetFile = await _db.GetFileByPath(repositoryId, targetPath);
                            if (targetFile != null)
                            {
                                // Check if dependency already exists
                                var existingDeps = await _db.GetDependenciesForFile(file.Id);
                                if (!existingDeps.Any(d => d.TargetFileId == targetFile.Id))
                                {
                                    // Create the missing dependency
                                    await _db.CreateDependency(new Dependency
                                    {
                                        SourceFileId = file.Id,
                                        TargetFileId = targetFile.Id
                                    });
                                    _logger.LogInformation($"  ✨ Reconciled: {file.FilePath} -> {targetPath}");
                                    reconciledCount++;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"  ⚠️ Error reconciling import '{import.Module}' in {file.FilePath}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"  ⚠️ Error reconciling file {file.FilePath}: {ex.Message}");
            }
        }

        _logger.LogInformation($"✅ Reconciled {reconciledCount} missing dependencies");
    }

    // ---------------------------------------------------------------------
    // Register GitHub webhook via the GitHub service
    // ---------------------------------------------------------------------
    // ---------------------------------------------------------------------
// Register GitHub webhook via the GitHub service
// ---------------------------------------------------------------------
private async Task RegisterWebhook(string owner, string repo)
{
    try
    {
        await _github.RegisterWebhook(owner, repo);
        _logger.LogInformation($"✅ Webhook registered for {owner}/{repo}");
    }
    catch (Exception ex)
    {
        _logger.LogWarning($"⚠️ Failed to register webhook for {owner}/{repo}. Error: {ex.Message}");
        _logger.LogWarning($"   Ensure the GitHub App is installed on this repository and has 'write' permissions for webhooks.");
    }
}

// ---------------------------------------------------------------------
// Fetch and store pull requests from GitHub API
// ---------------------------------------------------------------------
private async Task FetchAndStorePullRequests(string owner, string repo, Guid repositoryId)
{
   try
    {
        _logger.LogInformation($"📋 Attempting to fetch PRs for: {owner}/{repo}");
        _logger.LogInformation($"   Repository ID: {repositoryId}");
        
        // Fetch ALL pull requests (open and closed)
        var openRequest = new Octokit.PullRequestRequest { State = Octokit.ItemStateFilter.Open };
        var closedRequest = new Octokit.PullRequestRequest { State = Octokit.ItemStateFilter.Closed };
        
        _logger.LogInformation($"   🔍 Fetching open PRs from GitHub API...");
        var openPRs = await _github.GetPullRequests(owner, repo, openRequest);
        _logger.LogInformation($"   ✅ Fetched {openPRs.Count} open PRs");
        
        _logger.LogInformation($"   🔍 Fetching closed PRs from GitHub API...");
        var closedPRs = await _github.GetPullRequests(owner, repo, closedRequest);
        
        var allPRs = openPRs.Concat(closedPRs).ToList();
        _logger.LogInformation($"   Found {allPRs.Count} total pull requests ({openPRs.Count} open, {closedPRs.Count} closed)");

        int createdCount = 0;
        int skippedCount = 0;

        foreach (var pr in allPRs)
        {
            try
            {
                // Check if PR already exists
                var existing = await _db.GetPullRequestByNumber(repositoryId, pr.Number);
                if (existing != null)
                {
                    // Backfill title if missing
                    if (string.IsNullOrEmpty(existing.Title) && !string.IsNullOrEmpty(pr.Title))
                    {
                        await _db.UpdatePullRequestTitle(existing.Id, pr.Title);
                        _logger.LogInformation($"   Updated title for PR #{pr.Number}");
                    }
                    
                    // ✨ NEW: Sync PR state from GitHub
                    var githubState = pr.State.StringValue; // "open", "closed"
                    var dbState = existing.State ?? "unknown";
                    
                    if (!githubState.Equals(dbState, StringComparison.OrdinalIgnoreCase))
                    {
                        // State has changed - update database
                        await _db.UpdatePullRequestState(existing.Id, githubState);
                        _logger.LogInformation($"   ✅ Updated PR #{pr.Number} state: {dbState} → {githubState}");
                        
                        // If PR was closed/merged, clean up pr_files_changed table
                        if (githubState.Equals("closed", StringComparison.OrdinalIgnoreCase))
                        {
                            await _db.DeletePrFilesChangedByPrId(existing.Id);
                            _logger.LogInformation($"   🧹 Cleaned up pr_files_changed for closed PR #{pr.Number}");
                        }
                    }
                    
                    skippedCount++;
                    continue;
                }

                // Get or create author user - we have full GitHub data for PRs
                var authorGithubId = pr.User.Id; // Real GitHub ID
                var authorEmail = pr.User.Email; // Can be null - that's OK
                var authorUsername = pr.User.Login; // Real GitHub username
                
                // For PRs, we have real GitHub data so we can directly create/find user
                var author = await _db.GetUserByGitHubId(authorGithubId);
                if (author == null)
                {
                    author = await _db.GetUserByAuthorName(authorUsername);
                }
                if (author == null)
                {
                    author = await _db.CreateUser(new User
                    {
                        GithubId = authorGithubId,
                        AuthorName = authorUsername,
                        Email = authorEmail,
                        AvatarUrl = pr.User.AvatarUrl ?? $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(authorUsername)}"
                    });
                    _logger.LogInformation($"➕ Created user from PR: {authorUsername}");
                }

                // Create PR record
                var dbPr = await _db.CreatePullRequest(new PullRequest
                {
                    RepositoryId = repositoryId,
                    PrNumber = pr.Number,
                    Title = pr.Title,
                    State = pr.State.StringValue,
                    AuthorId = author.Id
                });

                // Fetch and store PR file changes ONLY for open PRs
                if (pr.State.StringValue.Equals("open", StringComparison.OrdinalIgnoreCase))
                {
                    var prFiles = await _github.GetPullRequestFiles(owner, repo, pr.Number);
                    _logger.LogInformation($"   PR #{pr.Number}: {prFiles.Count} files changed");

                    foreach (var prFile in prFiles)
                    {
                        // Find or create the file in our database
                        var file = await _db.GetFileByPath(repositoryId, prFile.FileName);
                        if (file == null)
                        {
                            // File doesn't exist yet (likely in a feature branch)
                            // Create it so PR files can be tracked
                            _logger.LogInformation($"     ➕ Creating file record for '{prFile.FileName}' from PR #{pr.Number}");
                            file = await _db.CreateFile(new RepositoryFile
                            {
                                RepositoryId = repositoryId,
                                FilePath = prFile.FileName,
                                TotalLines = null // We don't have this info yet
                            });
                        }

                        // Now add to PR files changed
                        await _db.CreatePrFileChanged(new PrFileChanged
                        {
                            PrId = dbPr.Id,
                            FileId = file.Id
                        });
                    }
                }
                else
                {
                    _logger.LogInformation($"   PR #{pr.Number}: Skipping file storage for closed PR");
                }

                createdCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"   ⚠️ Failed to process PR #{pr.Number}: {ex.Message}");
            }
        }

        _logger.LogInformation($"✅ Stored {createdCount} pull requests, skipped {skippedCount} existing");
    }
    catch (Exception ex)
    {
        _logger.LogError($"❌ Failed to fetch pull requests for {owner}/{repo}");
        _logger.LogError($"   Error Type: {ex.GetType().Name}");
        _logger.LogError($"   Error Message: {ex.Message}");
        if (ex.InnerException != null)
        {
            _logger.LogError($"   Inner Exception: {ex.InnerException.Message}");
        }
        _logger.LogError($"   Stack Trace: {ex.StackTrace}");
        // Don't throw - PR fetching is not critical for analysis
    }
}




    //---------------------------------------------------------------------
    // Helper: Resolve import paths using repository files from database
    // ---------------------------------------------------------------------
    private async Task<string?> ResolveImportPathAsync(string sourceFile, string importModule, string language, Guid repositoryId)
    {
        // Normalize paths
        sourceFile = sourceFile.Replace("\\", "/");
        var sourceDir = Path.GetDirectoryName(sourceFile)?.Replace("\\", "/");
        
        // Handle empty sourceDir (file in root directory)
        if (string.IsNullOrEmpty(sourceDir))
        {
            sourceDir = "";
        }
        
        _logger.LogInformation($"    Resolving '{importModule}' from '{sourceDir}' (Language: {language})");


        string? targetPath = null;

        // Handle relative imports (./ or ../)
        if (importModule.StartsWith("./") || importModule.StartsWith("../"))
        {
            // Normalize: combine source directory with import path
            string combinedPath;
            if (string.IsNullOrEmpty(sourceDir))
            {
                // File is in root, just use the import path
                combinedPath = importModule;
            }
            else
            {
                // Combine with source directory
                combinedPath = sourceDir + "/" + importModule;
            }
            
            // Use Path.GetFullPath with a fake root to resolve .. and .
            var fakeRoot = "C:\\fakeroot";  // Use Windows path for GetFullPath
            var combined = Path.Combine(fakeRoot, combinedPath.Replace("/", "\\"));
            
            try
            {
                var resolved = Path.GetFullPath(combined);
                
                // Check if path went above root
                if (!resolved.StartsWith(fakeRoot, StringComparison.OrdinalIgnoreCase))
                {
                    targetPath = null;  // Path escaped root
                }
                else
                {
                    // Remove fake root and normalize to forward slashes
                    targetPath = resolved.Substring(fakeRoot.Length)
                        .TrimStart('\\', '/')
                        .Replace("\\", "/");
                }
            }
            catch
            {
                targetPath = null;
            }
        }
        else if (language is "javascript" or "typescript")
        {
            // Non-relative import - could be local package or npm module
            // Try to find it in the repository
            var allFiles = await _db.GetFilesByRepository(repositoryId);
            var matches = allFiles.Where(f => f.FilePath.Contains(importModule)).ToList();
            
            if (matches.Count == 1)
            {
                return matches[0].FilePath;
            }
            else if (matches.Count > 1)
            {
                // Prefer exact match
                var exact = matches.FirstOrDefault(f => 
                    f.FilePath.EndsWith("/" + importModule) ||
                    f.FilePath.EndsWith("/" + importModule + ".js") ||
                    f.FilePath.EndsWith("/" + importModule + ".jsx") ||
                    f.FilePath.EndsWith("/" + importModule + ".ts") ||
                    f.FilePath.EndsWith("/" + importModule + ".tsx"));
                if (exact != null) return exact.FilePath;
            }
            
            // If no match, it's likely an npm package - skip
            return null;
        }
        else
        {
            // Other languages - only handle relative imports for now
            return null;
        }

        // If targetPath is null, we couldn't resolve it
        if (targetPath == null)
        {
            return null;
        }

        _logger.LogInformation($"    → Resolved to targetPath: '{targetPath}'");

        // Get all repository files
        var repoFiles = await _db.GetFilesByRepository(repositoryId);
        
        // Try exact match
        var exactFile = repoFiles.FirstOrDefault(f => f.FilePath.Equals(targetPath, StringComparison.OrdinalIgnoreCase));
        if (exactFile != null) return exactFile.FilePath;

        // Try with common extensions
        var extensions = new[] { ".js", ".jsx", ".ts", ".tsx", ".py", ".go", ".java", ".cs" };
        foreach (var ext in extensions)
        {
            var withExt = repoFiles.FirstOrDefault(f => f.FilePath.Equals(targetPath + ext, StringComparison.OrdinalIgnoreCase));
            if (withExt != null) return withExt.FilePath;
        }

        // Try index files
        foreach (var ext in extensions)
        {
            var indexPath = Path.Combine(targetPath, "index" + ext).Replace("\\", "/");
            var indexFile = repoFiles.FirstOrDefault(f => f.FilePath.Equals(indexPath, StringComparison.OrdinalIgnoreCase));
            if (indexFile != null) return indexFile.FilePath;
        }

        return null;
    }

    // ---------------------------------------------------------------------
    // Semantic delta calculation (Euclidean distance)
    // ---------------------------------------------------------------------
    private double CalculateSemanticDelta(float[] newEmbedding, float[] oldEmbedding)
    {
        double sum = 0;
        int len = Math.Min(newEmbedding.Length, oldEmbedding.Length);
        for (int i = 0; i < len; i++)
        {
            var diff = newEmbedding[i] - oldEmbedding[i];
            sum += diff * diff;
        }
        return Math.Sqrt(sum);
    }

    // ---------------------------------------------------------------------
    // Existing interface methods (kept for compatibility)
    // ---------------------------------------------------------------------
    public async Task<double> CalculateRisk(Guid fileId1, Guid fileId2)
    {
        var embeddings1 = await _db.GetEmbeddingsByFile(fileId1);
        var embeddings2 = await _db.GetEmbeddingsByFile(fileId2);
        if (embeddings1.Count == 0 || embeddings2.Count == 0) return 0;
        var e1 = embeddings1[^1].Embedding;
        var e2 = embeddings2[^1].Embedding;
        double dot = 0, norm1 = 0, norm2 = 0;
        for (int i = 0; i < Math.Min(e1.Length, e2.Length); i++)
        {
            dot += e1[i] * e2[i];
            norm1 += e1[i] * e1[i];
            norm2 += e2[i] * e2[i];
        }
        return dot / (Math.Sqrt(norm1) * Math.Sqrt(norm2));
    }

    public async Task<RiskAnalysisResult> CalculateRisk(Guid repositoryId, List<string> changedFiles, List<float[]> newEmbeddings)
    {
        var result = new RiskAnalysisResult
        {
            RiskScore = 0,
            StructuralOverlap = 0,
            SemanticOverlap = 0,
            ConflictingPrs = new List<ConflictingPr>()
        };
        try
        {
            var openPRs = await _db.GetOpenPullRequests(repositoryId);
            foreach (var pr in openPRs)
            {
                var prFiles = await _db.GetPrFiles(pr.Id);
                var structuralOverlap = changedFiles.Intersect(prFiles.Select(f => f.FilePath)).ToList();
                double maxSemanticSimilarity = 0;
                foreach (var changedFile in changedFiles)
                {
                    var file = await _db.GetFileByPath(repositoryId, changedFile);
                    if (file == null) continue;
                    foreach (var prFile in prFiles)
                    {
                        var similarity = await CalculateRisk(file.Id, prFile.Id);
                        if (similarity > maxSemanticSimilarity) maxSemanticSimilarity = similarity;
                    }
                }
                var structuralScore = structuralOverlap.Count > 0 ? 1.0 : 0.0;
                var semanticScore = maxSemanticSimilarity;
                var riskScore = (structuralScore * 0.4) + (semanticScore * 0.6);
                if (riskScore > 0.8)
                {
                    result.ConflictingPrs.Add(new ConflictingPr
                    {
                        PrNumber = pr.PrNumber,
                        Title = null,
                        Risk = riskScore,
                        ConflictingFiles = structuralOverlap
                    });
                }
                result.RiskScore = Math.Max(result.RiskScore, riskScore);
                result.StructuralOverlap = Math.Max(result.StructuralOverlap, structuralScore);
                result.SemanticOverlap = Math.Max(result.SemanticOverlap, semanticScore);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Risk calculation failed: {ex.Message}");
        }
        return result;
    }

    public async Task ProcessIncrementalUpdate(Guid repositoryId, string commitSha, List<string> changedFiles)
    {
        _logger.LogInformation($"Processing incremental update for commit {commitSha}");
        try
        {
            var repository = await _db.GetRepositoryById(repositoryId);
            if (repository == null)
            {
                _logger.LogError($"Repository {repositoryId} not found");
                return;
            }
            using var repo = _repoService.GetRepository(repository.OwnerUsername, repository.Name);
            
            // Always get the Git commit object
            var gitCommit = repo.Lookup(commitSha) as LibGitCommit;
            if (gitCommit == null)
            {
                _logger.LogError($"Commit {commitSha} not found in repository");
                return;
            }
            
            var commit = await _db.GetCommitBySha(repositoryId, commitSha);
            if (commit == null)
            {
                commit = await _db.CreateCommit(new DbCommit
                {
                    RepositoryId = repositoryId,
                    Sha = commitSha,
                    Message = gitCommit.MessageShort,
                    CommittedAt = gitCommit.Author.When.UtcDateTime
                });
            }
            foreach (var filePath in changedFiles)
            {
                // For incremental updates we don't have author info – use commit author if available
                var placeholderEmail = commit.AuthorEmail ?? "incremental@update.com";
                var placeholderUserId = commit.AuthorUserId ?? Guid.Empty;
                
                if (placeholderUserId == Guid.Empty)
                {
                    _logger.LogWarning($"⚠️ No author for incremental update, skipping file {filePath}");
                    continue;
                }
                
                await ProcessFile(repo, commit, gitCommit, filePath, placeholderUserId, placeholderEmail);
            }
            _logger.LogInformation($"Incremental update complete for {changedFiles.Count} files");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Incremental update failed: {ex.Message}");
            throw;
        }
    }
}
