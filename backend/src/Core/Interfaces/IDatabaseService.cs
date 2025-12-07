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

    // Repositories
    Task<Repository?> GetRepositoryByName(string owner, string name);
    Task<Repository> CreateRepository(Repository repository);
    Task UpdateRepositoryStatus(Guid repositoryId, string status);
    Task UpdateLastRefreshTime(Guid repositoryId);
    Task UpdateLastAnalyzedCommit(Guid repositoryId, string commitSha);
    Task<List<Repository>> GetUserRepositories(Guid userId);
    Task<Repository?> GetRepositoryById(Guid id);
    Task<List<Repository>> GetAnalyzedRepositories(Guid userId, string filter);

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

    // Files
    Task<RepositoryFile?> GetFileByPath(Guid repositoryId, string filePath);
    Task<RepositoryFile> CreateFile(RepositoryFile file);
    Task<List<RepositoryFile>> GetFilesByRepository(Guid repositoryId);
    Task<List<RepositoryFile>> GetFilesByBranch(Guid repositoryId, string branchName);
    Task<RepositoryFile?> GetFileById(Guid fileId);

    // File Changes
    Task CreateFileChange(FileChange fileChange);
    Task<List<FileChange>> GetFileChangesByCommit(Guid commitId);
    Task<List<FileChange>> GetFileChangesByFile(Guid fileId);

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

    // Webhook Queue
    Task<long> EnqueueWebhook(string payload);
    Task<WebhookQueueItem?> GetNextPendingWebhook();
    Task UpdateWebhookStatus(long id, string status);
}
