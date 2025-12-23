using LibGit2Sharp;

namespace CodeFamily.Api.Core.Interfaces;

public interface IRepositoryService
{
    /// <summary>
    /// Clone a repository as a bare clone for efficient access.
    /// If the repository already exists, fetches the latest changes from remote.
    /// Returns the path to the bare repository.
    /// </summary>
    /// <param name="accessToken">Optional GitHub access token for private repositories</param>
    Task<string> CloneBareRepository(string cloneUrl, string owner, string repoName, string? accessToken = null);

    /// <summary>
    /// Get the LibGit2Sharp Repository object for a bare clone.
    /// </summary>
    LibGit2Sharp.Repository GetRepository(string owner, string repoName);

    /// <summary>
    /// Read file content at a specific commit SHA.
    /// </summary>
    string? GetFileContentAtCommit(LibGit2Sharp.Repository repo, string commitSha, string filePath);

    /// <summary>
    /// Get all commits in reverse chronological order.
    /// </summary>
    List<LibGit2Sharp.Commit> GetAllCommits(LibGit2Sharp.Repository repo);

    /// <summary>
    /// Get file paths changed in a commit.
    /// </summary>
    List<string> GetChangedFiles(LibGit2Sharp.Repository repo, LibGit2Sharp.Commit commit);

    /// <summary>
    /// Fetch latest changes from remote.
    /// </summary>
    /// <param name="accessToken">Optional GitHub access token for private repositories</param>
    Task FetchRepository(string owner, string repoName, string? accessToken = null);

    /// <summary>
    /// Determine language from file extension.
    /// </summary>
    string GetLanguageFromPath(string filePath);

    /// <summary>
    /// Get all branch names in the repository.
    /// </summary>
    List<string> GetAllBranches(LibGit2Sharp.Repository repo);

    /// <summary>
    /// Get commits for a specific branch.
    /// </summary>
    List<LibGit2Sharp.Commit> GetCommitsByBranch(LibGit2Sharp.Repository repo, string branchName);

    /// <summary>
    /// Get actual line additions and deletions for a file in a commit.
    /// </summary>
    (int additions, int deletions) GetFileLineStats(LibGit2Sharp.Repository repo, LibGit2Sharp.Commit commit, string filePath);

    /// <summary>
    /// Get ALL file paths in the repository at HEAD commit.
    /// This enumerates the entire tree, not just changed files.
    /// </summary>
    List<string> GetAllFilesAtHead(LibGit2Sharp.Repository repo);

    /// <summary>
    /// Get ALL file paths in the repository at a specific branch's HEAD.
    /// This enumerates the entire tree for that branch.
    /// </summary>
    List<string> GetAllFilesAtBranch(LibGit2Sharp.Repository repo, string branchName);
}
