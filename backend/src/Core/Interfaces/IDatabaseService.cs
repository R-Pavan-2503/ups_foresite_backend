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


}