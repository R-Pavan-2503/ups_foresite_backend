using CodeFamily.Api.Core.Interfaces;
using LibGit2Sharp;
using Microsoft.Extensions.Options;
using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Core.Services;

/// <summary>
/// LibGit2Sharp repository management service.
/// Handles bare cloning and historical file access.
/// </summary>
public class RepositoryService : IRepositoryService
{
    private readonly string _cloneBasePath;

    public RepositoryService(IOptions<AppSettings> appSettings)
    {
        _cloneBasePath = appSettings.Value.CloneBasePath;
        Directory.CreateDirectory(_cloneBasePath);
    }

    public async Task<string> CloneBareRepository(string cloneUrl, string owner, string repoName)
    {
        var repoPath = Path.Combine(_cloneBasePath, $"{owner}_{repoName}.git");

        if (Directory.Exists(repoPath))
        {
            return repoPath; // Already cloned
        }

        await Task.Run(() =>
        {
            LibGit2Sharp.Repository.Clone(cloneUrl, repoPath, new CloneOptions
            {
                IsBare = true
            });
        });

        return repoPath;
    }

    public LibGit2Sharp.Repository GetRepository(string owner, string repoName)
    {
        var repoPath = Path.Combine(_cloneBasePath, $"{owner}_{repoName}.git");
        return new LibGit2Sharp.Repository(repoPath);
    }

    public string? GetFileContentAtCommit(LibGit2Sharp.Repository repo, string commitSha, string filePath)
    {
        var commit = repo.Lookup<LibGit2Sharp.Commit>(commitSha);
        if (commit == null) return null;

        var entry = commit[filePath];
        if (entry == null || entry.TargetType != TreeEntryTargetType.Blob)
            return null;

        var blob = (Blob)entry.Target;
        return blob.GetContentText();
    }

    public List<LibGit2Sharp.Commit> GetAllCommits(LibGit2Sharp.Repository repo)
    {
        return repo.Commits.QueryBy(new CommitFilter
        {
            SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Reverse
        }).ToList();
    }

    public List<string> GetChangedFiles(LibGit2Sharp.Repository repo, LibGit2Sharp.Commit commit)
    {
        if (commit.Parents.Any())
        {
            var parent = commit.Parents.First();
            var changes = repo.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree);
            return changes.Select(c => c.Path).ToList();
        }

        // First commit - all files are new - recursively enumerate all blobs
        var allFiles = new List<string>();
        EnumerateTreeRecursive(commit.Tree, "", allFiles);
        return allFiles;
    }

    private void EnumerateTreeRecursive(Tree tree, string basePath, List<string> files)
    {
        foreach (var entry in tree)
        {
            var fullPath = string.IsNullOrEmpty(basePath) ? entry.Name : $"{basePath}/{entry.Name}";
            
            if (entry.TargetType == TreeEntryTargetType.Blob)
            {
                // It's a file
                files.Add(fullPath);
            }
            else if (entry.TargetType == TreeEntryTargetType.Tree)
            {
                // It's a directory - recurse into it
                var subTree = (Tree)entry.Target;
                EnumerateTreeRecursive(subTree, fullPath, files);
            }
        }
    }

    public async Task FetchRepository(string owner, string repoName)
    {
        await Task.Run(() =>
        {
            using var repo = GetRepository(owner, repoName);
            var remote = repo.Network.Remotes["origin"];
            
            // Force update local refs to match remote (mirror behavior)
            // This ensures repo.Branches["main"] points to the latest commit
            var refSpecs = new[] { "+refs/heads/*:refs/heads/*" };
            
            var fetchOptions = new FetchOptions
            {
                Prune = true
            };
            
            Commands.Fetch(repo, remote.Name, refSpecs, fetchOptions, "fetch");

            // Manual Prune: Ensure local remote-tracking branches match remote exactly
            var remoteRefs = repo.Network.ListReferences(remote).Select(r => r.CanonicalName).ToHashSet();
            var localBranches = repo.Branches.ToList();

            foreach (var branch in localBranches)
            {
                // Check for stale remote-tracking branches (e.g. refs/remotes/origin/deleted-branch)
                if (branch.IsRemote && branch.Reference.CanonicalName.StartsWith("refs/remotes/origin/"))
                {
                    // Map refs/remotes/origin/branch -> refs/heads/branch
                    var expectedRemoteRef = branch.Reference.CanonicalName.Replace("refs/remotes/origin/", "refs/heads/");
                    
                    // If the remote doesn't have this ref anymore, delete our local tracking branch
                    if (!remoteRefs.Contains(expectedRemoteRef) && expectedRemoteRef != "refs/heads/HEAD")
                    {
                        repo.Branches.Remove(branch);
                    }
                }
            }
        });
    }

    public string GetLanguageFromPath(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLower();
        return ext switch
        {
            ".js" => "javascript",
            ".jsx" => "javascript",
            ".ts" => "typescript",
            ".tsx" => "typescript",
            ".py" => "python",
            ".go" => "go",
            ".java" => "java",
            ".cs" => "csharp",
            ".cpp" or ".cc" or ".cxx" => "cpp",
            ".c" or ".h" => "c",
            ".rs" => "rust",
            ".rb" => "ruby",
            ".php" => "php",
            _ => "unknown"
        };
    }

    public List<string> GetAllBranches(LibGit2Sharp.Repository repo)
    {
        return repo.Branches
            .Where(b => !b.IsRemote || b.FriendlyName.StartsWith("origin/"))
            .Select(b => b.FriendlyName.Replace("origin/", ""))
            .Distinct()
            .Where(name => name != "HEAD") // Filter out HEAD symbolic reference
            .OrderBy(name => name != "main" && name != "master") // Put main/master first
            .ThenBy(name => name)
            .ToList();
    }

    public List<LibGit2Sharp.Commit> GetCommitsByBranch(LibGit2Sharp.Repository repo, string branchName)
    {
        // Normalize branch name (remove origin/ prefix if present)
        var normalizedBranchName = branchName.Replace("origin/", "");
        
        // Try to find the branch (check both local and remote)
        var branch = repo.Branches[normalizedBranchName] 
                     ?? repo.Branches[$"origin/{normalizedBranchName}"];
        
        if (branch == null)
        {
            return new List<LibGit2Sharp.Commit>();
        }
        
        return branch.Commits
            .OrderByDescending(c => c.Author.When)
            .ToList();
    }

    public (int additions, int deletions) GetFileLineStats(LibGit2Sharp.Repository repo, LibGit2Sharp.Commit commit, string filePath)
    {
        if (!commit.Parents.Any())
        {
            // First commit - everything is added, nothing deleted
            var firstCommitContent = GetFileContentAtCommit(repo, commit.Sha, filePath);
            if (string.IsNullOrEmpty(firstCommitContent))
                return (0, 0);
            
            var lines = firstCommitContent.Split('\n').Length;
            return (lines, 0);
        }

        var parent = commit.Parents.First();
        var patch = repo.Diff.Compare<Patch>(parent.Tree, commit.Tree);
        
        foreach (var change in patch)
        {
            if (change.Path == filePath)
            {
                return (change.LinesAdded, change.LinesDeleted);
            }
        }

        return (0, 0);
    }
}
