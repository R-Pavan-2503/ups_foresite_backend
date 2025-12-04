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