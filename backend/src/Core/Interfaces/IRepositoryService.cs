using LibGit2Sharp;

namespace CodeFamily.Api.Core.Interfaces;

public interface IRepositoryService
{
    
    Task<string> CloneBareRepository(string cloneUrl, string owner, string repoName);

    LibGit2Sharp.Repository GetRepository(string owner, string repoName);

    string? GetFileContentAtCommit(LibGit2Sharp.Repository repo, string commitSha, string filePath);

    List<LibGit2Sharp.Commit> GetAllCommits(LibGit2Sharp.Repository repo);    

    List<string> GetChangedFiles(LibGit2Sharp.Repository repo, LibGit2Sharp.Commit commit);

    Task FetchRepository(string owner, string repoName);    

    string GetLanguageFromPath(string filePath);    

    List<string> GetAllBranches(LibGit2Sharp.Repository repo);

    List<LibGit2Sharp.Commit> GetCommitsByBranch(LibGit2Sharp.Repository repo, string branchName);

    (int additions, int deletions) GetFileLineStats(LibGit2Sharp.Repository repo, LibGit2Sharp.Commit commit, string filePath);
    
}