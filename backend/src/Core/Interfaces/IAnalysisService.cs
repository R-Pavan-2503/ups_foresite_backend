using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Core.Interfaces;

public interface IAnalysisService
{
    /// <summary>
    /// Run full ingestion pipeline for a repository.
    /// This is the CORE analysis workflow:
    /// 1. Bare clone
    /// 2. Walk commits
    /// 3. Extract functions (Tree-sitter)
    /// 4. Generate embeddings (Gemini)
    /// 5. Calculate semantic ownership
    /// 6. Attach webhook
    /// </summary>
    /// <param name="accessToken">Optional GitHub access token for private repositories</param>
    Task AnalyzeRepository(string owner, string repoName, Guid repositoryId, Guid userId, string? accessToken = null);

    /// <summary>
    /// Calculate semantic ownership for a file based on embedding deltas.
    /// NOT based on lines of code.
    /// </summary>
    Task CalculateSemanticOwnership(Guid fileId, Guid repositoryId);

    /// <summary>
    /// Calculate risk score between a push and open PRs.
    /// Risk = (Structural Overlap × 0.4) + (Semantic Overlap × 0.6)
    /// </summary>
    Task<RiskAnalysisResult> CalculateRisk(Guid repositoryId, List<string> changedFiles, List<float[]> newEmbeddings);

    /// <summary>
    /// Process incremental update from webhook.
    /// Only analyze changed files.
    /// </summary>
    Task ProcessIncrementalUpdate(Guid repositoryId, string commitSha, List<string> changedFiles);

    /// <summary>
    /// Fetch and store pull requests from GitHub API.
    /// Syncs PR state, merges, and reviewers.
    /// </summary>
    Task FetchAndStorePullRequests(string owner, string repo, Guid repositoryId, string? accessToken = null);

    /// <summary>
    /// Get or create author user from commit information.
    /// </summary>
    Task<Core.Models.User> GetOrCreateAuthorUser(string email, string username, string repoOwner, string repoName, string commitSha, Guid repositoryId);

    /// <summary>
    /// Process a single file at a specific commit.
    /// </summary>
    Task ProcessFile(LibGit2Sharp.Repository repo, Core.Models.Commit commit, LibGit2Sharp.Commit gitCommit, string filePath, Guid authorId, string authorEmail);

    /// <summary>
    /// Ensure all files at HEAD are in the database.
    /// </summary>
    Task EnsureAllFilesAtHead(LibGit2Sharp.Repository repo, Guid repositoryId);

    /// <summary>
    /// Calculate ownership for files that were modified.
    /// </summary>
    Task CalculateOwnershipForModifiedFiles(Guid repositoryId);

    /// <summary>
    /// Reconcile dependencies at HEAD.
    /// </summary>
    Task ReconcileDependenciesAtHead(LibGit2Sharp.Repository repo, Guid repositoryId);

    /// <summary>
    /// Calculate dependency graph metrics.
    /// </summary>
    Task CalculateDependencyMetrics(Guid repositoryId);

    /// <summary>
    /// Clear the file author deltas tracker.
    /// </summary>
    void ClearFileAuthorDeltas();
}
