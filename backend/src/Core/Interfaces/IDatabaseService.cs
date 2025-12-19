using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Core.Interfaces;

public interface IDatabaseService
{
    // Users
    Task<User?> GetUserByGitHubId(long githubId);
    Task<User?> GetUserByEmail(string email);
    Task<User?> GetUserByAuthorName(string authorName);
    Task<User> CreateUser(User user);
    Task<User?> GetUserById(Guid id);
    Task UpdateUserEmail(Guid userId, string email);
    Task UpdateUserAuthorName(Guid userId, string authorName);
    Task UpdateUserAvatar(Guid userId, string avatarUrl);
    Task<List<User>> GetUsersByAuthorNames(List<string> authorNames); // BATCH: Get multiple users at once

    // Repositories
    Task<Repository?> GetRepositoryByName(string owner, string name);
    Task<Repository> CreateRepository(Repository repository);
    Task UpdateRepositoryStatus(Guid repositoryId, string status);
    Task UpdateLastRefreshTime(Guid repositoryId);
    Task UpdateLastAnalyzedCommit(Guid repositoryId, string commitSha);
    Task<List<Repository>> GetUserRepositories(Guid userId);
    Task<Repository?> GetRepositoryById(Guid id);
    Task<List<Repository>> GetAnalyzedRepositories(Guid userId, string filter);

    // Repository User Access
    Task<bool> HasRepositoryAccess(Guid userId, Guid repositoryId);
    Task GrantRepositoryAccess(Guid userId, Guid repositoryId, Guid? grantedByUserId = null);
    Task<(string? userName, DateTime? analyzedAt)> GetRepositoryAnalyzer(Guid repositoryId);

    // Branches
    Task<Branch> CreateBranch(Branch branch);
    Task<List<Branch>> GetBranchesByRepository(Guid repositoryId);
    Task<Branch?> GetDefaultBranch(Guid repositoryId);
    Task<Branch?> GetBranchByName(Guid repositoryId, string branchName);
    Task UpdateBranchHead(Guid branchId, string commitSha);
    Task DeleteBranch(Guid branchId);

    // Commit-Branch Junction
    Task LinkCommitToBranch(Guid commitId, Guid branchId);
    Task<List<Guid>> GetBranchIdsForCommit(Guid commitId);

    // Commits
    Task<Commit> CreateCommit(Commit commit);
    Task<List<Commit>> GetCommitsByRepository(Guid repositoryId);
    Task<List<Commit>> GetCommitsByBranch(Guid repositoryId, string branchName);
    Task<Commit?> GetCommitById(Guid id);
    Task<Commit?> GetCommitBySha(Guid repositoryId, string sha);
    Task<List<Commit>> GetCommitsByIds(List<Guid> commitIds); // BATCH: Get multiple commits at once

    // Files
    Task<RepositoryFile?> GetFileByPath(Guid repositoryId, string filePath);
    Task<RepositoryFile> CreateFile(RepositoryFile file);
    Task<List<RepositoryFile>> GetFilesByRepository(Guid repositoryId);
    Task<List<RepositoryFile>> GetFilesByBranch(Guid repositoryId, string branchName);
    Task<RepositoryFile?> GetFileById(Guid fileId);
    Task<List<RepositoryFile>> GetFilesByIds(List<Guid> fileIds); // BATCH: Get multiple files at once

    // File Changes
    Task CreateFileChange(FileChange fileChange);
    Task<List<FileChange>> GetFileChangesByCommit(Guid commitId);
    Task<List<FileChange>> GetFileChangesByFile(Guid fileId);
    Task<List<Commit>> GetCommitsForFile(Guid fileId);
    Task<Dictionary<Guid, List<FileChange>>> GetFileChangesByFileIds(List<Guid> fileIds); // BATCH: Get changes for multiple files
    Task<Dictionary<Guid, List<FileChange>>> GetFileChangesByCommitIds(List<Guid> commitIds); // BATCH: Get changes for multiple commits

    // Embeddings
    Task<CodeEmbedding> CreateEmbedding(CodeEmbedding embedding);
    Task<List<CodeEmbedding>> GetEmbeddingsByFile(Guid fileId);
    Task<List<(RepositoryFile File, double Similarity)>> FindSimilarFiles(float[] embedding, Guid repositoryId, Guid excludeFileId, int limit = 10);

    // Dependencies
    Task CreateDependency(Dependency dependency);
    Task<List<Dependency>> GetDependenciesForFile(Guid fileId);
    Task<List<Dependency>> GetDependentsForFile(Guid fileId);

    // File Ownership
    Task UpsertFileOwnership(FileOwnership ownership);
    Task<List<FileOwnership>> GetFileOwnership(Guid fileId);
    Task<string?> GetMostActiveAuthorForFile(Guid fileId);
    Task<Dictionary<Guid, List<FileOwnership>>> GetFileOwnershipByFileIds(List<Guid> fileIds); // BATCH: Get ownership for multiple files

    // Pull Requests
    Task<PullRequest?> GetPullRequestByNumber(Guid repositoryId, int prNumber);
    Task<PullRequest> CreatePullRequest(PullRequest pr);
    Task<List<PullRequest>> GetOpenPullRequests(Guid repositoryId);
    Task<List<PullRequest>> GetAllPullRequests(Guid repositoryId);
    Task UpdatePullRequestState(Guid prId, string state);
    Task UpdatePullRequestTitle(Guid prId, string title);
    Task DeletePrFilesChangedByPrId(Guid prId);  // NEW: Clean up files for specific PR

    // PR Files
    Task CreatePrFileChanged(PrFileChanged prFile);
    Task<List<RepositoryFile>> GetPrFiles(Guid prId);
    Task<List<PrConflict>> GetPotentialConflicts(Guid prId, Guid repositoryId);  // NEW: Detect conflicts with other open PRs
    Task<bool> IsFileInOpenPr(Guid fileId);

    // Webhook Queue
    Task<long> EnqueueWebhook(string payload);
    Task<WebhookQueueItem?> GetNextPendingWebhook();
    Task UpdateWebhookStatus(long id, string status);

    // ============================================
    // PERSONALIZED DASHBOARD
    // ============================================
    
    // File Views (Recent Files)
    Task<List<FileView>> GetRecentFileViews(Guid userId, int limit = 10);
    Task UpsertFileView(Guid userId, Guid fileId);
    
    // File Bookmarks
    Task<List<FileBookmark>> GetFileBookmarks(Guid userId);
    Task<bool> IsFileBookmarked(Guid userId, Guid fileId);
    Task CreateFileBookmark(Guid userId, Guid fileId, string? category = null);
    Task DeleteFileBookmark(Guid userId, Guid fileId);
    
    // Team Activity
    Task<List<Commit>> GetTeamActivity(Guid userId, int limit = 20);
    
    // Quick Stats
    Task<int> GetUserFileViewCount(Guid userId);
    Task<int> GetUserRepositoryCount(Guid userId);
    Task<int> GetUserCommitCount(Guid userId);
    Task<int> GetUserPrsReviewedCount(Guid userId);
    
    // Pending Reviews (PRs waiting for user's input based on file ownership)
    Task<List<PullRequest>> GetPendingReviews(Guid userId, int limit = 10);
    
    // Debug helpers
    Task<bool> CheckUserHasCommitsInRepo(Guid userId, Guid repositoryId);
    Task<bool> CheckIsRequestedReviewer(Guid userId, Guid prId);
    
    // Debug helpers
    Task<List<PullRequest>> GetAllOpenPrs();
    Task<bool> CheckUserRepositoryAccess(Guid userId, Guid repositoryId);
    Task<bool> HasUserReviewedPr(Guid userId, Guid prId);
    
    // PR Requested Reviewers (NEW)
    Task CreatePrRequestedReviewer(PrRequestedReviewer reviewer);
    Task DeletePrRequestedReviewers(Guid prId);
    Task<List<PullRequest>> GetPrsWhereUserIsRequestedReviewer(Guid userId, int limit = 10);
}
